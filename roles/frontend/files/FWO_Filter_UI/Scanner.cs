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
                SkipWhitespaces();

                Tokens.AddRange(ReadToken());
            }

            return Tokens;
        }

        private void SkipWhitespaces()
        {
            while (IsWhitespaceOrEnd() == false)
            {
                position++;
            }
        }

        private bool IsWhitespaceOrEnd()
        {
            if (position < input.Length && (input[position] == ' ' || input[position] == '\t' || input[position] == '\n'))
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
            // Token position
            int tokenPosition = position;          
            
            // Token text
            string tokenText = "";
            while (IsWhitespaceOrEnd() == false)
            {
                switch (input[position])
                {
                    case '\\':
                        HandleEscapeSequence(input[++position]);
                        break;

                    case '\"':
                    case '\'':


                    default:
                        position++;
                        break;
                }

                tokenText += input[position];
                position++;
            }

            // Token kind
            TokenKind tokenKind;

            tokenText.Contains("src")

            switch (tokenText)
            {
                case "src":
                case "source":
                    tokenKind = TokenKind.Source;
                    break;

                case "dest":
                case "destination":
                    tokenKind = TokenKind.Destination;
                    break;

                case "(":
                    tokenKind = TokenKind.BL;
                    break;

                case ")":
                    tokenKind = TokenKind.BR;
                    break;

                case "or":
                case "|":
                case "||":
                    tokenKind = TokenKind.Or;
                    break;

                case "and":
                case "&":
                case "&&":
                    tokenKind = TokenKind.And;
                    break;

                case "=":
                case "==":
                case "eq":
                    tokenKind = TokenKind.EQ;
                    break;

                case "!=":
                case "neq":
                    tokenKind = TokenKind.NEQ;
                    break;

                default:
                    tokenKind = TokenKind.Text;
                    break;
            }

            return new Token(tokenPosition, tokenText, tokenKind);
        }

        private char HandleEscapeSequence(char characterCode)
        {
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
                    break;
            }
        }
    }
}
