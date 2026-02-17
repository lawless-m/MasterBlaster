namespace MasterBlaster.Tests.Tcp;

using MasterBlaster.Tcp;
using System.Text.Json;
using Xunit;

public class ProtocolTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ---- TaskRequest deserialization ----

    [Fact]
    public void TaskRequest_Deserializes_WithAllFields()
    {
        var json = """
            {
                "action": "run",
                "task": "create_invoice",
                "params": {
                    "customer_name": "Acme Corp",
                    "amount": "1500.00"
                }
            }
            """;

        var request = JsonSerializer.Deserialize<TaskRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("run", request.Action);
        Assert.Equal("create_invoice", request.Task);
        Assert.NotNull(request.Params);
        Assert.Equal(2, request.Params.Count);
        Assert.Equal("Acme Corp", request.Params["customer_name"]);
        Assert.Equal("1500.00", request.Params["amount"]);
    }

    [Fact]
    public void TaskRequest_Deserializes_WithMinimalFields()
    {
        var json = """
            {
                "action": "status"
            }
            """;

        var request = JsonSerializer.Deserialize<TaskRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("status", request.Action);
        Assert.Null(request.Task);
        Assert.Null(request.Params);
    }

    [Fact]
    public void TaskRequest_Deserializes_WithEmptyParams()
    {
        var json = """
            {
                "action": "run",
                "task": "simple_task",
                "params": {}
            }
            """;

        var request = JsonSerializer.Deserialize<TaskRequest>(json);

        Assert.NotNull(request);
        Assert.NotNull(request.Params);
        Assert.Empty(request.Params);
    }

    // ---- TaskResponse serialization ----

    [Fact]
    public void TaskResponse_Serializes_WithAllFields()
    {
        var response = new TaskResponse
        {
            Status = "completed",
            Task = "create_invoice",
            Outputs = new Dictionary<string, string>
            {
                { "confirmation_number", "INV-2024-001" }
            },
            DurationMs = 12345,
            StepsCompleted = 3,
            StepsTotal = 3,
            LogFile = "/logs/task_001.log"
        };

        var json = JsonSerializer.Serialize(response, SerializerOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("completed", root.GetProperty("status").GetString());
        Assert.Equal("create_invoice", root.GetProperty("task").GetString());
        Assert.Equal("INV-2024-001", root.GetProperty("outputs").GetProperty("confirmation_number").GetString());
        Assert.Equal(12345, root.GetProperty("durationMs").GetInt64());
        Assert.Equal(3, root.GetProperty("stepsCompleted").GetInt32());
        Assert.Equal(3, root.GetProperty("stepsTotal").GetInt32());
        Assert.Equal("/logs/task_001.log", root.GetProperty("logFile").GetString());
    }

    [Fact]
    public void TaskResponse_Serializes_NullFieldsOmitted()
    {
        var response = new TaskResponse
        {
            Status = "error",
            Error = "Something went wrong",
            DurationMs = 500,
            StepsCompleted = 1,
            StepsTotal = 3
        };

        var json = JsonSerializer.Serialize(response, SerializerOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("error", root.GetProperty("status").GetString());
        Assert.Equal("Something went wrong", root.GetProperty("error").GetString());

        // Null fields should be omitted
        Assert.False(root.TryGetProperty("task", out _));
        Assert.False(root.TryGetProperty("outputs", out _));
        Assert.False(root.TryGetProperty("failedAtStep", out _));
        Assert.False(root.TryGetProperty("screenshot", out _));
        Assert.False(root.TryGetProperty("logFile", out _));
        Assert.False(root.TryGetProperty("state", out _));
        Assert.False(root.TryGetProperty("currentTask", out _));
        Assert.False(root.TryGetProperty("currentStep", out _));
        Assert.False(root.TryGetProperty("rdpConnected", out _));
        Assert.False(root.TryGetProperty("tasks", out _));
    }

    [Fact]
    public void TaskResponse_Serializes_StatusFields()
    {
        var response = new TaskResponse
        {
            Status = "ok",
            State = "idle",
            RdpConnected = true
        };

        var json = JsonSerializer.Serialize(response, SerializerOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("idle", root.GetProperty("state").GetString());
        Assert.True(root.GetProperty("rdpConnected").GetBoolean());
    }

    [Fact]
    public void TaskResponse_Serializes_TaskListResponse()
    {
        var response = new TaskResponse
        {
            Status = "ok",
            Tasks = new List<string> { "create_invoice", "export_report", "login" }
        };

        var json = JsonSerializer.Serialize(response, SerializerOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("ok", root.GetProperty("status").GetString());
        var tasks = root.GetProperty("tasks");
        Assert.Equal(3, tasks.GetArrayLength());
        Assert.Equal("create_invoice", tasks[0].GetString());
        Assert.Equal("export_report", tasks[1].GetString());
        Assert.Equal("login", tasks[2].GetString());
    }

    [Fact]
    public void TaskResponse_Serializes_FailedTaskResponse()
    {
        var response = new TaskResponse
        {
            Status = "failed",
            Task = "create_invoice",
            Error = "Element not found",
            FailedAtStep = "Fill in invoice details",
            Screenshot = "base64encodeddata==",
            DurationMs = 8000,
            StepsCompleted = 1,
            StepsTotal = 3,
            LogFile = "/logs/task_fail.log"
        };

        var json = JsonSerializer.Serialize(response, SerializerOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("failed", root.GetProperty("status").GetString());
        Assert.Equal("create_invoice", root.GetProperty("task").GetString());
        Assert.Equal("Element not found", root.GetProperty("error").GetString());
        Assert.Equal("Fill in invoice details", root.GetProperty("failedAtStep").GetString());
        Assert.Equal("base64encodeddata==", root.GetProperty("screenshot").GetString());
    }

    // ---- Round-trip serialization/deserialization ----

    [Fact]
    public void TaskRequest_RoundTrips_ThroughJsonCorrectly()
    {
        var original = new TaskRequest
        {
            Action = "run",
            Task = "test_task",
            Params = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            }
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<TaskRequest>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Action, deserialized.Action);
        Assert.Equal(original.Task, deserialized.Task);
        Assert.Equal(original.Params!["key1"], deserialized.Params!["key1"]);
        Assert.Equal(original.Params["key2"], deserialized.Params["key2"]);
    }

    [Fact]
    public void TaskResponse_DefaultValues_AreCorrect()
    {
        var response = new TaskResponse();

        Assert.Equal("", response.Status);
        Assert.Null(response.Task);
        Assert.Null(response.Outputs);
        Assert.Null(response.Error);
        Assert.Null(response.FailedAtStep);
        Assert.Null(response.Screenshot);
        Assert.Equal(0, response.DurationMs);
        Assert.Equal(0, response.StepsCompleted);
        Assert.Equal(0, response.StepsTotal);
        Assert.Null(response.LogFile);
        Assert.Null(response.State);
        Assert.Null(response.CurrentTask);
        Assert.Null(response.CurrentStep);
        Assert.Null(response.RdpConnected);
        Assert.Null(response.Tasks);
    }
}
