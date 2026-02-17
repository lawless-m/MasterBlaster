namespace MasterBlaster.Claude;

using System.Text.RegularExpressions;

public enum ExpectResult { Match, NoMatch, Uncertain }

public record CoordinateResult(bool Found, int X, int Y, string? ErrorDetail);

public record ExtractResult(bool Found, bool Empty, string? Value);

public record BooleanResult(bool Value);

public static partial class ResponseParser
{
    [GeneratedRegex(@"^(\d+)\s*,\s*(\d+)$")]
    private static partial Regex CoordinatePattern();

    /// <summary>
    /// Parses an expect response. The first line must be exactly MATCH, NO_MATCH, or UNCERTAIN.
    /// If NO_MATCH, remaining lines are included as detail.
    /// </summary>
    public static ExpectResult ParseExpectResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return ExpectResult.Uncertain;

        var firstLine = GetFirstLine(response).Trim();

        return firstLine.ToUpperInvariant() switch
        {
            "MATCH" => ExpectResult.Match,
            "NO_MATCH" => ExpectResult.NoMatch,
            "UNCERTAIN" => ExpectResult.Uncertain,
            _ => ExpectResult.Uncertain,
        };
    }

    /// <summary>
    /// Parses a coordinate response. Expects "x,y" on the first line, or "NOT_FOUND: detail".
    /// </summary>
    public static CoordinateResult ParseCoordinateResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new CoordinateResult(false, 0, 0, "Empty response");

        var firstLine = GetFirstLine(response).Trim();

        if (firstLine.StartsWith("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
        {
            var detail = firstLine.Length > "NOT_FOUND".Length
                ? firstLine["NOT_FOUND".Length..].TrimStart(':', ' ')
                : GetRemainingLines(response);
            return new CoordinateResult(false, 0, 0, string.IsNullOrWhiteSpace(detail) ? "Element not found" : detail);
        }

        var match = CoordinatePattern().Match(firstLine);
        if (match.Success
            && int.TryParse(match.Groups[1].Value, out var x)
            && int.TryParse(match.Groups[2].Value, out var y))
        {
            return new CoordinateResult(true, x, y, null);
        }

        return new CoordinateResult(false, 0, 0, $"Could not parse coordinates from: {firstLine}");
    }

    /// <summary>
    /// Parses an extract response. Returns EMPTY if the field is empty, NOT_FOUND if not found,
    /// otherwise returns the trimmed text value.
    /// </summary>
    public static ExtractResult ParseExtractResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new ExtractResult(false, false, null);

        var trimmed = response.Trim();

        if (trimmed.Equals("EMPTY", StringComparison.OrdinalIgnoreCase))
            return new ExtractResult(true, true, null);

        if (trimmed.StartsWith("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            return new ExtractResult(false, false, null);

        return new ExtractResult(true, false, trimmed);
    }

    /// <summary>
    /// Parses a boolean (YES/NO) response.
    /// </summary>
    public static BooleanResult ParseBooleanResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new BooleanResult(false);

        var firstLine = GetFirstLine(response).Trim();

        return new BooleanResult(firstLine.Equals("YES", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetFirstLine(string text)
    {
        var idx = text.IndexOfAny(['\r', '\n']);
        return idx < 0 ? text : text[..idx];
    }

    private static string GetRemainingLines(string text)
    {
        var idx = text.IndexOfAny(['\r', '\n']);
        if (idx < 0)
            return "";

        // Skip past the newline character(s)
        if (idx < text.Length - 1 && text[idx] == '\r' && text[idx + 1] == '\n')
            idx += 2;
        else
            idx += 1;

        return idx < text.Length ? text[idx..].Trim() : "";
    }
}
