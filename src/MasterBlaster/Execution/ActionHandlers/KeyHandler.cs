namespace MasterBlaster.Execution.ActionHandlers;

using System.Diagnostics;
using MasterBlaster.Config;
using MasterBlaster.Logging;
using MasterBlaster.Mbl;
using MasterBlaster.Rdp;

/// <summary>
/// Handles the "key" action: sends a key combination directly via RDP
/// without requiring a screenshot or Claude interaction.
/// </summary>
public static class KeyHandler
{
    public static async Task ExecuteAsync(
        KeyAction action,
        ExecutionContext ctx,
        IRdpController rdp,
        TasksConfig tasksConfig,
        TaskLogger logger,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();
        await rdp.SendKeyComboAsync(action.KeyCombo);
        sw.Stop();

        logger.LogAction(
            task: ctx.TaskName,
            step: ctx.CurrentStepName ?? "",
            stepIndex: ctx.CurrentStepIndex,
            action: "key",
            detail: new { combo = action.KeyCombo },
            screenshot: null,
            requestTokens: null,
            responseTokens: null,
            claudeResponse: null,
            model: null,
            durationMs: sw.ElapsedMilliseconds);

        await Task.Delay(tasksConfig.PostActionDelayMs, ct);
    }
}
