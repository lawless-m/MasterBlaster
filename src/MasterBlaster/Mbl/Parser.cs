namespace MasterBlaster.Mbl;

public class MblParseException : Exception
{
    public int Line { get; }

    public MblParseException(string message, int line)
        : base($"Line {line}: {message}")
    {
        Line = line;
    }
}

public class Parser
{
    private List<Token> _tokens = new();
    private int _pos;

    public TaskDefinition Parse(List<Token> tokens, string fileName = "")
    {
        _tokens = tokens;
        _pos = 0;

        SkipNewlines();

        // Parse task declaration
        ExpectKeyword("task");
        string taskName = ExpectString();
        SkipNewlines();

        // Parse optional input declaration
        List<string> inputs = new();
        if (PeekKeyword("input"))
        {
            inputs = ParseInputDecl();
            SkipNewlines();
        }

        // Parse steps
        List<Step> steps = new();
        while (PeekKeyword("step"))
        {
            steps.Add(ParseStep());
            SkipNewlines();
        }

        // Parse optional error handlers
        ErrorHandler? onTimeout = null;
        ErrorHandler? onError = null;

        while (PeekKeyword("on"))
        {
            Advance(); // consume "on"
            SkipNewlines();

            if (PeekKeyword("timeout"))
            {
                Advance(); // consume "timeout"
                SkipNewlines();
                var actions = ParseActionList();
                onTimeout = new ErrorHandler { Actions = actions };
            }
            else if (PeekKeyword("error"))
            {
                Advance(); // consume "error"
                SkipNewlines();
                var actions = ParseActionList();
                onError = new ErrorHandler { Actions = actions };
            }
            else
            {
                throw new MblParseException(
                    $"Expected 'timeout' or 'error' after 'on', got '{Peek().Value}'",
                    Peek().Line);
            }

            SkipNewlines();
        }

        // Expect end of file
        if (Peek().Type != TokenType.Eof)
        {
            throw new MblParseException(
                $"Unexpected token '{Peek().Value}' at top level",
                Peek().Line);
        }

        return new TaskDefinition
        {
            Name = taskName,
            FileName = fileName,
            Inputs = inputs,
            Steps = steps,
            OnTimeout = onTimeout,
            OnError = onError
        };
    }

    private List<string> ParseInputDecl()
    {
        ExpectKeyword("input");
        var inputs = new List<string>();

        inputs.Add(ExpectIdentifier());

        while (PeekType(TokenType.Comma))
        {
            Advance(); // consume comma
            inputs.Add(ExpectIdentifier());
        }

        return inputs;
    }

    private Step ParseStep()
    {
        ExpectKeyword("step");
        string description = ExpectString();
        SkipNewlines();

        // Parse optional timeout (may be indented within the step)
        int? timeout = null;
        SkipIndent();
        if (PeekKeyword("timeout"))
        {
            Advance(); // consume "timeout"
            timeout = ExpectInteger();
            SkipNewlines();
        }

        // Parse actions
        var actions = ParseActionList();

        return new Step
        {
            Description = description,
            TimeoutSeconds = timeout,
            Actions = actions
        };
    }

    private List<IAction> ParseActionList()
    {
        var actions = new List<IAction>();

        while (IsActionStart())
        {
            actions.Add(ParseAction());
            SkipNewlines();
        }

        return actions;
    }

    private bool IsActionStart()
    {
        var token = Peek();
        if (token.Type == TokenType.Eof)
            return false;

        if (token.Type == TokenType.Indent)
        {
            // Look ahead past the indent to check if next token is an action keyword
            var next = PeekAt(_pos + 1);
            return next.Type == TokenType.Keyword && IsActionKeyword(next.Value);
        }

        return token.Type == TokenType.Keyword && IsActionKeyword(token.Value);
    }

    private static bool IsActionKeyword(string keyword)
    {
        return keyword is "expect" or "click" or "double-click" or "right-click"
            or "type" or "select" or "key" or "extract" or "output"
            or "screenshot" or "abort" or "if";
    }

    private IAction ParseAction()
    {
        // Skip indent tokens before actions
        SkipIndent();

        var token = Peek();

        if (token.Type != TokenType.Keyword)
        {
            throw new MblParseException(
                $"Expected action keyword, got '{token.Value}'",
                token.Line);
        }

        return token.Value switch
        {
            "expect" => ParseExpect(),
            "click" => ParseClick(),
            "double-click" => ParseDoubleClick(),
            "right-click" => ParseRightClick(),
            "type" => ParseType(),
            "select" => ParseSelect(),
            "key" => ParseKey(),
            "extract" => ParseExtract(),
            "output" => ParseOutput(),
            "screenshot" => ParseScreenshot(),
            "abort" => ParseAbort(),
            "if" => ParseIf(),
            _ => throw new MblParseException(
                $"Unknown action '{token.Value}'",
                token.Line)
        };
    }

    private ExpectAction ParseExpect()
    {
        ExpectKeyword("expect");
        string description = ExpectString();
        return new ExpectAction(description);
    }

    private ClickAction ParseClick()
    {
        ExpectKeyword("click");
        string target = ExpectString();
        return new ClickAction(target);
    }

    private DoubleClickAction ParseDoubleClick()
    {
        ExpectKeyword("double-click");
        string target = ExpectString();
        return new DoubleClickAction(target);
    }

    private RightClickAction ParseRightClick()
    {
        ExpectKeyword("right-click");
        string target = ExpectString();
        return new RightClickAction(target);
    }

