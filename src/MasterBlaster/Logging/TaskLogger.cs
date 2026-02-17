namespace MasterBlaster.Logging;

using System.Text.Json;
using System.Text.Json.Serialization;
using MasterBlaster.Config;
using Microsoft.Extensions.Logging;

public class TaskLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly LoggingConfig _config;
    private readonly ILogger _logger;
    private string? _currentLogFile;
    private readonly List<object> _entries = new();
    private readonly ScreenshotManager _screenshotManager;

    public TaskLogger(LoggingConfig config, ILogger<TaskLogger> logger)
    {
        _config = config;
        _logger = logger;
        _screenshotManager = new ScreenshotManager(config);
    }

    /// <summary>
    /// Creates a new log file for the given task and returns its path.
    /// </summary>
    public string StartTaskLog(string taskName)
    {
        var dir = Path.GetFullPath(_config.Directory);
        Directory.CreateDirectory(dir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var safeName = SanitizeFileName(taskName);
        var fileName = $"{safeName}_{timestamp}.json";
        _currentLogFile = Path.Combine(dir, fileName);

        _entries.Clear();

        _logger.LogInformation("Task log started: {LogFile}", _currentLogFile);
        return _currentLogFile;
    }

    /// <summary>
    /// Adds a structured entry to the in-memory log.
    /// </summary>
    public void LogEntry(object entry)
    {
        _entries.Add(entry);
    }

    /// <summary>
    /// Logs a structured action entry including Claude interaction details.
    /// </summary>
    public void LogAction(
        string task,
        string step,
        int stepIndex,
        string action,
        object? detail,
        string? screenshot,
        int? requestTokens,
        int? responseTokens,
        string? claudeResponse,
        string? model,
        long durationMs)
    {
        var entry = new Dictionary<string, object?>
        {
            ["type"] = "action",
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["task"] = task,
            ["step"] = step,
            ["step_index"] = stepIndex,
            ["action"] = action,
            ["detail"] = detail,
            ["screenshot"] = screenshot,
            ["request_tokens"] = requestTokens,
            ["response_tokens"] = responseTokens,
            ["claude_response"] = claudeResponse,
            ["model"] = model,
            ["duration_ms"] = durationMs,
        };

        _entries.Add(entry);
        _logger.LogDebug(
            "[{Task}] Step {StepIndex} ({Step}): {Action} completed in {Duration}ms",
            task, stepIndex, step, action, durationMs);
    }

    /// <summary>
    /// Logs when a step begins execution.
    /// </summary>
    public void LogStepStart(string task, string step, int stepIndex)
    {
        var entry = new Dictionary<string, object?>
        {
            ["type"] = "step_start",
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["task"] = task,
            ["step"] = step,
            ["step_index"] = stepIndex,
        };

        _entries.Add(entry);
        _logger.LogInformation("[{Task}] Step {StepIndex}: \"{Step}\" started", task, stepIndex, step);
    }

    /// <summary>
    /// Logs when a step finishes execution.
    /// </summary>
    public void LogStepComplete(string task, string step, int stepIndex)
    {
        var entry = new Dictionary<string, object?>
        {
            ["type"] = "step_complete",
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["task"] = task,
            ["step"] = step,
            ["step_index"] = stepIndex,
        };

        _entries.Add(entry);
        _logger.LogInformation("[{Task}] Step {StepIndex}: \"{Step}\" completed", task, stepIndex, step);
    }

    /// <summary>
    /// Logs when a task begins execution.
    /// </summary>
    public void LogTaskStart(string task)
    {
        var entry = new Dictionary<string, object?>
        {
            ["type"] = "task_start",
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["task"] = task,
        };

        _entries.Add(entry);
        _logger.LogInformation("Task \"{Task}\" started", task);
    }

    /// <summary>
    /// Logs when a task finishes execution (success or failure).
    /// </summary>
    public void LogTaskComplete(string task, bool success, long durationMs)
    {
        var entry = new Dictionary<string, object?>
        {
            ["type"] = "task_complete",
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["task"] = task,
            ["success"] = success,
            ["duration_ms"] = durationMs,
        };

        _entries.Add(entry);
        _logger.LogInformation(
            "Task \"{Task}\" completed. Success={Success}, Duration={Duration}ms",
            task, success, durationMs);
    }

    /// <summary>
    /// Saves a screenshot via the ScreenshotManager and returns the file path.
    /// </summary>
    public string SaveScreenshot(byte[] pngData, string prefix)
    {
        return _screenshotManager.SaveScreenshot(pngData, prefix);
    }

    /// <summary>
    /// Flushes all in-memory log entries to the current log file as a JSON array.
    /// </summary>
    public void FlushLog()
    {
        if (_currentLogFile is null)
            return;

        try
        {
            var json = JsonSerializer.Serialize(_entries, JsonOptions);
            File.WriteAllText(_currentLogFile, json);
            _logger.LogDebug("Log flushed to {LogFile} ({Count} entries)", _currentLogFile, _entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush log to {LogFile}", _currentLogFile);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            sanitized[i] = Array.IndexOf(invalid, name[i]) >= 0 ? '_' : name[i];
        }
        return new string(sanitized).Replace(' ', '_');
    }
}
