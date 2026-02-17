namespace MasterBlaster.Execution.ActionHandlers;

using System.Diagnostics;
using MasterBlaster.Claude;
using MasterBlaster.Config;
using MasterBlaster.Logging;
using MasterBlaster.Mbl;
using MasterBlaster.Rdp;

/// <summary>
/// Handles the "type" action: finds the target field via Claude, clicks to focus,
/// optionally clears the existing value, then types the new text.
/// </summary>
public static class TypeHandler
{
    public static async Task ExecuteAsync(
        TypeAction action,
        ExecutionContext ctx,
        IRdpController rdp,
        IClaudeClient claude,
        TasksConfig tasksConfig,
        TaskLogger logger,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Resolve the value — either literal or from parameters
        var textToType = action.IsParam
            ? ResolveParam(action.Value, ctx)
            : action.Value;

        var sw = Stopwatch.StartNew();
        var screenshot = await rdp.CaptureScreenshotAsync();
        var prompt = PromptBuilder.BuildTypeFieldPrompt(action.Target);
        var response = await claude.SendAsync(screenshot, prompt, ct);
        sw.Stop();

        ctx.TotalTokensUsed += response.InputTokens + response.OutputTokens;

        var screenshotPath = logger.SaveScreenshot(screenshot, "type");
        ctx.ScreenshotPaths.Add(screenshotPath);

        logger.LogAction(
            task: ctx.TaskName,
            step: ctx.CurrentStepName ?? "",
            stepIndex: ctx.CurrentStepIndex,
            action: "type",
            detail: new { target = action.Target, value = textToType, append = action.Append },
            screenshot: screenshotPath,
            requestTokens: response.InputTokens,
            responseTokens: response.OutputTokens,
            claudeResponse: response.Text,
            model: response.Model,
            durationMs: sw.ElapsedMilliseconds);

        var (x, y) = ClickHandler.ParseCoordinates(response.Text, action.Target);

        // Click to focus the field
        await rdp.ClickAsync(x, y);
        await Task.Delay(tasksConfig.PostClickDelayMs, ct);

        // Clear existing content unless we are appending
        if (!action.Append)
        {
            await rdp.SendKeyComboAsync("Ctrl+A");
            await Task.Delay(100, ct);
            await rdp.SendKeyComboAsync("Delete");
            await Task.Delay(100, ct);
        }

        // Type the text
        await rdp.SendKeysAsync(textToType);
        await Task.Delay(tasksConfig.PostActionDelayMs, ct);
    }

    private static string ResolveParam(string paramName, ExecutionContext ctx)
    {
        if (ctx.Parameters.TryGetValue(paramName, out var value))
            return value;

        // Also check extracted values as a fallback
        if (ctx.ExtractedValues.TryGetValue(paramName, out var extracted))
            return extracted;

        throw new InvalidOperationException(
            $"Parameter \"{paramName}\" was not provided and has not been extracted.");
    }
}
