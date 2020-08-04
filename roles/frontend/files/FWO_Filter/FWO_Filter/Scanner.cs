using System;
using System.Collections.Generic;
using System.Linq;

namespace FWO_Filter
{
    public static class Scanner
    {
        public static List<Token> Scan(string Input)
        {
            int Position = 0;
            Token Token;
            List<Token> Tokens = new List<Token>();

            while (Position < Input.Length)
            {
                Position = SkipWhitespaces(Input, Position);
                (Position, Token) = ReadToken(Input, Position);
                Tokens.Add(Token);
            }

            return Tokens;
        }

        private static int SkipWhitespaces(string Input, int Position)
        {
            while (Input[Position] == ' ' || Input[Position] == '\t')
            {
                Position++;
            }

            return Position;
        }

        private static (int, Token) ReadToken(string Input, int Position)
        {
            Token Token = new Token();
            Token.Position = Position;

            string Text = "";

            while (Position < Input.Length && Input[Position] != ' ' && Input[Position] != '\t')
            {
                Text += Input[Position];
                Position++;
            }

            Token.Text = Text;
            
            TokenKind Kind;

            switch (Text)
            {
                case "src":
                case "source":
                    Kind = TokenKind.Source;
                    break;

                case "dest":
                case "destination":
                    Kind = TokenKind.Destination;
                    break;

                case "(":
                    Kind = TokenKind.BL;
                    break;

                case ")":
                    Kind = TokenKind.BR;
                    break;

                case "or":
                case "|":
                case "||":
                    Kind = TokenKind.Or;
                    break;

                case "and":
                case "&":
                case "&&":
                    Kind = TokenKind.And;
                    break;

                default:
                    Kind = TokenKind.Text;
                    break;
            }

            Token.Kind = Kind;

            return (Position, Token);
        }
    }
}
