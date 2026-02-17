namespace MasterBlaster.Claude;

public interface IClaudeClient
{
    /// <summary>
    /// Send a screenshot with a prompt and get a text response.
    /// </summary>
    Task<ClaudeResponse> SendAsync(byte[] screenshotPng, string prompt, CancellationToken ct = default);
}

public record ClaudeResponse
{
    public string Text { get; init; } = "";
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public string Model { get; init; } = "";
    public TimeSpan Duration { get; init; }
}
