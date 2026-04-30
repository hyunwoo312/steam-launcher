using System.IO;
using System.Text;

namespace Flow.Launcher.Plugin.SteamLauncher.Vdf;

public sealed class VdfParser : IVdfParser
{
    public VdfNode.Object Parse(string content)
    {
        var tokenizer = new Tokenizer(content);
        var children = new Dictionary<string, VdfNode>(StringComparer.Ordinal);

        while (tokenizer.PeekToken() is { } token && token.Kind != TokenKind.Eof)
        {
            if (token.Kind == TokenKind.CloseBrace)
                throw new VdfParseException("Unexpected '}' at top level");

            var key = ExpectString(tokenizer);
            var value = ParseValue(tokenizer);
            children[key] = value;
        }

        return new VdfNode.Object(children);
    }

    public VdfNode.Object ParseFile(string filePath)
    {
        try
        {
            return Parse(File.ReadAllText(filePath));
        }
        catch (VdfParseException ex)
        {
            throw new VdfParseException(ex.Message, filePath, ex);
        }
        catch (IOException ex)
        {
            throw new VdfParseException($"Failed to read file: {ex.Message}", filePath, ex);
        }
    }

    private VdfNode ParseValue(Tokenizer tokenizer)
    {
        var token = tokenizer.NextToken();
        return token.Kind switch
        {
            TokenKind.String => new VdfNode.String(token.Value!),
            TokenKind.OpenBrace => ParseObject(tokenizer),
            _ => throw new VdfParseException($"Expected value, got {token.Kind}")
        };
    }

    private VdfNode.Object ParseObject(Tokenizer tokenizer)
    {
        var children = new Dictionary<string, VdfNode>(StringComparer.Ordinal);

        while (true)
        {
            var token = tokenizer.PeekToken();
            if (token is null || token.Kind == TokenKind.Eof)
                throw new VdfParseException("Unexpected end of input inside object");
            if (token.Kind == TokenKind.CloseBrace)
            {
                tokenizer.NextToken();
                return new VdfNode.Object(children);
            }

            var key = ExpectString(tokenizer);
            var value = ParseValue(tokenizer);
            children[key] = value;
        }
    }

    private static string ExpectString(Tokenizer tokenizer)
    {
        var token = tokenizer.NextToken();
        return token.Kind == TokenKind.String
            ? token.Value!
            : throw new VdfParseException($"Expected string key, got {token.Kind}");
    }

    private enum TokenKind { String, OpenBrace, CloseBrace, Eof }

    private sealed record Token(TokenKind Kind, string? Value = null);

    private sealed class Tokenizer(string input)
    {
        private int _pos;
        private Token? _peeked;

        public Token PeekToken() => _peeked ??= ReadToken();

        public Token NextToken()
        {
            if (_peeked is { } cached)
            {
                _peeked = null;
                return cached;
            }
            return ReadToken();
        }

        private Token ReadToken()
        {
            SkipWhitespaceAndComments();
            if (_pos >= input.Length) return new Token(TokenKind.Eof);

            var c = input[_pos];
            if (c == '{') { _pos++; return new Token(TokenKind.OpenBrace); }
            if (c == '}') { _pos++; return new Token(TokenKind.CloseBrace); }
            if (c == '"') return new Token(TokenKind.String, ReadQuotedString());
            return new Token(TokenKind.String, ReadBareString());
        }

        private void SkipWhitespaceAndComments()
        {
            while (_pos < input.Length)
            {
                var c = input[_pos];
                if (char.IsWhiteSpace(c)) { _pos++; continue; }
                if (c == '/' && _pos + 1 < input.Length && input[_pos + 1] == '/')
                {
                    while (_pos < input.Length && input[_pos] != '\n') _pos++;
                    continue;
                }
                // Platform conditionals like [$WIN32] — skip past closing bracket.
                if (c == '[')
                {
                    while (_pos < input.Length && input[_pos] != ']') _pos++;
                    if (_pos < input.Length) _pos++;
                    continue;
                }
                break;
            }
        }

        private string ReadQuotedString()
        {
            _pos++;
            var sb = new StringBuilder();
            while (_pos < input.Length)
            {
                var c = input[_pos];
                if (c == '"') { _pos++; return sb.ToString(); }
                if (c == '\\' && _pos + 1 < input.Length)
                {
                    var next = input[_pos + 1];
                    sb.Append(next switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        '\\' => '\\',
                        '"' => '"',
                        _ => next
                    });
                    _pos += 2;
                    continue;
                }
                sb.Append(c);
                _pos++;
            }
            throw new VdfParseException("Unterminated quoted string");
        }

        private string ReadBareString()
        {
            var start = _pos;
            while (_pos < input.Length && !char.IsWhiteSpace(input[_pos])
                   && input[_pos] != '{' && input[_pos] != '}'
                   && input[_pos] != '"')
            {
                _pos++;
            }
            return input.AsSpan(start, _pos - start).ToString();
        }
    }
}
