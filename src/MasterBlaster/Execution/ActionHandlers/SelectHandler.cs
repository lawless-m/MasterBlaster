namespace MasterBlaster.Execution.ActionHandlers;

using System.Diagnostics;
using MasterBlaster.Claude;
using MasterBlaster.Config;
using MasterBlaster.Logging;
using MasterBlaster.Mbl;
using MasterBlaster.Rdp;

/// <summary>
/// Handles the "select" action in two phases:
///   1. Finds and clicks the dropdown to open it.
///   2. Captures a fresh screenshot, finds and clicks the desired option.
/// </summary>
public static class SelectHandler
{
    public static async Task ExecuteAsync(
        SelectAction action,
        ExecutionContext ctx,
        IRdpController rdp,
        IClaudeClient claude,
        TasksConfig tasksConfig,
        TaskLogger logger,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Resolve the value — either literal or from parameters
        var valueToSelect = action.IsParam
            ? ResolveParam(action.Value, ctx)
            : action.Value;

        // --- Phase 1: find and click the dropdown ---
        var sw = Stopwatch.StartNew();
        var screenshot = await rdp.CaptureScreenshotAsync();
        var dropdownPrompt = PromptBuilder.BuildSelectDropdownPrompt(action.Target);
        var dropdownResponse = await claude.SendAsync(screenshot, dropdownPrompt, ct);
        sw.Stop();

        ctx.TotalTokensUsed += dropdownResponse.InputTokens + dropdownResponse.OutputTokens;

        var screenshotPath1 = logger.SaveScreenshot(screenshot, "select_dropdown");
        ctx.ScreenshotPaths.Add(screenshotPath1);

        logger.LogAction(
            task: ctx.TaskName,
            step: ctx.CurrentStepName ?? "",
            stepIndex: ctx.CurrentStepIndex,
            action: "select_open_dropdown",
            detail: new { target = action.Target, value = valueToSelect },
            screenshot: screenshotPath1,
            requestTokens: dropdownResponse.InputTokens,
            responseTokens: dropdownResponse.OutputTokens,
            claudeResponse: dropdownResponse.Text,
            model: dropdownResponse.Model,
            durationMs: sw.ElapsedMilliseconds);

        var (dx, dy) = ClickHandler.ParseCoordinates(dropdownResponse.Text, action.Target);
        await rdp.ClickAsync(dx, dy);

        // Wait for the dropdown to open
        await Task.Delay(tasksConfig.PostClickDelayMs + 300, ct);

        // --- Phase 2: find and click the option ---
        var sw2 = Stopwatch.StartNew();
        var screenshot2 = await rdp.CaptureScreenshotAsync();
        var optionPrompt = PromptBuilder.BuildSelectOptionPrompt(valueToSelect);
        var optionResponse = await claude.SendAsync(screenshot2, optionPrompt, ct);
        sw2.Stop();

        ctx.TotalTokensUsed += optionResponse.InputTokens + optionResponse.OutputTokens;

        var screenshotPath2 = logger.SaveScreenshot(screenshot2, "select_option");
        ctx.ScreenshotPaths.Add(screenshotPath2);

        logger.LogAction(
            task: ctx.TaskName,
            step: ctx.CurrentStepName ?? "",
            stepIndex: ctx.CurrentStepIndex,
            action: "select_click_option",
            detail: new { target = action.Target, value = valueToSelect },
            screenshot: screenshotPath2,
            requestTokens: optionResponse.InputTokens,
            responseTokens: optionResponse.OutputTokens,
            claudeResponse: optionResponse.Text,
            model: optionResponse.Model,
            durationMs: sw2.ElapsedMilliseconds);

        var (ox, oy) = ClickHandler.ParseCoordinates(optionResponse.Text, valueToSelect);
        await rdp.ClickAsync(ox, oy);

        await Task.Delay(tasksConfig.PostClickDelayMs, ct);
    }

    private static string ResolveParam(string paramName, ExecutionContext ctx)
    {
        if (ctx.Parameters.TryGetValue(paramName, out var value))
            return value;

        if (ctx.ExtractedValues.TryGetValue(paramName, out var extracted))
            return extracted;

        throw new InvalidOperationException(
            $"Parameter \"{paramName}\" was not provided and has not been extracted.");
    }
}
