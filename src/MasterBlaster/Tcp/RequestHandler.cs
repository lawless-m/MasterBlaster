namespace MasterBlaster.Tcp;

using MasterBlaster.Config;
using MasterBlaster.Execution;
using MasterBlaster.Logging;
using MasterBlaster.Mbl;
using MasterBlaster.Rdp;
using Microsoft.Extensions.Logging;

/// <summary>
/// Routes incoming TCP requests to the appropriate logic: run a task, query
/// status, list available tasks, capture a screenshot, reconnect RDP, or
/// trigger a graceful shutdown.
/// </summary>
public class RequestHandler
{
    private readonly TaskExecutor _executor;
    private readonly IRdpController _rdp;
    private readonly AppConfig _config;
    private readonly TaskLogger _taskLogger;
    private readonly ILogger<RequestHandler> _log;

    /// <summary>
    /// Raised when a "shutdown" request is received. The TCP server should
    /// monitor this to stop its listener loop.
    /// </summary>
    private readonly CancellationTokenSource _shutdownCts = new();
    public CancellationToken ShutdownToken => _shutdownCts.Token;

    public RequestHandler(
        TaskExecutor executor,
        IRdpController rdp,
        AppConfig config,
        TaskLogger taskLogger,
        ILogger<RequestHandler> log)
    {
        _executor = executor;
        _rdp = rdp;
        _config = config;
        _taskLogger = taskLogger;
        _log = log;
    }

    public async Task<TaskResponse> HandleAsync(TaskRequest request, CancellationToken ct)
    {
        _log.LogInformation("Handling request: action={Action}, task={Task}",
            request.Action, request.Task);

        return request.Action.ToLowerInvariant() switch
        {
            "run" => await HandleRunAsync(request, ct),
            "status" => HandleStatus(),
            "list_tasks" => HandleListTasks(),
            "screenshot" => await HandleScreenshotAsync(ct),
            "reconnect" => await HandleReconnectAsync(ct),
            "shutdown" => HandleShutdown(),
            _ => new TaskResponse
            {
                Status = "error",
                Error = $"Unknown action: \"{request.Action}\". " +
                        "Valid actions: run, status, list_tasks, screenshot, reconnect, shutdown.",
            },
        };
    }

