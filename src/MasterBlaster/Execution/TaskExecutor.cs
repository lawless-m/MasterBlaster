namespace MasterBlaster.Execution;

using System.Diagnostics;
using MasterBlaster.Claude;
using MasterBlaster.Config;
using MasterBlaster.Execution.ActionHandlers;
using MasterBlaster.Logging;
using MasterBlaster.Mbl;
using MasterBlaster.Rdp;
using Microsoft.Extensions.Logging;

public class TaskExecutionResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? FailedAtStep { get; init; }
    public Dictionary<string, string> Outputs { get; init; } = new();
    public int StepsCompleted { get; init; }
    public int StepsTotal { get; init; }
    public long DurationMs { get; init; }
    public string? LogFile { get; init; }
    public string? ScreenshotPath { get; init; }
}

public class TaskAbortException : Exception
{
    public TaskAbortException(string message) : base(message) { }
}

public class TaskExecutor
{
    private readonly IRdpController _rdp;
    private readonly IClaudeClient _claude;
    private readonly TasksConfig _tasksConfig;
    private readonly TaskLogger _logger;
    private readonly ILogger<TaskExecutor> _log;

    /// <summary>
    /// Tracks whether the executor is currently running a task, and if so which one.
    /// </summary>
    public string? CurrentTaskName { get; private set; }
    public string? CurrentStepName { get; private set; }
    public bool IsRunning => CurrentTaskName is not null;

    public TaskExecutor(
        IRdpController rdp,
        IClaudeClient claude,
        TasksConfig tasksConfig,
        TaskLogger logger,
        ILogger<TaskExecutor> log)
    {
        _rdp = rdp;
        _claude = claude;
        _tasksConfig = tasksConfig;
        _logger = logger;
        _log = log;
    }

    public async Task<TaskExecutionResult> ExecuteAsync(
        TaskDefinition task,
        Dictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var logFile = _logger.StartTaskLog(task.Name);

        var ctx = new ExecutionContext
        {
            TaskName = task.Name,
            Parameters = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase),
        };

        CurrentTaskName = task.Name;
        CurrentStepName = null;

        _logger.LogTaskStart(task.Name);
        _log.LogInformation("Executing task \"{TaskName}\" with {ParamCount} parameters",
            task.Name, parameters.Count);

        int stepsCompleted = 0;

