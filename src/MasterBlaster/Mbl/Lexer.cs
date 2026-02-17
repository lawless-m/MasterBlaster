namespace MasterBlaster.Mbl;

public enum TokenType
{
    Keyword,
    StringLiteral,
    Identifier,
    Integer,
    Comma,
    KeyCombo,
    Newline,
    Eof,
    Indent
}

public record Token(TokenType Type, string Value, int Line);

public class Lexer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "task", "input", "step", "expect", "click", "double-click", "right-click",
        "type", "into", "append", "select", "in", "key", "extract", "from",
        "output", "screenshot", "abort", "if", "screen", "shows", "else", "end",
        "on", "timeout", "error"
    };

    private static readonly HashSet<string> ValidKeys = new(StringComparer.Ordinal)
    {
        "Tab", "Enter", "Escape", "Space",
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "Ctrl", "Alt", "Shift",
        "Up", "Down", "Left", "Right",
        "Home", "End", "PageUp", "PageDown",
        "Backspace", "Delete"
    };

    public List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var lines = source.Split('\n');

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            int lineNumber = lineIndex + 1;
            string line = lines[lineIndex];

            // Remove trailing carriage return for Windows line endings
            if (line.EndsWith('\r'))
            {
                line = line[..^1];
            }

            // Skip completely empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Detect leading whitespace for indentation
            int indent = 0;
            int pos = 0;
            while (pos < line.Length && (line[pos] == ' ' || line[pos] == '\t'))
            {
                if (line[pos] == '\t')
                {
                    indent += 4; // Treat tab as 4 spaces
                }
                else
                {
                    indent++;
                }
                pos++;
            }

            // Skip blank lines (only whitespace)
            if (pos >= line.Length)
            {
                continue;
            }

            // Skip comment lines
            if (line[pos] == '#')
            {
                continue;
            }

            // Emit indent token if there is leading whitespace
            if (indent > 0)
            {
                tokens.Add(new Token(TokenType.Indent, indent.ToString(), lineNumber));
            }

            // Tokenize the rest of the line
            TokenizeLine(line, pos, lineNumber, tokens);

            // Emit newline token at the end of each logical line
            tokens.Add(new Token(TokenType.Newline, "\\n", lineNumber));
        }

        tokens.Add(new Token(TokenType.Eof, "", lines.Length > 0 ? lines.Length : 1));
        return tokens;
    }

    private void TokenizeLine(string line, int pos, int lineNumber, List<Token> tokens)
    {
        while (pos < line.Length)
        {
            char c = line[pos];

            // Skip whitespace between tokens (not leading)
            if (c == ' ' || c == '\t')
            {
                pos++;
                continue;
            }

            // Inline comment: rest of line is ignored
            if (c == '#')
            {
                break;
            }

            // String literal
            if (c == '"')
            {
                pos++;
                int start = pos;
                while (pos < line.Length && line[pos] != '"')
                {
                    pos++;
                }

                if (pos >= line.Length)
                {
                    throw new MblParseException($"Unterminated string literal", lineNumber);
                }

                string value = line[start..pos];
                tokens.Add(new Token(TokenType.StringLiteral, value, lineNumber));
                pos++; // skip closing quote
                continue;
            }

            // Comma
            if (c == ',')
            {
                tokens.Add(new Token(TokenType.Comma, ",", lineNumber));
                pos++;
                continue;
            }

            // Check for "double-click" and "right-click" keywords first
            if (pos + 12 <= line.Length && line.Substring(pos, 12).Equals("double-click", StringComparison.OrdinalIgnoreCase)
                && (pos + 12 >= line.Length || !IsIdentChar(line[pos + 12])))
            {
                tokens.Add(new Token(TokenType.Keyword, "double-click", lineNumber));
                pos += 12;
                continue;
            }

            if (pos + 11 <= line.Length && line.Substring(pos, 11).Equals("right-click", StringComparison.OrdinalIgnoreCase)
                && (pos + 11 >= line.Length || !IsIdentChar(line[pos + 11])))
            {
                tokens.Add(new Token(TokenType.Keyword, "right-click", lineNumber));
                pos += 11;
                continue;
            }

            // Identifier or keyword
            if (IsIdentStartChar(c))
            {
                int start = pos;
                while (pos < line.Length && IsIdentChar(line[pos]))
                {
                    pos++;
                }

                string word = line[start..pos];

                // Check if this is a key combo (e.g., Ctrl+S, Alt+F4, Tab, Enter)
                if (IsKeyComponent(word) && PeekIsKeyCombo(line, pos, word))
                {
                    // Parse the full key combo
                    string combo = word;
                    while (pos < line.Length && line[pos] == '+')
                    {
                        pos++; // skip '+'
                        int keyStart = pos;
                        while (pos < line.Length && IsIdentChar(line[pos]))
                        {
                            pos++;
                        }

                        if (pos == keyStart)
                        {
                            throw new MblParseException($"Expected key name after '+' in key combo", lineNumber);
                        }

                        string nextKey = line[keyStart..pos];
                        combo += "+" + nextKey;
                    }

                    tokens.Add(new Token(TokenType.KeyCombo, combo, lineNumber));
                    continue;
                }

                // Check if it's a keyword
                if (Keywords.Contains(word))
                {
                    tokens.Add(new Token(TokenType.Keyword, word.ToLowerInvariant(), lineNumber));
                }
                else
                {
                    tokens.Add(new Token(TokenType.Identifier, word, lineNumber));
                }

                continue;
            }

            // Integer
            if (char.IsDigit(c))
            {
                int start = pos;
                while (pos < line.Length && char.IsDigit(line[pos]))
                {
                    pos++;
                }

                // Check if followed by identifier chars (invalid token)
                if (pos < line.Length && IsIdentStartChar(line[pos]))
                {
                    throw new MblParseException($"Invalid token: number followed by letter", lineNumber);
                }

                string number = line[start..pos];
                tokens.Add(new Token(TokenType.Integer, number, lineNumber));
                continue;
            }

            throw new MblParseException($"Unexpected character '{c}'", lineNumber);
        }
    }

    /// <summary>
    /// Determines whether a word that is a valid key component should be treated as a key combo token.
    /// A standalone key name (like Tab, Enter, etc.) after the "key" keyword context
    /// is recognized as a key combo. Keys followed by '+' are always key combos.
    /// Single uppercase letters A-Z are treated as identifiers unless they form a combo with '+'.
    /// </summary>
    private bool PeekIsKeyCombo(string line, int pos, string word)
    {
        // If followed by '+', it's definitely a key combo
        if (pos < line.Length && line[pos] == '+')
        {
            return true;
        }

        // Standalone recognized key names (not single chars) are key combos
        if (ValidKeys.Contains(word))
        {
            return true;
        }

        // Single uppercase letter or single digit alone - only a key combo if followed by '+'
        if (word.Length == 1 && (char.IsUpper(word[0]) || char.IsDigit(word[0])))
        {
            return pos < line.Length && line[pos] == '+';
        }

        return false;
    }

    private static bool IsKeyComponent(string word)
    {
        if (ValidKeys.Contains(word))
        {
            return true;
        }

        // Single uppercase letter A-Z
        if (word.Length == 1 && word[0] >= 'A' && word[0] <= 'Z')
        {
            return true;
        }

        // Single digit 0-9
        if (word.Length == 1 && word[0] >= '0' && word[0] <= '9')
        {
            return true;
        }

        return false;
    }

    private static bool IsIdentStartChar(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
