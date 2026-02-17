namespace MasterBlaster.Execution.ActionHandlers;

using System.Diagnostics;
using MasterBlaster.Claude;
using MasterBlaster.Config;
using MasterBlaster.Logging;
using MasterBlaster.Mbl;
using MasterBlaster.Rdp;

/// <summary>
/// Handles the "if screen shows" conditional action: captures a screenshot,
/// asks Claude a YES/NO question, then executes the appropriate branch.
/// </summary>
public static class IfScreenShowsHandler
{
    public static async Task<List<IAction>> ExecuteAsync(
        IfScreenShowsAction action,
        ExecutionContext ctx,
        IRdpController rdp,
        IClaudeClient claude,
        TasksConfig tasksConfig,
        TaskLogger logger,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();
        var screenshot = await rdp.CaptureScreenshotAsync();
        var prompt = PromptBuilder.BuildIfScreenShowsPrompt(action.Condition);
        var response = await claude.SendAsync(screenshot, prompt, ct);
        sw.Stop();

        ctx.TotalTokensUsed += response.InputTokens + response.OutputTokens;

        var screenshotPath = logger.SaveScreenshot(screenshot, "if_screen_shows");
        ctx.ScreenshotPaths.Add(screenshotPath);

        var conditionResult = response.Text.Trim().ToUpperInvariant().StartsWith("YES");

        logger.LogAction(
            task: ctx.TaskName,
            step: ctx.CurrentStepName ?? "",
            stepIndex: ctx.CurrentStepIndex,
            action: "if_screen_shows",
            detail: new { condition = action.Condition, result = conditionResult ? "YES" : "NO" },
            screenshot: screenshotPath,
            requestTokens: response.InputTokens,
            responseTokens: response.OutputTokens,
            claudeResponse: response.Text,
            model: response.Model,
            durationMs: sw.ElapsedMilliseconds);

        // Return the actions from the appropriate branch for the caller to execute
        if (conditionResult)
        {
            return action.Then;
        }

        return action.Else ?? new List<IAction>();
    }
}