    /// <summary>
    /// Loads the specified .mbl task file, parses it, validates parameters,
    /// and executes the task. Returns the result.
    /// </summary>
    private async Task<TaskResponse> HandleRunAsync(TaskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Task))
        {
            return new TaskResponse
            {
                Status = "error",
                Error = "The \"task\" field is required for the \"run\" action.",
            };
        }

        if (_executor.IsRunning)
        {
            return new TaskResponse
            {
                Status = "error",
                Error = $"A task is already running: \"{_executor.CurrentTaskName}\". " +
                        "Wait for it to complete or cancel it first.",
            };
        }

        // Locate the .mbl file
        var taskDir = Path.GetFullPath(_config.Tasks.Directory);
        var taskFileName = request.Task.EndsWith(".mbl", StringComparison.OrdinalIgnoreCase)
            ? request.Task
            : request.Task + ".mbl";
        var taskFilePath = Path.Combine(taskDir, taskFileName);

        if (!File.Exists(taskFilePath))
        {
            return new TaskResponse
            {
                Status = "error",
                Error = $"Task file not found: \"{taskFilePath}\".",
            };
        }

        // Parse the MBL file
        TaskDefinition taskDef;
        try
        {
            var source = await File.ReadAllTextAsync(taskFilePath, ct);
            var lexer = new Lexer();
            var tokens = lexer.Tokenize(source);
            var parser = new Parser();
            taskDef = parser.Parse(tokens, taskFileName);
        }
        catch (MblParseException ex)
        {
            _log.LogError(ex, "Failed to parse task file {File}", taskFilePath);
            return new TaskResponse
            {
                Status = "error",
                Error = $"Failed to parse task file: {ex.Message}",
            };
        }

        // Execute the task
        var parameters = request.Params ?? new Dictionary<string, string>();
        var result = await _executor.ExecuteAsync(taskDef, parameters, ct);

        return new TaskResponse
        {
            Status = result.Success ? "ok" : "error",
            Task = taskDef.Name,
            Outputs = result.Outputs.Count > 0 ? result.Outputs : null,
            Error = result.Error,
            FailedAtStep = result.FailedAtStep,
            Screenshot = result.ScreenshotPath,
            DurationMs = result.DurationMs,
            StepsCompleted = result.StepsCompleted,
            StepsTotal = result.StepsTotal,
            LogFile = result.LogFile,
        };
    }

    /// <summary>
    /// Returns the current execution state: idle or running, with optional
    /// step and RDP connection info.
    /// </summary>
    private TaskResponse HandleStatus()
    {
        return new TaskResponse
        {
            Status = "ok",
            State = _executor.IsRunning ? "running" : "idle",
            CurrentTask = _executor.CurrentTaskName,
            CurrentStep = _executor.CurrentStepName,
            RdpConnected = _rdp.IsConnected,
        };
    }

    /// <summary>
    /// Scans the configured tasks directory for .mbl files and returns their names.
    /// </summary>
    private TaskResponse HandleListTasks()
    {
        var taskDir = Path.GetFullPath(_config.Tasks.Directory);

        if (!Directory.Exists(taskDir))
        {
            return new TaskResponse
            {
                Status = "ok",
                Tasks = new List<string>(),
            };
        }

        var files = Directory.GetFiles(taskDir, "*.mbl")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TaskResponse
        {
            Status = "ok",
            Tasks = files,
        };
    }

    /// <summary>
    /// Captures a screenshot of the current RDP session, saves it, and returns
    /// the file path in the response.
    /// </summary>
    private async Task<TaskResponse> HandleScreenshotAsync(CancellationToken ct)
    {
        if (!_rdp.IsConnected)
        {
            return new TaskResponse
            {
                Status = "error",
                Error = "RDP is not connected. Use \"reconnect\" first.",
            };
        }

        try
        {
            var pngData = await _rdp.CaptureScreenshotAsync();
            var path = _taskLogger.SaveScreenshot(pngData, "manual");

            return new TaskResponse
            {
                Status = "ok",
                Screenshot = path,
            };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to capture screenshot");
            return new TaskResponse
            {
                Status = "error",
                Error = $"Failed to capture screenshot: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Disconnects and reconnects the RDP session.
    /// </summary>
    private async Task<TaskResponse> HandleReconnectAsync(CancellationToken ct)
    {
        try
        {
            if (_rdp.IsConnected)
            {
                _log.LogInformation("Disconnecting existing RDP session for reconnect");
                await _rdp.DisconnectAsync();
            }

            _log.LogInformation("Reconnecting RDP");
            var connectionConfig = new RdpConnectionConfig
            {
                Server = _config.Rdp.Server,
                Port = _config.Rdp.Port,
                Username = _config.Rdp.Username,
                Password = Environment.GetEnvironmentVariable(_config.Rdp.PasswordEnv) ?? "",
                Domain = _config.Rdp.Domain,
                Width = _config.Rdp.Width,
                Height = _config.Rdp.Height,
                ColorDepth = _config.Rdp.ColorDepth,
            };

            await _rdp.ConnectAsync(connectionConfig, ct);

            return new TaskResponse
            {
                Status = "ok",
                RdpConnected = true,
            };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to reconnect RDP");
            return new TaskResponse
            {
                Status = "error",
                Error = $"Failed to reconnect RDP: {ex.Message}",
                RdpConnected = false,
            };
        }
    }

    /// <summary>
    /// Signals a graceful shutdown. The TCP server should detect this and exit.
    /// </summary>
    private TaskResponse HandleShutdown()
    {
        _log.LogInformation("Shutdown requested via TCP");
        _shutdownCts.Cancel();

        return new TaskResponse
        {
            Status = "ok",
            State = "shutting_down",
        };
    }
}
