using FWO.Basics.Exceptions;
using FWO.Report.Filter.Exceptions;
using System.Text;

namespace FWO.Report.Filter
{
    public class Scanner
    {
        private readonly string input;
        private int position = 0;

        private static readonly Dictionary<string, TokenKind> whitespaceTokens = [];
        private static readonly Dictionary<string, TokenKind> noWhitespaceTokens = [];
        private static readonly int noWhitespaceTokenMaxLength = 0;

        public Scanner(string input)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
        }

        static Scanner()
        {
            // Initialize Token Syntax
            foreach (TokenKind tokenKind in Enum.GetValues(typeof(TokenKind)))
            {
                TokenSyntax validTokenSyntax = TokenSyntax.Get(tokenKind);

                foreach (string tokenSyntax in validTokenSyntax.WhiteSpaceRequiered)
                {
                    whitespaceTokens[tokenSyntax.ToLower()] = tokenKind;
                }
                foreach (string tokenSyntax in validTokenSyntax.NoWhiteSpaceRequiered)
                {
                    noWhitespaceTokens[tokenSyntax.ToLower()] = tokenKind;
                    if (tokenSyntax.Length > noWhitespaceTokenMaxLength)
                    {
                        noWhitespaceTokenMaxLength = tokenSyntax.Length;
                    }
                }
            }
        }

        public List<Token> Scan()
        {
            List<Token> tokens = [];

            while (position < input.Length)
            {
                if (position < input.Length && IsWhitespace(position))
                {
                    position++;
                    continue;
                }
                tokens.AddRange(ReadTokens());
                position++;
            }
            return tokens;
        }

        private bool IsWhitespace(int currentPosition)
        {
            return input[currentPosition] == ' ' || input[currentPosition] == '\t' || input[currentPosition] == '\n' || input[currentPosition] == '\r';
        }

        private bool IsWhitespaceOrEnd(int currentPosition)
        {
            return currentPosition >= input.Length || IsWhitespace(currentPosition);
        }

        private List<Token> ReadTokens()
        {
            List<Token> tokens = [];

            // Token begin position
            int tokenBeginPosition = position;          
            
            // Token text
            StringBuilder tokenText = new();

            while (!IsWhitespaceOrEnd(position))
            {             
                switch (input[position])
                {
                    case '\\':
                        tokenText.Append(ScanEscapeSequence());
                        break;

                    case '\'':
                    case '\"':
                        tokens.Add(ScanQuoted(input[position]));
                        tokenBeginPosition = position + 1;
                        break;

                    default:
                        tokenText.Append(input[position]);
                        List<Token> newTokens = TryExtractToken(tokenBeginPosition, tokenText.ToString(), IsWhitespaceOrEnd(position + 1), 0);
                        if (newTokens.Count > 0)
                        {
                            tokens.AddRange(newTokens);
                            tokenBeginPosition = position + 1;
                            tokenText = new();
                        }
                        break;
                }
                position++;
            }

            if (tokenText.Length > 0)
            {
                tokens.Add(new Token(tokenBeginPosition..position, tokenText.ToString(), TokenKind.Value));
            }
            return tokens;
        }

        private List<Token> TryExtractToken(int beginPosition, string text, bool surroundedByWhitespace, int recusionDepth)
        {
            if (recusionDepth > 1)
            {
                throw new InternalException("Internal error: Stackoverflow. Please report this error.");
            }

            List<Token> tokens = [];
           
            if (surroundedByWhitespace && whitespaceTokens.TryGetValue(text.ToLower(), out TokenKind whiteSpaceTokenKind))
            {
                tokens.Add(new Token(beginPosition..(beginPosition + text.Length), text, whiteSpaceTokenKind));
                return tokens;
            }

            int tokenLength = 1;
            while (tokenLength <= noWhitespaceTokenMaxLength && tokenLength <= text.Length)
            {
                string tokenText = text[^tokenLength..^0].ToLower();

                if (noWhitespaceTokens.TryGetValue(tokenText, out TokenKind tokenKind))
                {
                    return ExtractTokens(beginPosition, text, tokenText, tokenLength, tokenKind, recusionDepth, tokens);
                }
                tokenLength++;
            }

            return tokens;
        }

        private List<Token> ExtractTokens(int beginPosition, string text, string tokenText, int tokenLength, TokenKind tokenKind, int recusionDepth, List<Token> tokens)
        {
            if (!IsWhitespaceOrEnd(beginPosition + text.Length) &&
                noWhitespaceTokens.TryGetValue(tokenText + input[beginPosition + text.Length], out TokenKind realTokenKind))
            {
                tokenLength++;
                position++;
                tokenKind = realTokenKind;
                tokenText += input[beginPosition + text.Length];
                text += input[beginPosition + text.Length];
            }

            if (text.Length - tokenLength > 0)
            {
                List<Token> potentialTokens = TryExtractToken(beginPosition, text[..(text.Length - tokenLength)], true, recusionDepth + 1);
                if (potentialTokens.Count > 0)
                {
                    tokens.AddRange(potentialTokens);
                }
                else
                {
                    tokens.Add(new Token(beginPosition..(beginPosition + text.Length - tokenLength), text[..^tokenLength], TokenKind.Value));
                }
            }

            tokens.Add(new Token((beginPosition + text.Length - tokenLength)..(beginPosition + text.Length), tokenText, tokenKind));
            return tokens;
        }

        private Token ScanQuoted(char quoteChar)
        {
            int tokenBeginPosition = position;
            StringBuilder tokenText = new();

            position++;
            while (position < input.Length)
            {
                if (input[position] == '\\')
                {
                    tokenText.Append(ScanEscapeSequence());
                    position++;
                }
                else if (input[position] == quoteChar)
                {
                    return new Token(tokenBeginPosition..(position), tokenText.ToString(), TokenKind.Value);
                }
                else
                {
                    tokenText.Append(input[position]);
                    position++;
                }
            }
            throw new SyntaxException($"Expected {quoteChar} got end.", (tokenBeginPosition)..(position));
        }

        private char ScanEscapeSequence()
        {
            position++;

            if (IsWhitespaceOrEnd(position))
            {
                throw new SyntaxException("Expected escape sequence got whitespace or end.", (position - 1)..(position));
            }

            char characterCode = input[position];

            return characterCode switch
            {
                // Marks \ " ' as non keywords
                '\\' or '\"' or '\'' => characterCode,
                // tab
                't' => '\t',
                // new line
                'n' => '\n',                 
                // carriage return
                'r' => '\r',
                // default case
                _ => throw new SyntaxException($"Escape Sequence \"\\{characterCode}\" is unknown.", (position - 1)..position),
            };
        }
    }
}
