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
        private const int lookAhead = 2;

        public Scanner(string input)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
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
            if (currentPosition >= input.Length || input[currentPosition] == ' ' || input[currentPosition] == '\t' || input[currentPosition] == '\n' || input[position] == '\r')
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
                    case '\'':
                    case '\"':
                        tokens.Add(ScanQuoted(input[position]));
                        tokenBeginPosition = position + 1;
                        break;

                    default:
                        bool newTokensAdded = false;
                        var a = "abcde"[1..2];
                        var b = "abcde"[1..^2];
                        var c = "abcde"[1..1];

                        for (int lookAheadPosition = Math.Min(position + lookAhead, input.Length - 1); lookAheadPosition >= position; lookAheadPosition--)
                        {
                            string test = input[position..(lookAheadPosition + 1)];

                            List<Token> newTokens = TryExtractToken(tokenBeginPosition, 
                                tokenText + input[position..(lookAheadPosition + 1)],
                                IsWhitespaceOrEnd(lookAheadPosition + 1));

                            if (newTokens.Count > 0)
                            {
                                newTokensAdded = true;
                                tokens.AddRange(newTokens);
                                position = lookAheadPosition;
                                tokenBeginPosition = position + 1;
                                tokenText = "";
                                break;
                            }
                        }
                        if (!newTokensAdded)
                        {
                            tokenText += input[position];
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

        private List<Token> TryExtractToken(int beginPosition, string potentialToken, bool surroundedByWhitespace = false)
        {
            List<Token> tokens = new List<Token>();

            foreach (TokenKind tokenKind in Enum.GetValues(typeof(TokenKind)))
            {
                TokenSyntax validTokenSyntax = TokenSyntax.Get(tokenKind);

                if (surroundedByWhitespace == true)
                {
                    foreach (string validToken in validTokenSyntax.WhiteSpaceRequiered)
                    {
                        if (potentialToken == validToken)
                        {
                            tokens.Add(new Token(beginPosition..(beginPosition + potentialToken.Length), potentialToken, tokenKind));
                            return tokens;
                        }
                    }
                }

                foreach (string validToken in validTokenSyntax.NoWhiteSpaceRequiered)
                {
                    if (potentialToken.EndsWith(validToken))
                    {
                        TokenKind realTokenKind = tokenKind;

                        if (potentialToken.Length - validToken.Length > 0)
                        {
                            List<Token> potentialTokens = TryExtractToken(beginPosition, potentialToken.Substring(0, potentialToken.Length - validToken.Length), true);
                            if (potentialTokens.Count == 0)
                            {
                                tokens.Add(new Token(beginPosition..(beginPosition + potentialToken.Length - validToken.Length), potentialToken[..^validToken.Length], TokenKind.Value));
                            }
                            else
                            {
                                //if (potentialTokens.Last().Kind == TokenKind.Not && tokenKind == TokenKind.EQ)
                                //{
                                //    potentialTokens.RemoveAt(potentialTokens.Count - 1);
                                //    realTokenKind = TokenKind.NEQ;
                                //}

                                tokens.AddRange(potentialTokens);
                            }
                        }

                        tokens.Add(new Token((beginPosition + potentialToken.Length - validToken.Length)..(beginPosition + potentialToken.Length), validToken, realTokenKind));

                        return tokens;
                    }
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
                    return new Token(tokenBeginPosition..^(position), tokenText, TokenKind.Value);
                }

                else
                {
                    tokenText += input[position];
                    position++;
                }
            }

            throw new SyntaxException($"Expected {quoteChar} got end.", (tokenBeginPosition)..^(position - 1));
        }

        private char ScanEscapeSequence()
        {
            position++;

            if (IsWhitespaceOrEnd(position))
            {
                throw new SyntaxException("Expected escape sequence got whitespace or end.", (position - 1)..^(position - 1));
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
                _ => throw new SyntaxException($"Escape Sequence \"\\{characterCode}\" is unknown.", (position - 1)..^position),
            };
        }
    }
}
