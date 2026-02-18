namespace MasterBlaster.Config;

using System.Text.Json;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppConfig Load(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Configuration file not found: {fullPath}", fullPath);

        var json = File.ReadAllText(fullPath);

        var config = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize configuration file; result was null.");

        Validate(config);

        return config;
    }

    private static void Validate(AppConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.Rdp.Server))
            errors.Add("rdp.server must be specified.");

        if (config.Rdp.Port is <= 0 or > 65535)
            errors.Add($"rdp.port must be between 1 and 65535, got {config.Rdp.Port}.");

        if (config.Rdp.Width <= 0 || config.Rdp.Height <= 0)
            errors.Add("rdp.width and rdp.height must be positive integers.");

        if (string.IsNullOrWhiteSpace(config.Tcp.Host))
            errors.Add("tcp.host must be specified.");

        if (config.Tcp.Port is <= 0 or > 65535)
            errors.Add($"tcp.port must be between 1 and 65535, got {config.Tcp.Port}.");

        if (string.IsNullOrWhiteSpace(config.Claude.Model))
            errors.Add("claude.model must be specified.");

        if (config.Claude.MaxTokensPerRequest <= 0)
            errors.Add("claude.max_tokens_per_request must be a positive integer.");

        if (config.Claude.TokenBudgetPerTask <= 0)
            errors.Add("claude.token_budget_per_task must be a positive integer.");

        if (string.IsNullOrWhiteSpace(config.Tasks.Directory))
            errors.Add("tasks.directory must be specified.");

        if (config.Tasks.DefaultExpectTimeoutSeconds <= 0)
            errors.Add("tasks.default_expect_timeout_seconds must be a positive integer.");

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configuration validation failed with {errors.Count} error(s):\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }

    public static string ResolveClaudeApiKey(ClaudeConfig claude)
    {
        return claude.ApiKey;
    }
}
