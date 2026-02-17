namespace MasterBlaster.Execution.ActionHandlers;

using System.Diagnostics;
using MasterBlaster.Claude;
using MasterBlaster.Config;
using MasterBlaster.Logging;
using MasterBlaster.Mbl;
using MasterBlaster.Rdp;

/// <summary>
/// Handles the "expect" action: captures a screenshot, asks Claude whether the
/// screen matches the expected description, and retries with configured intervals
/// until a match is found or retries are exhausted.
/// </summary>
public static class ExpectHandler
{
    public static async Task ExecuteAsync(
        ExpectAction action,
        ExecutionContext ctx,
        IRdpController rdp,
        IClaudeClient claude,
        TasksConfig tasksConfig,
        TaskLogger logger,
        CancellationToken ct)
    {
        var intervals = tasksConfig.ExpectRetryIntervalsMs;
        var totalTimeout = (ctx.CurrentStepIndex >= 0 ? null : (int?)null)
            ?? tasksConfig.DefaultExpectTimeoutSeconds;

        // We attempt once, then retry for each configured interval
        var maxAttempts = 1 + intervals.Length;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var sw = Stopwatch.StartNew();
            var screenshot = await rdp.CaptureScreenshotAsync();
            var prompt = PromptBuilder.BuildExpectPrompt(action.Description);
            var response = await claude.SendAsync(screenshot, prompt, ct);
            sw.Stop();

            ctx.TotalTokensUsed += response.InputTokens + response.OutputTokens;

            var screenshotPath = logger.SaveScreenshot(screenshot, $"expect_{attempt}");
            ctx.ScreenshotPaths.Add(screenshotPath);

            logger.LogAction(
                task: ctx.TaskName,
                step: ctx.CurrentStepName ?? "",
                stepIndex: ctx.CurrentStepIndex,
                action: "expect",
                detail: new { description = action.Description, attempt },
                screenshot: screenshotPath,
                requestTokens: response.InputTokens,
                responseTokens: response.OutputTokens,
                claudeResponse: response.Text,
                model: response.Model,
                durationMs: sw.ElapsedMilliseconds);

            var result = ParseExpectResult(response.Text);

            if (result == ExpectResult.Match)
                return;

            if (result == ExpectResult.Uncertain)
            {
                // Treat uncertain as a soft mismatch; retry if we still can.
            }

            // If this was the last attempt, the expect has failed
            if (attempt >= maxAttempts - 1)
            {
                throw new TimeoutException(
                    $"Expect failed after {maxAttempts} attempts: \"{action.Description}\". " +
                    $"Claude responded: {response.Text}");
            }

            // Wait the configured interval before retrying
            var delayMs = intervals[attempt];
            await Task.Delay(delayMs, ct);
        }
    }

    private enum ExpectResult { Match, NoMatch, Uncertain }

    private static ExpectResult ParseExpectResult(string text)
    {
        var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim().ToUpperInvariant() ?? "";

        if (firstLine.StartsWith("MATCH"))
            return ExpectResult.Match;
        if (firstLine.StartsWith("NO_MATCH"))
            return ExpectResult.NoMatch;
        if (firstLine.StartsWith("UNCERTAIN"))
            return ExpectResult.Uncertain;

        // If the response doesn't follow the protocol, treat as uncertain
        return ExpectResult.Uncertain;
    }
}
