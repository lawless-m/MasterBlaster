namespace MasterBlaster.Tests.Claude;

using MasterBlaster.Claude;
using Xunit;

public class ResponseParserTests
{
    // ---- ParseExpectResponse ----

    [Fact]
    public void ParseExpectResponse_Match_ReturnsMatch()
    {
        var result = ResponseParser.ParseExpectResponse("MATCH");

        Assert.Equal(ExpectResult.Match, result);
    }

    [Fact]
    public void ParseExpectResponse_MatchWithTrailingText_ReturnsMatch()
    {
        var result = ResponseParser.ParseExpectResponse("MATCH\nThe screen shows the expected content.");

        Assert.Equal(ExpectResult.Match, result);
    }

    [Fact]
    public void ParseExpectResponse_NoMatch_ReturnsNoMatch()
    {
        var result = ResponseParser.ParseExpectResponse("NO_MATCH");

        Assert.Equal(ExpectResult.NoMatch, result);
    }

    [Fact]
    public void ParseExpectResponse_NoMatchWithDetail_ReturnsNoMatch()
    {
        var result = ResponseParser.ParseExpectResponse("NO_MATCH\nThe screen shows a login form instead.");

        Assert.Equal(ExpectResult.NoMatch, result);
    }

    [Fact]
    public void ParseExpectResponse_Uncertain_ReturnsUncertain()
    {
        var result = ResponseParser.ParseExpectResponse("UNCERTAIN");

        Assert.Equal(ExpectResult.Uncertain, result);
    }

    [Fact]
    public void ParseExpectResponse_EmptyInput_ReturnsUncertain()
    {
        var result = ResponseParser.ParseExpectResponse("");

        Assert.Equal(ExpectResult.Uncertain, result);
    }

    [Fact]
    public void ParseExpectResponse_WhitespaceOnly_ReturnsUncertain()
    {
        var result = ResponseParser.ParseExpectResponse("   ");

        Assert.Equal(ExpectResult.Uncertain, result);
    }

    [Fact]
    public void ParseExpectResponse_UnexpectedInput_ReturnsUncertain()
    {
        var result = ResponseParser.ParseExpectResponse("I can see a button on the screen.");

        Assert.Equal(ExpectResult.Uncertain, result);
    }

    [Fact]
    public void ParseExpectResponse_CaseInsensitive_ReturnsMatch()
    {
        var result = ResponseParser.ParseExpectResponse("match");

        Assert.Equal(ExpectResult.Match, result);
    }

    // ---- ParseCoordinateResponse ----

    [Fact]
    public void ParseCoordinateResponse_ValidCoordinates_ReturnsFoundWithCoords()
    {
        var result = ResponseParser.ParseCoordinateResponse("645,312");

        Assert.True(result.Found);
        Assert.Equal(645, result.X);
        Assert.Equal(312, result.Y);
        Assert.Null(result.ErrorDetail);
    }

    [Fact]
    public void ParseCoordinateResponse_CoordinatesWithSpace_ReturnsFoundWithCoords()
    {
        var result = ResponseParser.ParseCoordinateResponse("645, 312");

        Assert.True(result.Found);
        Assert.Equal(645, result.X);
        Assert.Equal(312, result.Y);
        Assert.Null(result.ErrorDetail);
    }

    [Fact]
    public void ParseCoordinateResponse_CoordinatesWithExtraSpaces_ReturnsFoundWithCoords()
    {
        var result = ResponseParser.ParseCoordinateResponse("  645 , 312  ");

        Assert.True(result.Found);
        Assert.Equal(645, result.X);
        Assert.Equal(312, result.Y);
    }

    [Fact]
    public void ParseCoordinateResponse_NotFound_ReturnsNotFoundWithDetail()
    {
        var result = ResponseParser.ParseCoordinateResponse("NOT_FOUND: The button is not visible");

        Assert.False(result.Found);
        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal("The button is not visible", result.ErrorDetail);
    }

    [Fact]
    public void ParseCoordinateResponse_NotFoundWithoutDetail_ReturnsDefaultMessage()
    {
        var result = ResponseParser.ParseCoordinateResponse("NOT_FOUND");

        Assert.False(result.Found);
        Assert.NotNull(result.ErrorDetail);
    }

