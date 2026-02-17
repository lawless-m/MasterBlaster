namespace MasterBlaster.Tcp;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MasterBlaster.Config;
using Microsoft.Extensions.Logging;

/// <summary>
/// TCP server that accepts connections and processes newline-delimited JSON
/// messages. Each connection can send multiple sequential requests.
/// </summary>
public class TcpServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly TcpConfig _config;
    private readonly RequestHandler _handler;
    private readonly ILogger<TcpServer> _log;
    private TcpListener? _listener;

    public TcpServer(TcpConfig config, RequestHandler handler, ILogger<TcpServer> log)
    {
        _config = config;
        _handler = handler;
        _log = log;
    }

    /// <summary>
    /// Starts listening for TCP connections. Blocks until the cancellation token
    /// is triggered, at which point it performs a graceful shutdown.
    /// </summary>
    public async Task StartAsync(CancellationToken ct)
    {
        var ip = IPAddress.Parse(_config.Host);
        _listener = new TcpListener(ip, _config.Port);
        _listener.Start();

        _log.LogInformation("TCP server listening on {Host}:{Port}", _config.Host, _config.Port);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException ex) when (ct.IsCancellationRequested)
                {
                    _log.LogDebug(ex, "Listener socket closed during shutdown");
                    break;
                }

                var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                _log.LogInformation("Client connected from {Endpoint}", endpoint);

                // Handle the connection on a background task so we can accept new ones
                _ = HandleConnectionAsync(client, endpoint, ct);
            }
        }
        finally
        {
            _listener.Stop();
            _log.LogInformation("TCP server stopped");
        }
    }

    /// <summary>
    /// Handles a single client connection, reading newline-delimited JSON
    /// requests and writing JSON responses.
    /// </summary>
    private async Task HandleConnectionAsync(TcpClient client, string endpoint, CancellationToken ct)
    {
        try
        {
            using (client)
            await using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true))
            await using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true))
            {
                writer.AutoFlush = true;

                while (!ct.IsCancellationRequested && client.Connected)
                {
                    string? line;
                    try
                    {
                        line = await reader.ReadLineAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        // Client disconnected
                        break;
                    }

                    if (line is null)
                    {
                        // Client closed the connection
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    _log.LogDebug("Received from {Endpoint}: {Message}", endpoint, line);

                    TaskResponse response;
                    try
                    {
                        var request = JsonSerializer.Deserialize<TaskRequest>(line, JsonOptions);
                        if (request is null)
                        {
                            response = new TaskResponse
                            {
                                Status = "error",
                                Error = "Failed to parse request JSON.",
                            };
                        }
                        else
                        {
                            response = await _handler.HandleAsync(request, ct);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _log.LogWarning(ex, "Invalid JSON from {Endpoint}", endpoint);
                        response = new TaskResponse
                        {
                            Status = "error",
                            Error = $"Invalid JSON: {ex.Message}",
                        };
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Unhandled error processing request from {Endpoint}", endpoint);
                        response = new TaskResponse
                        {
                            Status = "error",
                            Error = $"Internal server error: {ex.Message}",
                        };
                    }

                    var responseJson = JsonSerializer.Serialize(response, JsonOptions);
                    _log.LogDebug("Sending to {Endpoint}: {Response}", endpoint, responseJson);

                    try
                    {
                        await writer.WriteLineAsync(responseJson);
                    }
                    catch (IOException)
                    {
                        _log.LogWarning("Failed to send response to {Endpoint} — client disconnected", endpoint);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error handling connection from {Endpoint}", endpoint);
        }
        finally
        {
            _log.LogInformation("Client disconnected: {Endpoint}", endpoint);
        }
    }
}