        try
        {
            // Validate all required inputs are provided
            foreach (var input in task.Inputs)
            {
                if (!parameters.ContainsKey(input))
                {
                    throw new InvalidOperationException(
                        $"Required input \"{input}\" was not provided for task \"{task.Name}\".");
                }
            }

            for (int stepIdx = 0; stepIdx < task.Steps.Count; stepIdx++)
            {
                ct.ThrowIfCancellationRequested();

                var step = task.Steps[stepIdx];
                ctx.CurrentStepIndex = stepIdx;
                ctx.CurrentStepName = step.Description;
                CurrentStepName = step.Description;

                _logger.LogStepStart(task.Name, step.Description, stepIdx);

                // Create a per-step timeout if the step specifies one
                var stepTimeout = step.TimeoutSeconds ?? _tasksConfig.DefaultExpectTimeoutSeconds;
                using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                stepCts.CancelAfter(TimeSpan.FromSeconds(stepTimeout));

                try
                {
                    await ExecuteActionsAsync(step.Actions, ctx, stepCts.Token);
                }
                catch (OperationCanceledException) when (stepCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    // The step timed out (not the overall cancellation token)
                    _log.LogWarning(
                        "Step \"{Step}\" timed out after {Timeout}s",
                        step.Description, stepTimeout);

                    if (task.OnTimeout is not null)
                    {
                        _log.LogInformation("Running on-timeout handler");
                        await ExecuteActionsAsync(task.OnTimeout.Actions, ctx, ct);
                    }

                    throw new TimeoutException(
                        $"Step \"{step.Description}\" timed out after {stepTimeout}s.");
                }
                catch (TimeoutException)
                {
                    // Expect retries exhausted — run the on-timeout handler if available
                    if (task.OnTimeout is not null)
                    {
                        _log.LogInformation("Running on-timeout handler after expect failure");
                        await ExecuteActionsAsync(task.OnTimeout.Actions, ctx, ct);
                    }
                    throw;
                }

                _logger.LogStepComplete(task.Name, step.Description, stepIdx);
                stepsCompleted++;
            }

            sw.Stop();
            _logger.LogTaskComplete(task.Name, success: true, durationMs: sw.ElapsedMilliseconds);
            _logger.FlushLog();

            var lastScreenshot = ctx.ScreenshotPaths.Count > 0 ? ctx.ScreenshotPaths[^1] : null;

            _log.LogInformation(
                "Task \"{TaskName}\" completed successfully in {Duration}ms",
                task.Name, sw.ElapsedMilliseconds);

            return new TaskExecutionResult
            {
                Success = true,
                Outputs = ctx.GetOutputs(),
                StepsCompleted = stepsCompleted,
                StepsTotal = task.Steps.Count,
                DurationMs = sw.ElapsedMilliseconds,
                LogFile = logFile,
                ScreenshotPath = lastScreenshot,
            };
        }
        catch (TaskAbortException ex)
        {
            sw.Stop();
            _log.LogWarning(ex, "Task \"{TaskName}\" aborted at step \"{Step}\"",
                task.Name, ctx.CurrentStepName);

            _logger.LogTaskComplete(task.Name, success: false, durationMs: sw.ElapsedMilliseconds);
            _logger.FlushLog();

            return BuildFailureResult(ctx, stepsCompleted, task.Steps.Count, sw.ElapsedMilliseconds, logFile, ex.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            _log.LogWarning("Task \"{TaskName}\" was cancelled", task.Name);

            _logger.LogTaskComplete(task.Name, success: false, durationMs: sw.ElapsedMilliseconds);
            _logger.FlushLog();

            return BuildFailureResult(ctx, stepsCompleted, task.Steps.Count, sw.ElapsedMilliseconds, logFile, "Task was cancelled.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _log.LogError(ex, "Task \"{TaskName}\" failed at step \"{Step}\"",
                task.Name, ctx.CurrentStepName);

            // Run the on-error handler if available
            if (task.OnError is not null)
            {
                try
                {
                    _log.LogInformation("Running on-error handler");
                    await ExecuteActionsAsync(task.OnError.Actions, ctx, CancellationToken.None);
                }
                catch (Exception handlerEx)
                {
                    _log.LogError(handlerEx, "On-error handler itself failed");
                }
            }

            _logger.LogTaskComplete(task.Name, success: false, durationMs: sw.ElapsedMilliseconds);
            _logger.FlushLog();

            return BuildFailureResult(ctx, stepsCompleted, task.Steps.Count, sw.ElapsedMilliseconds, logFile, ex.Message);
        }
        finally
        {
            CurrentTaskName = null;
            CurrentStepName = null;
        }
    }

    /// <summary>
    /// Executes a list of actions sequentially, handling conditional branching.
    /// </summary>
    private async Task ExecuteActionsAsync(
        List<IAction> actions,
        ExecutionContext ctx,
        CancellationToken ct)
    {
        foreach (var action in actions)
        {
            ct.ThrowIfCancellationRequested();
            await ExecuteActionAsync(action, ctx, ct);
        }
    }