    private TypeAction ParseType()
    {
        ExpectKeyword("type");

        var (value, isParam) = ParseValue();

        bool append = false;
        if (PeekKeyword("append"))
        {
            Advance();
            append = true;
        }

        ExpectKeyword("into");
        string target = ExpectString();

        return new TypeAction(value, isParam, target, append);
    }

    private SelectAction ParseSelect()
    {
        ExpectKeyword("select");

        var (value, isParam) = ParseValue();

        ExpectKeyword("in");
        string target = ExpectString();

        return new SelectAction(value, isParam, target);
    }

    private KeyAction ParseKey()
    {
        ExpectKeyword("key");
        string keyCombo = ExpectKeyCombo();
        return new KeyAction(keyCombo);
    }

    private ExtractAction ParseExtract()
    {
        ExpectKeyword("extract");
        string variableName = ExpectIdentifier();

        ExpectKeyword("from");
        string source = ExpectString();

        return new ExtractAction(variableName, source);
    }

    private OutputAction ParseOutput()
    {
        ExpectKeyword("output");
        string variableName = ExpectIdentifier();
        return new OutputAction(variableName);
    }

    private ScreenshotAction ParseScreenshot()
    {
        ExpectKeyword("screenshot");
        return new ScreenshotAction();
    }

    private AbortAction ParseAbort()
    {
        ExpectKeyword("abort");
        string message = ExpectString();
        return new AbortAction(message);
    }

    private IfScreenShowsAction ParseIf()
    {
        ExpectKeyword("if");
        ExpectKeyword("screen");
        ExpectKeyword("shows");
        string condition = ExpectString();
        SkipNewlines();

        // Parse then actions
        var thenActions = new List<IAction>();
        while (IsActionStart())
        {
            thenActions.Add(ParseAction());
            SkipNewlines();
        }

        // Parse optional else block
        List<IAction>? elseActions = null;
        SkipIndent();
        if (PeekKeyword("else"))
        {
            Advance(); // consume "else"
            SkipNewlines();

            elseActions = new List<IAction>();
            while (IsActionStart())
            {
                elseActions.Add(ParseAction());
                SkipNewlines();
            }
        }

        // Expect "end"
        SkipIndent();
        ExpectKeyword("end");

        return new IfScreenShowsAction(condition, thenActions, elseActions);
    }

    /// <summary>
    /// Parses a value, which is either a string literal or an identifier (parameter reference).
    /// Returns the value and whether it is a parameter reference.
    /// </summary>
    private (string Value, bool IsParam) ParseValue()
    {
        var token = Peek();

        if (token.Type == TokenType.StringLiteral)
        {
            Advance();
            return (token.Value, false);
        }

        if (token.Type == TokenType.Identifier)
        {
            Advance();
            return (token.Value, true);
        }

        throw new MblParseException(
            $"Expected string literal or identifier, got '{token.Value}'",
            token.Line);
    }

    // ---- Token navigation helpers ----

    private Token Peek()
    {
        if (_pos >= _tokens.Count)
            return new Token(TokenType.Eof, "", _tokens.Count > 0 ? _tokens[^1].Line : 1);
        return _tokens[_pos];
    }

    private Token PeekAt(int position)
    {
        if (position >= _tokens.Count)
            return new Token(TokenType.Eof, "", _tokens.Count > 0 ? _tokens[^1].Line : 1);
        return _tokens[position];
    }

    private Token Advance()
    {
        var token = Peek();
        _pos++;
        return token;
    }

    private bool PeekKeyword(string keyword)
    {
        var token = Peek();
        return token.Type == TokenType.Keyword
            && token.Value.Equals(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private bool PeekType(TokenType type)
    {
        return Peek().Type == type;
    }

    private void SkipNewlines()
    {
        while (_pos < _tokens.Count && _tokens[_pos].Type == TokenType.Newline)
        {
            _pos++;
        }
    }

    private void SkipIndent()
    {
        while (_pos < _tokens.Count && _tokens[_pos].Type == TokenType.Indent)
        {
            _pos++;
        }
    }

    private void ExpectKeyword(string keyword)
    {
        SkipIndent();
        var token = Peek();

        if (token.Type == TokenType.Keyword
            && token.Value.Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            return;
        }

        throw new MblParseException(
            $"Expected keyword '{keyword}', got '{token.Value}' ({token.Type})",
            token.Line);
    }

    private string ExpectString()
    {
        var token = Peek();
        if (token.Type != TokenType.StringLiteral)
        {
            throw new MblParseException(
                $"Expected string literal, got '{token.Value}' ({token.Type})",
                token.Line);
        }

        Advance();
        return token.Value;
    }

    private string ExpectIdentifier()
    {
        var token = Peek();
        if (token.Type != TokenType.Identifier)
        {
            throw new MblParseException(
                $"Expected identifier, got '{token.Value}' ({token.Type})",
                token.Line);
        }

        Advance();
        return token.Value;
    }

    private int ExpectInteger()
    {
        var token = Peek();
        if (token.Type != TokenType.Integer)
        {
            throw new MblParseException(
                $"Expected integer, got '{token.Value}' ({token.Type})",
                token.Line);
        }

        Advance();
        return int.Parse(token.Value);
    }

    private string ExpectKeyCombo()
    {
        var token = Peek();
        if (token.Type != TokenType.KeyCombo)
        {
            throw new MblParseException(
                $"Expected key combo, got '{token.Value}' ({token.Type})",
                token.Line);
        }

        Advance();
        return token.Value;
    }
}
