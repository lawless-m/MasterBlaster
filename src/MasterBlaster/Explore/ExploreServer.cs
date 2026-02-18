namespace MasterBlaster.Explore;

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MasterBlaster.Claude;
using MasterBlaster.Rdp;
using Microsoft.Extensions.Logging;

public record ExploreAction(string Type, DateTime Timestamp, string Detail);

public class ExploreServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpListener _listener;
    private readonly IRdpController _rdp;
    private readonly IClaudeClient _claude;
    private readonly int _rdpWidth;
    private readonly int _rdpHeight;
    private readonly ILogger _logger;
    private readonly List<ExploreAction> _history = new();
    private readonly object _historyLock = new();

    public ExploreServer(IRdpController rdp, IClaudeClient claude, int rdpWidth, int rdpHeight, int port, ILogger logger)
    {
        _rdp = rdp;
        _claude = claude;
        _rdpWidth = rdpWidth;
        _rdpHeight = rdpHeight;
        _logger = logger;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _listener.Start();
        _logger.LogInformation("Explore server listening on {Prefix}", _listener.Prefixes.First());

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().WaitAsync(ct);
                _ = HandleRequestAsync(context);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var req = context.Request;
        var resp = context.Response;

        try
        {
            var path = req.Url?.AbsolutePath ?? "/";
            var method = req.HttpMethod;

            switch (path)
            {
                case "/":
                    ServeHtml(resp);
                    break;
                case "/api/screenshot":
                    await HandleScreenshotAsync(resp);
                    break;
                case "/api/status":
                    HandleStatus(resp);
                    break;
                case "/api/click" when method == "POST":
                    await HandleClickAsync(req, resp);
                    break;
                case "/api/doubleclick" when method == "POST":
                    await HandleDoubleClickAsync(req, resp);
                    break;
                case "/api/rightclick" when method == "POST":
                    await HandleRightClickAsync(req, resp);
                    break;
                case "/api/type" when method == "POST":
                    await HandleTypeAsync(req, resp);
                    break;
                case "/api/keycombo" when method == "POST":
                    await HandleKeyComboAsync(req, resp);
                    break;
                case "/api/chat" when method == "POST":
                    await HandleChatAsync(req, resp);
                    break;
                case "/api/history":
                    HandleHistory(resp);
                    break;
                case "/api/suggest-mbl" when method == "POST":
                    await HandleSuggestMblAsync(req, resp);
                    break;
                default:
                    resp.StatusCode = 404;
                    await WriteJsonAsync(resp, new { error = "Not found" });
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {Method} {Path}", req.HttpMethod, req.Url?.AbsolutePath);
            resp.StatusCode = 500;
            try { await WriteJsonAsync(resp, new { error = ex.Message }); } catch { }
        }
        finally
        {
            resp.Close();
        }
    }

    private void ServeHtml(HttpListenerResponse resp)
    {
        resp.ContentType = "text/html; charset=utf-8";
        var bytes = Encoding.UTF8.GetBytes(ExploreHtml.Html);
        resp.ContentLength64 = bytes.Length;
        resp.OutputStream.Write(bytes, 0, bytes.Length);
    }

    private async Task HandleScreenshotAsync(HttpListenerResponse resp)
    {
        var png = await _rdp.CaptureScreenshotAsync();
        resp.ContentType = "image/png";
        resp.ContentLength64 = png.Length;
        await resp.OutputStream.WriteAsync(png);
    }

    private void HandleStatus(HttpListenerResponse resp)
    {
        WriteJsonSync(resp, new { rdpConnected = _rdp.IsConnected, rdpWidth = _rdpWidth, rdpHeight = _rdpHeight });
    }

    private async Task HandleClickAsync(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadBodyAsync<CoordRequest>(req);
        await _rdp.ClickAsync(body.X, body.Y);
        RecordAction("click", $"({body.X}, {body.Y})");
        await WriteJsonAsync(resp, new { ok = true });
    }

    private async Task HandleDoubleClickAsync(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadBodyAsync<CoordRequest>(req);
        await _rdp.DoubleClickAsync(body.X, body.Y);
        RecordAction("doubleclick", $"({body.X}, {body.Y})");
        await WriteJsonAsync(resp, new { ok = true });
    }

    private async Task HandleRightClickAsync(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadBodyAsync<CoordRequest>(req);
        await _rdp.RightClickAsync(body.X, body.Y);
        RecordAction("rightclick", $"({body.X}, {body.Y})");
        await WriteJsonAsync(resp, new { ok = true });
    }

    private async Task HandleTypeAsync(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadBodyAsync<TextRequest>(req);
        await _rdp.SendKeysAsync(body.Text);
        RecordAction("type", body.Text);
        await WriteJsonAsync(resp, new { ok = true });
    }

    private async Task HandleKeyComboAsync(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadBodyAsync<KeyComboRequest>(req);
        await _rdp.SendKeyComboAsync(body.Combo);
        RecordAction("keycombo", body.Combo);
        await WriteJsonAsync(resp, new { ok = true });
    }

    private async Task HandleChatAsync(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadBodyAsync<ChatRequest>(req);
        var screenshot = await _rdp.CaptureScreenshotAsync();

        var planPrompt = $"""
            The user says: {body.Prompt}

            You are an interactive agent controlling a Windows application via RDP.
            Look at the screenshot and perform the requested actions.

            Respond with a DESCRIPTION line, then one or more numbered action lines.
            Each action line has ACTION, TARGET, and VALUE on the same line separated by two spaces.

            Format:
            DESCRIPTION: what you see and what you'll do
            1. ACTION: click  TARGET: element name  VALUE: x,y
            2. ACTION: type  TARGET: field name  VALUE: text to type

            Valid actions: click, doubleclick, rightclick, type, keycombo
            For click/doubleclick/rightclick: VALUE is x,y pixel coordinates
            For type: VALUE is the text to type
            For keycombo: VALUE is the key combination like Ctrl+S

            If the user is asking a question (not requesting an action), respond with only:
            DESCRIPTION: your answer

            Example — click a field then type into it:
            DESCRIPTION: I'll click the User name field and type the username
            1. ACTION: click  TARGET: User name field  VALUE: 400, 250
            2. ACTION: type  TARGET: User name field  VALUE: MHE
            """;

        var result = await _claude.SendAsync(screenshot, planPrompt);
        var parsed = ParseStructuredResponse(result.Text);

        var executedActions = new List<object>();

        foreach (var (action, target, value) in parsed.Actions)
        {
            if (await ExecuteActionAsync(action, value))
            {
                RecordAction(action, target);
                executedActions.Add(new { type = action, target });
                await Task.Delay(200);
            }
        }

        await WriteJsonAsync(resp, new
        {
            response = parsed.Description ?? result.Text,
            model = result.Model,
            inputTokens = result.InputTokens,
            outputTokens = result.OutputTokens,
            actions = executedActions,
        });
    }

    private record ParsedResponse(string? Description, List<(string Action, string Target, string Value)> Actions);

    private static readonly Regex ActionLineRegex = new(
        @"^\d+\.\s*ACTION:\s*(\S+)\s+TARGET:\s*(.*?)\s{2,}VALUE:\s*(.+)$",
        RegexOptions.IgnoreCase);

    private static ParsedResponse ParseStructuredResponse(string text)
    {
        string? description = null;
        var actions = new List<(string, string, string)>();

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
            {
                description = trimmed["DESCRIPTION:".Length..].Trim();
            }
            else
            {
                var m = ActionLineRegex.Match(trimmed);
                if (m.Success)
                {
                    actions.Add((
                        m.Groups[1].Value.Trim().ToLowerInvariant(),
                        m.Groups[2].Value.Trim(),
                        m.Groups[3].Value.Trim()));
                }
            }
        }

        return new ParsedResponse(description, actions);
    }

    private async Task<bool> ExecuteActionAsync(string action, string value)
    {
        switch (action)
        {
            case "click":
            case "doubleclick":
            case "rightclick":
                if (TryParseCoords(value, out var x, out var y))
                {
                    if (action == "click") await _rdp.ClickAsync(x, y);
                    else if (action == "doubleclick") await _rdp.DoubleClickAsync(x, y);
                    else await _rdp.RightClickAsync(x, y);
                    return true;
                }
                _logger.LogWarning("Could not parse coordinates from '{Value}'", value);
                return false;
            case "type":
                await _rdp.SendKeysAsync(value);
                return true;
            case "keycombo":
                await _rdp.SendKeyComboAsync(value);
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseCoords(string value, out int x, out int y)
    {
        x = y = 0;
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y);
    }

    private void HandleHistory(HttpListenerResponse resp)
    {
        List<ExploreAction> snapshot;
        lock (_historyLock)
        {
            snapshot = new List<ExploreAction>(_history);
        }
        WriteJsonSync(resp, snapshot);
    }

    private async Task HandleSuggestMblAsync(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var body = await ReadBodyAsync<SuggestMblRequest>(req);

        List<ExploreAction> snapshot;
        lock (_historyLock)
        {
            snapshot = new List<ExploreAction>(_history);
        }

        var actionLog = new StringBuilder();
        for (int i = 0; i < snapshot.Count; i++)
        {
            var a = snapshot[i];
            actionLog.AppendLine($"  {i + 1}. [{a.Type}] {a.Detail}");
        }

        var prompt = $"""
            I recorded the following actions during an interactive RDP exploration session.
            Generate an MBL task file for these actions.

            Task name: "{body.TaskName}"

            Recorded actions:
            {actionLog}

            MBL grammar reference:
            - task "Name" / input param1, param2
            - step "description" / timeout N (optional)
            - Actions: expect "...", click "...", double_click "...", right_click "...",
              type "value" into "field", type param_name into "field",
              select "value" in "dropdown", key Combo,
              extract var_name from "source", output var_name, screenshot, abort "msg"
            - if_screen_shows "condition" then ... else ... end
            - on timeout / on error handlers at the end

            Example:
            task "Create New Customer"
              input name, account_type, postcode

              step "Open customer screen"
                expect "main menu is visible"
                click "Customers menu item"
                expect "customer list screen is loaded"

              step "Save"
                click "Save button"
                expect "record saved successfully"

              on timeout
                abort "Timed out"

            Generate only the MBL file content, no explanation.
            """;

        var screenshot = await _rdp.CaptureScreenshotAsync();
        var result = await _claude.SendAsync(screenshot, prompt);
        RecordAction("suggest-mbl", body.TaskName);
        await WriteJsonAsync(resp, new { mbl = result.Text });
    }

    private void RecordAction(string type, string detail)
    {
        lock (_historyLock)
        {
            _history.Add(new ExploreAction(type, DateTime.UtcNow, detail));
        }
    }

    private static async Task<T> ReadBodyAsync<T>(HttpListenerRequest req)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid request body");
    }

    private static async Task WriteJsonAsync(HttpListenerResponse resp, object value)
    {
        resp.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        resp.ContentLength64 = bytes.Length;
        await resp.OutputStream.WriteAsync(bytes);
    }

    private static void WriteJsonSync(HttpListenerResponse resp, object value)
    {
        resp.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        resp.ContentLength64 = bytes.Length;
        resp.OutputStream.Write(bytes, 0, bytes.Length);
    }

    public async ValueTask DisposeAsync()
    {
        if (_listener.IsListening)
            _listener.Stop();
        await Task.CompletedTask;
    }

    private record CoordRequest(int X, int Y);
    private record TextRequest(string Text);
    private record KeyComboRequest(string Combo);
    private record ChatRequest(string Prompt);
    private record SuggestMblRequest(string TaskName);
}
