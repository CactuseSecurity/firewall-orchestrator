using FWO.Ui.Filter.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace FWO.Ui.Filter
{
    public class Scanner
    {
        private string input;
        private int position;

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

                tokens.AddRange(ReadToken());
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

        private List<Token> ReadToken()
        {
            List<Token> tokens = new List<Token>();

            // Token begin position
            int tokenBeginPosition = position;          
            
            // Token text
            string tokenText = "";

            // Token kind
            TokenKind tokenKind = TokenKind.Value;

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

                        List<Token> newTokens = TryExtractToken(tokenBeginPosition, tokenText, IsWhitespaceOrEnd(position + 1));

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
                tokens.Add(new Token(tokenBeginPosition..^(position-1), tokenText, tokenKind));
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
                            tokens.Add(new Token(beginPosition..^position, potentialToken, tokenKind));
                            return tokens;
                        }
                    }
                }

                foreach (string validToken in validTokenSyntax.NoWhiteSpaceRequiered)
                {
                    if (potentialToken.EndsWith(validToken))
                    {
                        if (potentialToken.Length - validToken.Length > 0)
                        {
                            List<Token> potentialTokens = TryExtractToken(beginPosition, potentialToken.Substring(0, potentialToken.Length - validToken.Length), true);
                            if (potentialTokens.Count == 0)
                            {
                                potentialTokens.Add(new Token(beginPosition..^(beginPosition + potentialToken.Length - validToken.Length), potentialToken.Substring(0, potentialToken.Length - validToken.Length), TokenKind.Value));
                            }
                            tokens.AddRange(potentialTokens);
                        }

                        tokens.Add(new Token((beginPosition + potentialToken.Length - validToken.Length)..^position, validToken, tokenKind));

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

            throw new SyntaxException($"Expected {quoteChar} got end.", (tokenBeginPosition)..^(position-1));
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
