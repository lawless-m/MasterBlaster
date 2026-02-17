namespace MasterBlaster.Execution;

public class ExecutionContext
{
    public string TaskName { get; init; } = "";
    public Dictionary<string, string> Parameters { get; init; } = new();
    public Dictionary<string, string> ExtractedValues { get; } = new();
    public List<string> DeclaredOutputs { get; } = new();
    public string? CurrentStepName { get; set; }
    public int CurrentStepIndex { get; set; }
    public DateTime StartTime { get; } = DateTime.UtcNow;
    public List<string> ScreenshotPaths { get; } = new();
    public int TotalTokensUsed { get; set; }

    public Dictionary<string, string> GetOutputs()
    {
        var outputs = new Dictionary<string, string>();
        foreach (var name in DeclaredOutputs)
        {
            if (ExtractedValues.TryGetValue(name, out var value))
                outputs[name] = value;
        }
        return outputs;
    }

    public TimeSpan Elapsed => DateTime.UtcNow - StartTime;
}
