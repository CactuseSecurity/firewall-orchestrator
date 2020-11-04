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
            if (input == null)
                throw new ArgumentNullException("Filter input is null");

            if (input == "")
                throw new ArgumentException("Filter input is empty");

            this.input = input;
        }

        public List<Token> Scan()
        {
            List<Token> Tokens = new List<Token>();

            for (position = 0; position < input.Length; position++)
            {
                while ((position < input.Length && (input[position] == ' ' || input[position] == '\t' || input[position] == '\n')) == true)
                {
                    position++;
                }

                Tokens.AddRange(ReadToken());
            }
            return Tokens;
        }

        private bool IsWhitespaceOrEnd()
        {
            if (position >= input.Length || input[position] == ' ' || input[position] == '\t' || input[position] == '\n')
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

            while (IsWhitespaceOrEnd() == false)
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

                if (IsWhitespaceOrEnd())
                    break;

                tokenText += input[position];

                if (detectKeywords == true)
                {
                    List<Token> newTokens = TryExtractToken(tokenBeginPosition, tokenText);

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

            if (IsWhitespaceOrEnd())
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

        private List<Token> TryExtractToken(int beginPosition, string potentialToken)
        {
            List<Token> tokens = new List<Token>();

            foreach (TokenKind tokenKind in Enum.GetValues(typeof(TokenKind)))
            {
                TokenSyntax validTokenSyntax = TokenSyntax.Get(tokenKind);

                foreach (string validToken in validTokenSyntax.WhiteSpaceRequiered)
                {
                    if (potentialToken == validToken)
                    {
                        tokens.Add(new Token(beginPosition, potentialToken, tokenKind));
                        return tokens;
                    }
                }

                foreach (string validToken in validTokenSyntax.NoWhiteSpaceRequiered)
                {
                    if (potentialToken.EndsWith(validToken))
                    {
                        if (potentialToken.Length - validToken.Length > 0)
                        {
                            tokens.Add(new Token(beginPosition, potentialToken.Substring(0, potentialToken.Length - validToken.Length), TokenKind.Value));
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
