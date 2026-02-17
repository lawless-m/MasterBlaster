namespace MasterBlaster.Claude;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using MasterBlaster.Config;
using Microsoft.Extensions.Logging;

public class ClaudeClient : IClaudeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxTokens;
    private readonly string _systemPrompt;
    private readonly bool _retryOnRateLimit;
    private readonly int _rateLimitBackoffMs;
    private readonly int _maxRetries;
    private readonly ILogger<ClaudeClient> _logger;

    public ClaudeClient(ClaudeConfig config, int screenWidth, int screenHeight, ILogger<ClaudeClient> logger)
        : this(new HttpClient(), config, screenWidth, screenHeight, logger)
    {
    }

    public ClaudeClient(HttpClient http, ClaudeConfig config, int screenWidth, int screenHeight, ILogger<ClaudeClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _apiKey = ConfigLoader.ResolveClaudeApiKey(config)
            ?? throw new ArgumentException($"Environment variable '{config.ApiKeyEnv}' is not set", nameof(config));
        _model = config.Model;
        _maxTokens = config.MaxTokensPerRequest;
        _systemPrompt = PromptBuilder.BuildSystemPrompt(screenWidth, screenHeight);
        _retryOnRateLimit = config.RetryOnRateLimit;
        _rateLimitBackoffMs = config.RateLimitBackoffMs;
        _maxRetries = 3;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClaudeResponse> SendAsync(byte[] screenshotPng, string prompt, CancellationToken ct = default)
    {
        var base64Image = Convert.ToBase64String(screenshotPng);

        var requestBody = new AnthropicRequest
        {
            Model = _model,
            MaxTokens = _maxTokens,
            System = _systemPrompt,
            Messages =
            [
                new AnthropicMessage
                {
                    Role = "user",
                    Content =
                    [
                        new ContentBlock
                        {
                            Type = "image",
                            Source = new ImageSource
                            {
                                Type = "base64",
                                MediaType = "image/png",
                                Data = base64Image,
                            },
                        },
                        new ContentBlock
                        {
                            Type = "text",
                            Text = prompt,
                        },
                    ],
                },
            ],
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var sw = Stopwatch.StartNew();
        var attempt = 0;

        while (true)
        {
            attempt++;
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(json);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            _logger.LogDebug("Sending request to Claude API (attempt {Attempt}), model={Model}", attempt, _model);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request to Claude API failed on attempt {Attempt}", attempt);
                throw;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests && _retryOnRateLimit && attempt <= _maxRetries)
            {
                var backoff = _rateLimitBackoffMs * (int)Math.Pow(2, attempt - 1);
                _logger.LogWarning(
                    "Rate limited by Claude API (429). Retrying in {BackoffMs}ms (attempt {Attempt}/{MaxRetries})",
                    backoff, attempt, _maxRetries);
                await Task.Delay(backoff, ct);
                continue;
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Claude API returned {StatusCode}: {Body}",
                    (int)response.StatusCode, responseBody);
                throw new HttpRequestException(
                    $"Claude API returned {(int)response.StatusCode}: {responseBody}",
                    null,
                    response.StatusCode);
            }

            sw.Stop();

            var anthropicResponse = JsonSerializer.Deserialize<AnthropicResponse>(responseBody, JsonOptions);
            if (anthropicResponse is null)
            {
                throw new InvalidOperationException("Failed to deserialize Claude API response");
            }

            var text = ExtractTextContent(anthropicResponse);

            _logger.LogDebug(
                "Claude API responded in {Duration}ms. Input tokens: {InputTokens}, Output tokens: {OutputTokens}",
                sw.ElapsedMilliseconds, anthropicResponse.Usage?.InputTokens ?? 0, anthropicResponse.Usage?.OutputTokens ?? 0);

            return new ClaudeResponse
            {
                Text = text,
                InputTokens = anthropicResponse.Usage?.InputTokens ?? 0,
                OutputTokens = anthropicResponse.Usage?.OutputTokens ?? 0,
                Model = anthropicResponse.Model ?? _model,
                Duration = sw.Elapsed,
            };
        }
    }

    private static string ExtractTextContent(AnthropicResponse response)
    {
        if (response.Content is null || response.Content.Count == 0)
            return "";

        var textParts = response.Content
            .Where(c => c.Type == "text" && c.Text is not null)
            .Select(c => c.Text!);

        return string.Join("\n", textParts);
    }

    // --- Request DTOs ---

    private sealed class AnthropicRequest
    {
        public string Model { get; init; } = "";
        public int MaxTokens { get; init; }
        public string? System { get; init; }
        public List<AnthropicMessage> Messages { get; init; } = [];
    }

    private sealed class AnthropicMessage
    {
        public string Role { get; init; } = "";
        public List<ContentBlock> Content { get; init; } = [];
    }

    private sealed class ContentBlock
    {
        public string Type { get; init; } = "";
        public string? Text { get; init; }
        public ImageSource? Source { get; init; }
    }

    private sealed class ImageSource
    {
        public string Type { get; init; } = "";
        public string MediaType { get; init; } = "";
        public string Data { get; init; } = "";
    }

    // --- Response DTOs ---

    private sealed class AnthropicResponse
    {
        public string? Model { get; init; }
        public List<ResponseContentBlock>? Content { get; init; }
        public UsageInfo? Usage { get; init; }
    }

    private sealed class ResponseContentBlock
    {
        public string Type { get; init; } = "";
        public string? Text { get; init; }
    }

    private sealed class UsageInfo
    {
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }
    }
}
