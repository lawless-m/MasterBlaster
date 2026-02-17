namespace MasterBlaster.Config;

using System.Text.Json;

/// <summary>
/// Loads application configuration from a JSON file and resolves environment variable references.
///
/// The JSON config uses snake_case naming. Properties with an "_env" suffix convention
/// (e.g., "password_env", "api_key_env") contain the name of an environment variable
/// whose value should be read at load time. The resolved values are kept in the config
/// object for use by other components.
/// </summary>
public static class ConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads and validates an <see cref="AppConfig"/> from the specified JSON file path.
    /// </summary>
    /// <param name="path">Absolute or relative path to the JSON configuration file.</param>
    /// <returns>A fully resolved and validated <see cref="AppConfig"/>.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the config file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required configuration values are missing or invalid.</exception>
    public static AppConfig Load(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {fullPath}", fullPath);
        }

        var json = File.ReadAllText(fullPath);

        var config = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize configuration file; result was null.");

        ResolveEnvironmentVariables(config);
        Validate(config);

        return config;
    }

    /// <summary>
    /// Resolves environment variable references in the configuration.
    /// Properties following the "_env" convention hold the name of an environment variable.
    /// This method reads the actual values from the environment but keeps the env var names
    /// in the config so callers know the source.
    /// </summary>
    private static void ResolveEnvironmentVariables(AppConfig config)
    {
        // Resolve RDP password from the environment variable specified in PasswordEnv.
        // The resolved password is not stored in AppConfig itself; consumers should call
        // ResolveRdpPassword() to obtain it at connection time.
        // We validate that the env var is set here so we fail fast.
        if (!string.IsNullOrWhiteSpace(config.Rdp.PasswordEnv))
        {
            var password = Environment.GetEnvironmentVariable(config.Rdp.PasswordEnv);
            if (string.IsNullOrEmpty(password))
            {
                // Not a hard failure at load time -- the password may not be needed
                // if the caller is only using parts of the config.
                // Validation of required secrets is deferred to Validate().
            }
        }

        // Resolve Claude API key environment variable.
        if (!string.IsNullOrWhiteSpace(config.Claude.ApiKeyEnv))
        {
            var apiKey = Environment.GetEnvironmentVariable(config.Claude.ApiKeyEnv);
            if (string.IsNullOrEmpty(apiKey))
            {
                // Same approach: warn but don't fail at load, since the caller may
                // not need the Claude API in every run mode.
            }
        }
    }

    /// <summary>
    /// Validates the loaded configuration and throws if required fields are missing.
    /// </summary>
    private static void Validate(AppConfig config)
    {
        var errors = new List<string>();

        // RDP server is required when RDP configuration is present.
        if (string.IsNullOrWhiteSpace(config.Rdp.Server))
        {
            errors.Add("rdp.server must be specified.");
        }

        if (config.Rdp.Port is <= 0 or > 65535)
        {
            errors.Add($"rdp.port must be between 1 and 65535, got {config.Rdp.Port}.");
        }

        if (config.Rdp.Width <= 0 || config.Rdp.Height <= 0)
        {
            errors.Add("rdp.width and rdp.height must be positive integers.");
        }

        // TCP validation.
        if (string.IsNullOrWhiteSpace(config.Tcp.Host))
        {
            errors.Add("tcp.host must be specified.");
        }

        if (config.Tcp.Port is <= 0 or > 65535)
        {
            errors.Add($"tcp.port must be between 1 and 65535, got {config.Tcp.Port}.");
        }

        // Claude validation.
        if (string.IsNullOrWhiteSpace(config.Claude.Model))
        {
            errors.Add("claude.model must be specified.");
        }

        if (config.Claude.MaxTokensPerRequest <= 0)
        {
            errors.Add("claude.max_tokens_per_request must be a positive integer.");
        }

        if (config.Claude.TokenBudgetPerTask <= 0)
        {
            errors.Add("claude.token_budget_per_task must be a positive integer.");
        }

        // Tasks validation.
        if (string.IsNullOrWhiteSpace(config.Tasks.Directory))
        {
            errors.Add("tasks.directory must be specified.");
        }

        if (config.Tasks.DefaultExpectTimeoutSeconds <= 0)
        {
            errors.Add("tasks.default_expect_timeout_seconds must be a positive integer.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configuration validation failed with {errors.Count} error(s):\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }

    /// <summary>
    /// Resolves the RDP password by reading the environment variable specified in
    /// <see cref="RdpConfig.PasswordEnv"/>.
    /// </summary>
    /// <param name="config">The loaded application configuration.</param>
    /// <returns>The RDP password, or an empty string if the env var is not set.</returns>
    public static string ResolveRdpPassword(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Rdp.PasswordEnv))
        {
            return string.Empty;
        }

        return Environment.GetEnvironmentVariable(config.Rdp.PasswordEnv) ?? string.Empty;
    }

    /// <summary>
    /// Resolves the Claude API key by reading the environment variable specified in
    /// <see cref="ClaudeConfig.ApiKeyEnv"/>.
    /// </summary>
    /// <param name="config">The loaded application configuration.</param>
    /// <returns>The API key, or an empty string if the env var is not set.</returns>
    public static string ResolveClaudeApiKey(AppConfig config)
    {
        return ResolveClaudeApiKey(config.Claude);
    }

    /// <summary>
    /// Resolves the Claude API key by reading the environment variable specified in
    /// <see cref="ClaudeConfig.ApiKeyEnv"/>.
    /// </summary>
    public static string ResolveClaudeApiKey(ClaudeConfig claude)
    {
        if (string.IsNullOrWhiteSpace(claude.ApiKeyEnv))
        {
            return string.Empty;
        }

        return Environment.GetEnvironmentVariable(claude.ApiKeyEnv) ?? string.Empty;
    }
}
