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

            Position = SkipWhitespaces(Input, Position);

            while (Position < Input.Length)
            {
                (Position, Token) = ReadToken(Input, Position);
                Tokens.Add(Token);
                Position = SkipWhitespaces(Input, Position);
            }

            return Tokens;
        }

        private static int SkipWhitespaces(string Input, int Position)
        {
            while (Position < Input.Length && (Input[Position] == ' ' || Input[Position] == '\t' || Input[Position] == '\n'))
                Position++;

            return Position;
        }

        private static (int, Token) ReadToken(string Input, int Position)
        {
            Token Token = new Token();
            Token.Position = Position;

            string Text = "";

            while (Position < Input.Length && (Input[Position] != ' ' && Input[Position] != '\t' && Input[Position] != '\n'))
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

                case "=":
                case "==":
                case "eq":
                    Kind = TokenKind.EQ;
                    break;

                case "!=":
                case "neq":
                    Kind = TokenKind.NEQ;
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
