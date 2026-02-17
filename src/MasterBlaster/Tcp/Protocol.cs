namespace MasterBlaster.Tcp;

using System.Text.Json.Serialization;

public class TaskRequest
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("task")]
    public string? Task { get; set; }

    [JsonPropertyName("params")]
    public Dictionary<string, string>? Params { get; set; }
}

public class TaskResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("task")]
    public string? Task { get; set; }

    [JsonPropertyName("outputs")]
    public Dictionary<string, string>? Outputs { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("failed_at_step")]
    public string? FailedAtStep { get; set; }

    [JsonPropertyName("screenshot")]
    public string? Screenshot { get; set; }

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }

    [JsonPropertyName("steps_completed")]
    public int StepsCompleted { get; set; }

    [JsonPropertyName("steps_total")]
    public int StepsTotal { get; set; }

    [JsonPropertyName("log_file")]
    public string? LogFile { get; set; }

    // For status response
    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("current_task")]
    public string? CurrentTask { get; set; }

    [JsonPropertyName("current_step")]
    public string? CurrentStep { get; set; }

    [JsonPropertyName("rdp_connected")]
    public bool? RdpConnected { get; set; }

    // For list_tasks response
    [JsonPropertyName("tasks")]
    public List<string>? Tasks { get; set; }
}
