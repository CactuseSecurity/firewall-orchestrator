using System;
using System.Collections.Generic;
using System.Linq;

namespace FWO.Ui.Filter
{
    public class Scanner
    {
        private string input;
        private int position;

        public Scanner(string input)
        {
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
            TokenKind tokenKind = TokenKind.Text;

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
                    switch (tokenText)
                    {
                        case string _ when tokenText.EndsWith("src"):
                        case string _ when tokenText.EndsWith("source"):
                            tokenKind = TokenKind.Source;
                            break;

                        case string _ when tokenText.EndsWith("dst"):
                        case string _ when tokenText.EndsWith("dest"):
                        case string _ when tokenText.EndsWith("destination"):
                            tokenKind = TokenKind.Destination;
                            break;

                        case string _ when tokenText.EndsWith("("):
                            tokenKind = TokenKind.BL;
                            break;

                        case string _ when tokenText.EndsWith(")"):
                            tokenKind = TokenKind.BR;
                            break;

                        case string _ when tokenText.EndsWith("or"):
                        case string _ when tokenText.EndsWith("|"):
                        case string _ when tokenText.EndsWith("||"):
                            tokenKind = TokenKind.Or;
                            break;

                        case string _ when tokenText.EndsWith("and"):
                        case string _ when tokenText.EndsWith("&"):
                        case string _ when tokenText.EndsWith("&&"):
                            tokenKind = TokenKind.And;
                            break;

                        case string _ when tokenText.EndsWith(":"):
                        case string _ when tokenText.EndsWith("="):
                        case string _ when tokenText.EndsWith("=="):
                        case string _ when tokenText.EndsWith("eq"):
                            tokenKind = TokenKind.EQ;
                            break;

                        case string _ when tokenText.EndsWith("!="):
                        case string _ when tokenText.EndsWith("neq"):
                            tokenKind = TokenKind.NEQ;
                            break;

                        default:
                            tokenKind = TokenKind.Text;
                            break;
                    }

                    if (tokenKind != TokenKind.Text)
                    {
                        tokens.Add(new Token(tokenBeginPosition, tokenText, tokenKind));
                        tokenText = "";
                        tokenKind = TokenKind.Text;
                        tokenBeginPosition = position + 1;
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
                throw new NotSupportedException("Expected escape sequence got whitespace or end.");
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
                    throw new NotSupportedException($"Escape Sequence \"\\{characterCode}\" is unknown.");
            }
        }
    }
}
