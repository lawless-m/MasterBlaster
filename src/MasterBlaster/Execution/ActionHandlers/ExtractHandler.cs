namespace MasterBlaster.Execution.ActionHandlers;

using System.Diagnostics;
using MasterBlaster.Claude;
using MasterBlaster.Config;
using MasterBlaster.Logging;
using MasterBlaster.Mbl;
using MasterBlaster.Rdp;

/// <summary>
/// Handles the "extract" action: captures a screenshot, asks Claude to read
/// a value from a specified UI element, and stores it in the execution context.
/// </summary>
public static class ExtractHandler
{
    public static async Task ExecuteAsync(
        ExtractAction action,
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
        var prompt = PromptBuilder.BuildExtractPrompt(action.Source);
        var response = await claude.SendAsync(screenshot, prompt, ct);
        sw.Stop();

        ctx.TotalTokensUsed += response.InputTokens + response.OutputTokens;

        var screenshotPath = logger.SaveScreenshot(screenshot, "extract");
        ctx.ScreenshotPaths.Add(screenshotPath);

        logger.LogAction(
            task: ctx.TaskName,
            step: ctx.CurrentStepName ?? "",
            stepIndex: ctx.CurrentStepIndex,
            action: "extract",
            detail: new { variable = action.VariableName, source = action.Source },
            screenshot: screenshotPath,
            requestTokens: response.InputTokens,
            responseTokens: response.OutputTokens,
            claudeResponse: response.Text,
            model: response.Model,
            durationMs: sw.ElapsedMilliseconds);

        var extractedValue = response.Text.Trim();

        if (extractedValue.Equals("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Claude could not find the field \"{action.Source}\" to extract variable \"{action.VariableName}\".");
        }

        if (extractedValue.Equals("EMPTY", StringComparison.OrdinalIgnoreCase))
        {
            extractedValue = "";
        }

        ctx.ExtractedValues[action.VariableName] = extractedValue;
    }
}
