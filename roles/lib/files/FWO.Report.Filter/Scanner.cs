using FWO.Report.Filter.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace FWO.Report.Filter
{
    public class Scanner
    {
        private string input;
        private int position;
        private const int lookAhead = 1;

        private static Dictionary<string, TokenKind> whitespaceTokens = new Dictionary<string, TokenKind>();
        private static Dictionary<string, TokenKind> noWhitespaceTokens = new Dictionary<string, TokenKind>();
        private static int noWhitespaceTokenMaxLength = 0;

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
            List<Token> tokens = new List<Token>();

            for (position = 0; position < input.Length; position++)
            {
                while ((position < input.Length && (input[position] == ' ' || input[position] == '\t' || input[position] == '\n' || input[position] == '\r')) == true)
                {
                    position++;
                }

                tokens.AddRange(ReadTokens());
            }

            return tokens;
        }

        private bool IsWhitespaceOrEnd(int currentPosition)
        {
            if (currentPosition >= input.Length || input[currentPosition] == ' ' || input[currentPosition] == '\t' || input[currentPosition] == '\n' || input[currentPosition] == '\r')
            {
                return true;
            }
            else
            {
                return false;
            }      
        }

        private List<Token> ReadTokens()
        {
            List<Token> tokens = new List<Token>();

            // Token begin position
            int tokenBeginPosition = position;          
            
            // Token text
            string tokenText = "";

            while (IsWhitespaceOrEnd(position) == false)
            {             
                switch (input[position])
                {
                    case '\\':
                        tokenText += ScanEscapeSequence();
                        break;

                    case '\'':
                    case '\"':
                        tokens.Add(ScanQuoted(input[position]));
                        tokenBeginPosition = position + 1;
                        break;

                    default:
                        tokenText += input[position];

                        List<Token> newTokens = TryExtractToken(tokenBeginPosition, tokenText, IsWhitespaceOrEnd(position + 1), 0);

                        if (newTokens.Count > 0)
                        {
                            tokens.AddRange(newTokens);
                            tokenBeginPosition = position + 1;
                            tokenText = "";
                        }
                        break;
                }

                position++;
            }

            if (tokenText != "")
            {
                tokens.Add(new Token(tokenBeginPosition..position, tokenText, TokenKind.Value));
            }

            return tokens;
        }

        private List<Token> TryExtractToken(int beginPosition, string text, bool surroundedByWhitespace, int recusionDepth)
        {
            if (recusionDepth > 1) {
                throw new Exception("Internal error: Stackoverflow. Please report this error.");
            }

            List<Token> tokens = new List<Token>();
           
            if (surroundedByWhitespace == true)
            {
                if (whitespaceTokens.TryGetValue(text.ToLower(), out TokenKind tokenKind))
                {
                    tokens.Add(new Token(beginPosition..(beginPosition + text.Length), text, tokenKind));
                    return tokens;
                }
            }

            for (int tokenLength = 1; tokenLength <= noWhitespaceTokenMaxLength && tokenLength <= text.Length; tokenLength++)
            {
                string tokenText = text[^tokenLength..^0].ToLower();

                if (noWhitespaceTokens.TryGetValue(tokenText, out TokenKind tokenKind))
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
            }

            return tokens;
        }

        private Token ScanQuoted(char quoteChar)
        {
            int tokenBeginPosition = position;
            string tokenText = "";

            position++;

            while (position < input.Length)
            {
                if (input[position] == '\\')
                {
                    tokenText += ScanEscapeSequence();
                    position++;
                }

                else if (input[position] == quoteChar)
                {
                    return new Token(tokenBeginPosition..(position), tokenText, TokenKind.Value);
                }

                else
                {
                    tokenText += input[position];
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
