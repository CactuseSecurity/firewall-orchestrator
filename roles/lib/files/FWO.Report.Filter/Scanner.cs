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

        static List<Token> whitespaceTokens = new List<Token>();
        static List<Token> noWhitespaceTokens = new List<Token>();

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
                    whitespaceTokens.Add(new Token(0..0, tokenSyntax, tokenKind));
                }
                foreach (string tokenSyntax in validTokenSyntax.NoWhiteSpaceRequiered)
                {
                    noWhitespaceTokens.Add(new Token(0..0, tokenSyntax, tokenKind));
                }
            }

            whitespaceTokens.Sort((x, y) => x.Text.Length - y.Text.Length);
            noWhitespaceTokens.Sort((x, y) => x.Text.Length - y.Text.Length);
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
                tokens.Add(new Token(tokenBeginPosition..position, tokenText, TokenKind.Value));
            }

            return tokens;
        }

        private List<Token> TryExtractToken(int beginPosition, string potentialTokenText, bool surroundedByWhitespace = false)
        {
            List<Token> tokens = new List<Token>();
           
            if (surroundedByWhitespace == true)
            {
                foreach (Token validToken in whitespaceTokens)
                {
                    if (potentialTokenText == validToken.Text)
                    {
                        tokens.Add(new Token(beginPosition..(beginPosition + potentialTokenText.Length), potentialTokenText, validToken.Kind));
                        return tokens;
                    }
                }
            }

            foreach (Token validToken in noWhitespaceTokens)
            {
                if (potentialTokenText.EndsWith(validToken.Text))
                {
                    Token realToken = validToken;

                    if (!IsWhitespaceOrEnd(beginPosition + potentialTokenText.Length))
                    {
                        foreach (Token longerToken in noWhitespaceTokens)
                        {
                            if (longerToken != validToken && (potentialTokenText + input[beginPosition + potentialTokenText.Length]).EndsWith(longerToken.Text))
                            {
                                position++;
                                realToken = longerToken;
                                potentialTokenText += input[beginPosition + potentialTokenText.Length];
                                break;
                            }
                        }
                    }

                    if (potentialTokenText.Length - realToken.Text.Length > 0)
                    {
                        List<Token> potentialTokens = TryExtractToken(beginPosition, potentialTokenText[..(potentialTokenText.Length - realToken.Text.Length)], true);
                        if (potentialTokens.Count > 0)
                        {
                            tokens.AddRange(potentialTokens);
                        }
                        else
                        {
                            tokens.Add(new Token(beginPosition..(beginPosition + potentialTokenText.Length - realToken.Text.Length), potentialTokenText[..^realToken.Text.Length], TokenKind.Value));
                        }
                    }

                    tokens.Add(new Token((beginPosition + potentialTokenText.Length - realToken.Text.Length)..(beginPosition + potentialTokenText.Length), realToken.Text, realToken.Kind));
                    break;
                    //for (int i = potentialTokenText.Length - validToken.Text.Length; i > 0; i--)
                    //{
                    //    List<Token> potentialTokens = TryExtractToken(beginPosition, potentialTokenText[..i], true);
                    //    if (potentialTokens.Count > 0)
                    //    {
                    //        tokens.AddRange(potentialTokens);
                    //        if (!string.IsNullOrWhiteSpace(potentialTokenText[i..^validToken.Text.Length]))
                    //        {
                    //            tokens.Add(new Token((beginPosition + i)..(beginPosition + potentialTokenText.Length - validToken.Text.Length), potentialTokenText[i..^validToken.Text.Length], TokenKind.Value));
                    //        }
                    //        break;
                    //    }
                    //}

                    //if (tokens.Count == 0 && !string.IsNullOrWhiteSpace(potentialTokenText[..^validToken.Text.Length]))
                    //{
                    //    tokens.Add(new Token(beginPosition..(beginPosition + potentialTokenText.Length - validToken.Text.Length), potentialTokenText[..^validToken.Text.Length], TokenKind.Value));
                    //}

                    //tokens.Add(new Token((beginPosition + potentialTokenText.Length - validToken.Text.Length)..(beginPosition + potentialTokenText.Length), validToken.Text, realTokenKind));

                    //return tokens;
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
