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

            // Detect Keywordss
            bool detectKeywords = true;

            while (IsWhitespaceOrEnd(position) == false)
            { 
                switch (input[position])
                {
                    case '\\':
                        tokenText += HandleEscapeSequence(input[position]);
                        position++;
                        break;

                    case '\'':
                    case '\"':
                        detectKeywords = !detectKeywords;
                        position++;
                        break;
                }

                if (IsWhitespaceOrEnd(position))
                    break;

                tokenText += input[position];

                if (detectKeywords == true)
                {
                    bool surroundedByWhitespace = false;

                    if (IsWhitespaceOrEnd(position + 1))
                        surroundedByWhitespace = true;

                    List<Token> newTokens = TryExtractToken(tokenBeginPosition, tokenText, surroundedByWhitespace);

                    if (newTokens.Count > 0)
                    {
                        tokens.AddRange(newTokens);
                        tokenBeginPosition = position + 1;
                        tokenText = "";
                    }
                }

                position++;
            }

            if (tokenText != "")
            {
                tokens.Add(new Token(tokenBeginPosition, tokenText, tokenKind));
            }

            return tokens;
        }

        private char HandleEscapeSequence(char characterCode)
        {
            position++;

            if (IsWhitespaceOrEnd(position))
            {
                throw new SyntaxException("Expected escape sequence got whitespace or end.", (position-1)..^(position - 1));
            }

            switch (characterCode)
            {
                // Marks \ " ' as non keywords
                case '\\':
                case '\"':
                case '\'':
                    return characterCode;

                // tab
                case 't':
                    return '\t';

                // new line
                case 'n':
                    return '\n';
                
                // carriage return
                case 'r':
                    return '\r';

                default:
                    throw new SyntaxException($"Escape Sequence \"\\{characterCode}\" is unknown.", (position-1)..^position);
            }
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
                            tokens.Add(new Token(beginPosition, potentialToken, tokenKind));
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
                                potentialTokens.Add(new Token(beginPosition, potentialToken.Substring(0, potentialToken.Length - validToken.Length), TokenKind.Value));
                            }
                            tokens.AddRange(potentialTokens);
                        }

                        tokens.Add(new Token(beginPosition + potentialToken.Length - validToken.Length, validToken, tokenKind));

                        return tokens;
                    }
                }
            }

            return tokens;
        }
    }
}
