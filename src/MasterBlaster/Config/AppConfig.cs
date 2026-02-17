namespace MasterBlaster.Config;

using System.Text.Json.Serialization;

public class AppConfig
{
    [JsonPropertyName("tcp")]
    public TcpConfig Tcp { get; set; } = new();

    [JsonPropertyName("rdp")]
    public RdpConfig Rdp { get; set; } = new();

    [JsonPropertyName("claude")]
    public ClaudeConfig Claude { get; set; } = new();

    [JsonPropertyName("tasks")]
    public TasksConfig Tasks { get; set; } = new();

    [JsonPropertyName("logging")]
    public LoggingConfig Logging { get; set; } = new();
}

public class TcpConfig
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 9500;
}

public class RdpConfig
{
    [JsonPropertyName("server")]
    public string Server { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 3389;

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password_env")]
    public string PasswordEnv { get; set; } = "MB_RDP_PASSWORD";

    [JsonPropertyName("domain")]
    public string Domain { get; set; } = "";

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1920;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1080;

    [JsonPropertyName("color_depth")]
    public int ColorDepth { get; set; } = 32;
}

public class ClaudeConfig
{
    [JsonPropertyName("api_key_env")]
    public string ApiKeyEnv { get; set; } = "ANTHROPIC_API_KEY";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "claude-sonnet-4-5-20250929";

    [JsonPropertyName("max_tokens_per_request")]
    public int MaxTokensPerRequest { get; set; } = 1024;

    [JsonPropertyName("token_budget_per_task")]
    public int TokenBudgetPerTask { get; set; } = 100000;

    [JsonPropertyName("retry_on_rate_limit")]
    public bool RetryOnRateLimit { get; set; } = true;

    [JsonPropertyName("rate_limit_backoff_ms")]
    public int RateLimitBackoffMs { get; set; } = 5000;
}

public class TasksConfig
{
    [JsonPropertyName("directory")]
    public string Directory { get; set; } = "./tasks";

    [JsonPropertyName("default_expect_timeout_seconds")]
    public int DefaultExpectTimeoutSeconds { get; set; } = 10;

    [JsonPropertyName("expect_retry_intervals_ms")]
    public int[] ExpectRetryIntervalsMs { get; set; } = [1000, 2000, 4000];

    [JsonPropertyName("post_action_delay_ms")]
    public int PostActionDelayMs { get; set; } = 500;

    [JsonPropertyName("post_click_delay_ms")]
    public int PostClickDelayMs { get; set; } = 500;

    [JsonPropertyName("typing_delay_ms")]
    public int TypingDelayMs { get; set; } = 50;
}

public class LoggingConfig
{
    [JsonPropertyName("directory")]
    public string Directory { get; set; } = "./logs";

    [JsonPropertyName("screenshot_directory")]
    public string ScreenshotDirectory { get; set; } = "./screenshots";

    [JsonPropertyName("retention_days")]
    public int RetentionDays { get; set; } = 30;

    [JsonPropertyName("log_level")]
    public string LogLevel { get; set; } = "info";
}
