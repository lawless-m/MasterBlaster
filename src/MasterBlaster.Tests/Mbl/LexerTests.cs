namespace MasterBlaster.Tests.Mbl;

using MasterBlaster.Mbl;
using Xunit;

public class LexerTests
{
    private readonly Lexer _lexer = new();

    [Fact]
    public void Tokenize_SimpleTaskDeclaration_ReturnsTaskKeywordAndStringLiteral()
    {
        var source = "task \"my_task\"";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.Keyword, tokens[0].Type);
        Assert.Equal("task", tokens[0].Value);
        Assert.Equal(TokenType.StringLiteral, tokens[1].Type);
        Assert.Equal("my_task", tokens[1].Value);
        Assert.Equal(TokenType.Newline, tokens[2].Type);
        Assert.Equal(TokenType.Eof, tokens[3].Type);
    }

    [Fact]
    public void Tokenize_StringLiterals_PreservesContent()
    {
        var source = "task \"Hello World 123!\"";

        var tokens = _lexer.Tokenize(source);

        var stringToken = tokens[1];
        Assert.Equal(TokenType.StringLiteral, stringToken.Type);
        Assert.Equal("Hello World 123!", stringToken.Value);
    }

    [Fact]
    public void Tokenize_CommentLines_AreSkipped()
    {
        var source = """
            # This is a comment
            task "my_task"
            # Another comment
            """;

        var tokens = _lexer.Tokenize(source);

        // The comment lines should be skipped entirely
        Assert.Equal(TokenType.Keyword, tokens[0].Type);
        Assert.Equal("task", tokens[0].Value);
        Assert.Equal(TokenType.StringLiteral, tokens[1].Type);
        Assert.Equal("my_task", tokens[1].Value);
        Assert.Equal(TokenType.Newline, tokens[2].Type);
        Assert.Equal(TokenType.Eof, tokens[3].Type);
    }

    [Fact]
    public void Tokenize_InlineComment_IsIgnored()
    {
        var source = "task \"my_task\" # inline comment";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.Keyword, tokens[0].Type);
        Assert.Equal(TokenType.StringLiteral, tokens[1].Type);
        Assert.Equal(TokenType.Newline, tokens[2].Type);
        Assert.Equal(TokenType.Eof, tokens[3].Type);
    }

    [Fact]
    public void Tokenize_IndentedLine_EmitsIndentToken()
    {
        var source = "    click \"button\"";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.Indent, tokens[0].Type);
        Assert.Equal("4", tokens[0].Value);
        Assert.Equal(TokenType.Keyword, tokens[1].Type);
        Assert.Equal("click", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_TabIndentation_TreatsTabAsFourSpaces()
    {
        var source = "\tclick \"button\"";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.Indent, tokens[0].Type);
        Assert.Equal("4", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_SimpleKeyCombos_ReturnsKeyComboTokens()
    {
        var source = "key Tab";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.Keyword, tokens[0].Type);
        Assert.Equal("key", tokens[0].Value);
        Assert.Equal(TokenType.KeyCombo, tokens[1].Type);
        Assert.Equal("Tab", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_EnterKey_ReturnsKeyComboToken()
    {
        var source = "key Enter";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.KeyCombo, tokens[1].Type);
        Assert.Equal("Enter", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_CtrlPlusS_ReturnsKeyComboToken()
    {
        var source = "key Ctrl+S";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.KeyCombo, tokens[1].Type);
        Assert.Equal("Ctrl+S", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_AltPlusF4_ReturnsKeyComboToken()
    {
        var source = "key Alt+F4";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.KeyCombo, tokens[1].Type);
        Assert.Equal("Alt+F4", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_DoubleClickKeyword_ReturnsKeywordToken()
    {
        var source = "double-click \"item\"";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.Keyword, tokens[0].Type);
        Assert.Equal("double-click", tokens[0].Value);
        Assert.Equal(TokenType.StringLiteral, tokens[1].Type);
        Assert.Equal("item", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_RightClickKeyword_ReturnsKeywordToken()
    {
        var source = "right-click \"item\"";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.Keyword, tokens[0].Type);
        Assert.Equal("right-click", tokens[0].Value);
        Assert.Equal(TokenType.StringLiteral, tokens[1].Type);
        Assert.Equal("item", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_UnterminatedString_ThrowsMblParseException()
    {
        var source = "task \"unterminated";

        var ex = Assert.Throws<MblParseException>(() => _lexer.Tokenize(source));
        Assert.Contains("Unterminated string literal", ex.Message);
    }

    [Fact]
    public void Tokenize_MultipleInputsWithCommas_ReturnsCommaTokens()
    {
        var source = "input customer_name, invoice_number, amount";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.Keyword, tokens[0].Type);
        Assert.Equal("input", tokens[0].Value);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("customer_name", tokens[1].Value);
        Assert.Equal(TokenType.Comma, tokens[2].Type);
        Assert.Equal(TokenType.Identifier, tokens[3].Type);
        Assert.Equal("invoice_number", tokens[3].Value);
        Assert.Equal(TokenType.Comma, tokens[4].Type);
        Assert.Equal(TokenType.Identifier, tokens[5].Type);
        Assert.Equal("amount", tokens[5].Value);
    }

    [Fact]
    public void Tokenize_EmptyInput_ReturnsOnlyEof()
    {
        var source = "";

        var tokens = _lexer.Tokenize(source);

        Assert.Single(tokens);
        Assert.Equal(TokenType.Eof, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_WhitespaceOnlyInput_ReturnsOnlyEof()
    {
        var source = "   \n   \n   ";

        var tokens = _lexer.Tokenize(source);

        Assert.Single(tokens);
        Assert.Equal(TokenType.Eof, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_CompleteMblFile_ReturnsAllExpectedTokens()
    {
        var source = """
            task "create_invoice"
            input customer_name, amount

            step "Fill out invoice form"
                timeout 30
                click "New Invoice"
                type customer_name into "Customer Name"
                type "100.00" into "Amount"
                select "Net 30" in "Payment Terms"
                key Tab
                extract total from "Total"
                output total
                screenshot
                if screen shows "Confirmation dialog"
                    click "OK"
                else
                    abort "Expected confirmation"
                end

            on timeout
                screenshot
                abort "Timed out"

            on error
                screenshot
            """;

        var tokens = _lexer.Tokenize(source);

        // Verify the token stream starts correctly
        Assert.Equal(TokenType.Keyword, tokens[0].Type);
        Assert.Equal("task", tokens[0].Value);
        Assert.Equal(1, tokens[0].Line);

        Assert.Equal(TokenType.StringLiteral, tokens[1].Type);
        Assert.Equal("create_invoice", tokens[1].Value);

        // Verify we find all major keywords in the stream
        var keywords = tokens.Where(t => t.Type == TokenType.Keyword).Select(t => t.Value).ToList();
        Assert.Contains("task", keywords);
        Assert.Contains("input", keywords);
        Assert.Contains("step", keywords);
        Assert.Contains("click", keywords);
        Assert.Contains("type", keywords);
        Assert.Contains("into", keywords);
        Assert.Contains("select", keywords);
        Assert.Contains("in", keywords);
        Assert.Contains("key", keywords);
        Assert.Contains("extract", keywords);
        Assert.Contains("from", keywords);
        Assert.Contains("output", keywords);
        Assert.Contains("screenshot", keywords);
        Assert.Contains("if", keywords);
        Assert.Contains("screen", keywords);
        Assert.Contains("shows", keywords);
        Assert.Contains("else", keywords);
        Assert.Contains("end", keywords);
        Assert.Contains("abort", keywords);
        Assert.Contains("on", keywords);
        Assert.Contains("timeout", keywords);
        Assert.Contains("error", keywords);

        // Verify identifiers for input params
        var identifiers = tokens.Where(t => t.Type == TokenType.Identifier).Select(t => t.Value).ToList();
        Assert.Contains("customer_name", identifiers);
        Assert.Contains("amount", identifiers);
        Assert.Contains("total", identifiers);

        // Verify key combo
        var keyCombos = tokens.Where(t => t.Type == TokenType.KeyCombo).Select(t => t.Value).ToList();
        Assert.Contains("Tab", keyCombos);

        // Verify integers
        var integers = tokens.Where(t => t.Type == TokenType.Integer).Select(t => t.Value).ToList();
        Assert.Contains("30", integers);

        // Last token should be Eof
        Assert.Equal(TokenType.Eof, tokens[^1].Type);
    }

    [Fact]
    public void Tokenize_LineNumbersAreCorrect()
    {
        var source = """
            task "my_task"
            input name
            step "do thing"
            """;

        var tokens = _lexer.Tokenize(source);

        // "task" is on line 1
        Assert.Equal(1, tokens[0].Line);

        // "input" keyword should be on line 2
        var inputToken = tokens.First(t => t.Type == TokenType.Keyword && t.Value == "input");
        Assert.Equal(2, inputToken.Line);

        // "step" keyword should be on line 3
        var stepToken = tokens.First(t => t.Type == TokenType.Keyword && t.Value == "step");
        Assert.Equal(3, stepToken.Line);
    }

    [Fact]
    public void Tokenize_IntegerToken_IsParsedCorrectly()
    {
        var source = "    timeout 45";

        var tokens = _lexer.Tokenize(source);

        Assert.Equal(TokenType.Indent, tokens[0].Type);
        Assert.Equal(TokenType.Keyword, tokens[1].Type);
        Assert.Equal("timeout", tokens[1].Value);
        Assert.Equal(TokenType.Integer, tokens[2].Type);
        Assert.Equal("45", tokens[2].Value);
    }
}
