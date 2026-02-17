namespace MasterBlaster.Mbl;

public record TaskDefinition
{
    public string Name { get; init; } = "";
    public string FileName { get; init; } = "";
    public List<string> Inputs { get; init; } = new();
    public List<Step> Steps { get; init; } = new();
    public ErrorHandler? OnTimeout { get; init; }
    public ErrorHandler? OnError { get; init; }
}

public record Step
{
    public string Description { get; init; } = "";
    public int? TimeoutSeconds { get; init; }
    public List<IAction> Actions { get; init; } = new();
}

public record ErrorHandler
{
    public List<IAction> Actions { get; init; } = new();
}