    [Fact]
    public void ParseCoordinateResponse_InvalidFormat_ReturnsError()
    {
        var result = ResponseParser.ParseCoordinateResponse("I can see the button at the top left");

        Assert.False(result.Found);
        Assert.NotNull(result.ErrorDetail);
        Assert.Contains("Could not parse coordinates", result.ErrorDetail);
    }

    [Fact]
    public void ParseCoordinateResponse_EmptyInput_ReturnsError()
    {
        var result = ResponseParser.ParseCoordinateResponse("");

        Assert.False(result.Found);
        Assert.Equal("Empty response", result.ErrorDetail);
    }

    [Fact]
    public void ParseCoordinateResponse_MultilineWithCoordinatesFirst_ParsesFirstLine()
    {
        var result = ResponseParser.ParseCoordinateResponse("320,240\nThe button is in the center of the screen.");

        Assert.True(result.Found);
        Assert.Equal(320, result.X);
        Assert.Equal(240, result.Y);
    }

    // ---- ParseExtractResponse ----

    [Fact]
    public void ParseExtractResponse_Value_ReturnsFoundWithValue()
    {
        var result = ResponseParser.ParseExtractResponse("INV-2024-001");

        Assert.True(result.Found);
        Assert.False(result.Empty);
        Assert.Equal("INV-2024-001", result.Value);
    }

    [Fact]
    public void ParseExtractResponse_ValueWithWhitespace_ReturnsTrimmedValue()
    {
        var result = ResponseParser.ParseExtractResponse("  INV-2024-001  ");

        Assert.True(result.Found);
        Assert.False(result.Empty);
        Assert.Equal("INV-2024-001", result.Value);
    }

    [Fact]
    public void ParseExtractResponse_Empty_ReturnsFoundAndEmpty()
    {
        var result = ResponseParser.ParseExtractResponse("EMPTY");

        Assert.True(result.Found);
        Assert.True(result.Empty);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ParseExtractResponse_EmptyCaseInsensitive_ReturnsFoundAndEmpty()
    {
        var result = ResponseParser.ParseExtractResponse("empty");

        Assert.True(result.Found);
        Assert.True(result.Empty);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ParseExtractResponse_NotFound_ReturnsNotFound()
    {
        var result = ResponseParser.ParseExtractResponse("NOT_FOUND");

        Assert.False(result.Found);
        Assert.False(result.Empty);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ParseExtractResponse_EmptyInput_ReturnsNotFound()
    {
        var result = ResponseParser.ParseExtractResponse("");

        Assert.False(result.Found);
        Assert.False(result.Empty);
        Assert.Null(result.Value);
    }

    [Fact]
    public void ParseExtractResponse_WhitespaceOnly_ReturnsNotFound()
    {
        var result = ResponseParser.ParseExtractResponse("   ");

        Assert.False(result.Found);
        Assert.Null(result.Value);
    }

    // ---- ParseBooleanResponse ----

    [Fact]
    public void ParseBooleanResponse_Yes_ReturnsTrue()
    {
        var result = ResponseParser.ParseBooleanResponse("YES");

        Assert.True(result.Value);
    }

    [Fact]
    public void ParseBooleanResponse_YesCaseInsensitive_ReturnsTrue()
    {
        var result = ResponseParser.ParseBooleanResponse("yes");

        Assert.True(result.Value);
    }

    [Fact]
    public void ParseBooleanResponse_No_ReturnsFalse()
    {
        var result = ResponseParser.ParseBooleanResponse("NO");

        Assert.False(result.Value);
    }

    [Fact]
    public void ParseBooleanResponse_EmptyInput_ReturnsFalse()
    {
        var result = ResponseParser.ParseBooleanResponse("");

        Assert.False(result.Value);
    }

    [Fact]
    public void ParseBooleanResponse_WhitespaceOnly_ReturnsFalse()
    {
        var result = ResponseParser.ParseBooleanResponse("   ");

        Assert.False(result.Value);
    }

    [Fact]
    public void ParseBooleanResponse_UnexpectedInput_ReturnsFalse()
    {
        var result = ResponseParser.ParseBooleanResponse("MAYBE");

        Assert.False(result.Value);
    }

    [Fact]
    public void ParseBooleanResponse_YesWithTrailingText_ReturnsTrue()
    {
        var result = ResponseParser.ParseBooleanResponse("YES\nThe dialog is visible on screen.");

        Assert.True(result.Value);
    }
}