    /// <summary>
    /// Dispatches a single action to the appropriate handler.
    /// </summary>
    private async Task ExecuteActionAsync(IAction action, ExecutionContext ctx, CancellationToken ct)
    {
        switch (action)
        {
            case ExpectAction expect:
                await ExpectHandler.ExecuteAsync(expect, ctx, _rdp, _claude, _tasksConfig, _logger, ct);
                break;

            case ClickAction click:
                await ClickHandler.ExecuteAsync(
                    click.Target, ClickHandler.ClickType.Single,
                    ctx, _rdp, _claude, _tasksConfig, _logger, ct);
                break;

            case DoubleClickAction doubleClick:
                await ClickHandler.ExecuteAsync(
                    doubleClick.Target, ClickHandler.ClickType.Double,
                    ctx, _rdp, _claude, _tasksConfig, _logger, ct);
                break;

            case RightClickAction rightClick:
                await ClickHandler.ExecuteAsync(
                    rightClick.Target, ClickHandler.ClickType.Right,
                    ctx, _rdp, _claude, _tasksConfig, _logger, ct);
                break;

            case TypeAction type:
                await TypeHandler.ExecuteAsync(type, ctx, _rdp, _claude, _tasksConfig, _logger, ct);
                break;

            case SelectAction select:
                await SelectHandler.ExecuteAsync(select, ctx, _rdp, _claude, _tasksConfig, _logger, ct);
                break;

            case KeyAction key:
                await KeyHandler.ExecuteAsync(key, ctx, _rdp, _tasksConfig, _logger, ct);
                break;

            case ExtractAction extract:
                await ExtractHandler.ExecuteAsync(extract, ctx, _rdp, _claude, _tasksConfig, _logger, ct);
                break;

            case OutputAction output:
                ExecuteOutputAction(output, ctx);
                break;

            case ScreenshotAction:
                await ExecuteScreenshotActionAsync(ctx, ct);
                break;

            case AbortAction abort:
                throw new TaskAbortException(abort.Message);

            case IfScreenShowsAction ifAction:
                var branchActions = await IfScreenShowsHandler.ExecuteAsync(
                    ifAction, ctx, _rdp, _claude, _tasksConfig, _logger, ct);
                await ExecuteActionsAsync(branchActions, ctx, ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown action type: {action.GetType().Name}");
        }
    }

    /// <summary>
    /// Registers a variable name as a declared output for the task.
    /// </summary>
    private void ExecuteOutputAction(OutputAction action, ExecutionContext ctx)
    {
        if (!ctx.DeclaredOutputs.Contains(action.VariableName))
        {
            ctx.DeclaredOutputs.Add(action.VariableName);
        }

        _logger.LogAction(
            task: ctx.TaskName,
            step: ctx.CurrentStepName ?? "",
            stepIndex: ctx.CurrentStepIndex,
            action: "output",
            detail: new { variable = action.VariableName },
            screenshot: null,
            requestTokens: null,
            responseTokens: null,
            claudeResponse: null,
            model: null,
            durationMs: 0);
    }

    /// <summary>
    /// Captures and saves a screenshot, logging the event.
    /// </summary>
    private async Task ExecuteScreenshotActionAsync(ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();
        var screenshot = await _rdp.CaptureScreenshotAsync();
        sw.Stop();

        var path = _logger.SaveScreenshot(screenshot, $"screenshot_{ctx.TaskName}");
        ctx.ScreenshotPaths.Add(path);

        _logger.LogAction(
            task: ctx.TaskName,
            step: ctx.CurrentStepName ?? "",
            stepIndex: ctx.CurrentStepIndex,
            action: "screenshot",
            detail: new { path },
            screenshot: path,
            requestTokens: null,
            responseTokens: null,
            claudeResponse: null,
            model: null,
            durationMs: sw.ElapsedMilliseconds);
    }

    private TaskExecutionResult BuildFailureResult(
        ExecutionContext ctx,
        int stepsCompleted,
        int stepsTotal,
        long durationMs,
        string logFile,
        string error)
    {
        var lastScreenshot = ctx.ScreenshotPaths.Count > 0 ? ctx.ScreenshotPaths[^1] : null;

        return new TaskExecutionResult
        {
            Success = false,
            Error = error,
            FailedAtStep = ctx.CurrentStepName,
            Outputs = ctx.GetOutputs(),
            StepsCompleted = stepsCompleted,
            StepsTotal = stepsTotal,
            DurationMs = durationMs,
            LogFile = logFile,
            ScreenshotPath = lastScreenshot,
        };
    }
}
