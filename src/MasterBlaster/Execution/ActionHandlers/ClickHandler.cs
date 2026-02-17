namespace MasterBlaster.Execution.ActionHandlers;

using System.Diagnostics;
using MasterBlaster.Claude;
using MasterBlaster.Config;
using MasterBlaster.Logging;
using MasterBlaster.Mbl;
using MasterBlaster.Rdp;

/// <summary>
/// Handles click, double-click, and right-click actions: captures a screenshot,
/// asks Claude for the target coordinates, then performs the appropriate click.
/// </summary>
public static class ClickHandler
{
    public enum ClickType { Single, Double, Right }

    public static async Task ExecuteAsync(
        string target,
        ClickType clickType,
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
        var prompt = PromptBuilder.BuildClickPrompt(target);
        var response = await claude.SendAsync(screenshot, prompt, ct);
        sw.Stop();

        ctx.TotalTokensUsed += response.InputTokens + response.OutputTokens;

        var screenshotPath = logger.SaveScreenshot(screenshot, $"click_{clickType.ToString().ToLowerInvariant()}");
        ctx.ScreenshotPaths.Add(screenshotPath);

        var actionName = clickType switch
        {
            ClickType.Double => "double-click",
            ClickType.Right => "right-click",
            _ => "click",
        };

        logger.LogAction(
            task: ctx.TaskName,
            step: ctx.CurrentStepName ?? "",
            stepIndex: ctx.CurrentStepIndex,
            action: actionName,
            detail: new { target },
            screenshot: screenshotPath,
            requestTokens: response.InputTokens,
            responseTokens: response.OutputTokens,
            claudeResponse: response.Text,
            model: response.Model,
            durationMs: sw.ElapsedMilliseconds);

        var (x, y) = ParseCoordinates(response.Text, target);

        switch (clickType)
        {
            case ClickType.Single:
                await rdp.ClickAsync(x, y);
                break;
            case ClickType.Double:
                await rdp.DoubleClickAsync(x, y);
                break;
            case ClickType.Right:
                await rdp.RightClickAsync(x, y);
                break;
        }

        await Task.Delay(tasksConfig.PostClickDelayMs, ct);
    }

    internal static (int X, int Y) ParseCoordinates(string text, string target)
    {
        var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim() ?? "";

        if (firstLine.StartsWith("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Claude could not find element \"{target}\": {firstLine}");
        }

        var parts = firstLine.Split(',');
        if (parts.Length == 2
            && int.TryParse(parts[0].Trim(), out var x)
            && int.TryParse(parts[1].Trim(), out var y))
        {
            return (x, y);
        }

        throw new InvalidOperationException(
            $"Failed to parse coordinates from Claude response for \"{target}\": \"{firstLine}\"");
    }
}
