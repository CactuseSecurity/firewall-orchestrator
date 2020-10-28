using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FWO.Ui.Filter
{
    class TokenSyntax
    {
        public string[] WhiteSpaceRequiered { get; private set; }
        public string[] NoWhiteSpaceRequiered { get; private set; }

        public static TokenSyntax Get(TokenKind tokenKind)
        {
            switch (tokenKind)
            {
                case TokenKind.Value:
                    return new TokenSyntax
                    {
                        WhiteSpaceRequiered = new string[] { },
                        NoWhiteSpaceRequiered = new string[] { }
                    };

                case TokenKind.Source:
                    return new TokenSyntax()
                    {
                        WhiteSpaceRequiered = new string[] {"src", "source"},
                        NoWhiteSpaceRequiered = new string[] {}
                    };

                case TokenKind.Destination:
                    return new TokenSyntax()
                    {
                        WhiteSpaceRequiered = new string[] { "dst", "dest", "destination" },
                        NoWhiteSpaceRequiered = new string[] { }
                    };

                case TokenKind.BL:
                    return new TokenSyntax()
                    {
                        WhiteSpaceRequiered = new string[] { },
                        NoWhiteSpaceRequiered = new string[] { "(" }
                    };

                case TokenKind.BR:
                    return new TokenSyntax()
                    {
                        WhiteSpaceRequiered = new string[] { },
                        NoWhiteSpaceRequiered = new string[] { ")" }
                    };

                case TokenKind.And:
                    return new TokenSyntax()
                    {
                        WhiteSpaceRequiered = new string[] { "and"},
                        NoWhiteSpaceRequiered = new string[] { "&", "&&" }
                    };

                case TokenKind.Or:
                    return new TokenSyntax()
                    {
                        WhiteSpaceRequiered = new string[] { "or" },
                        NoWhiteSpaceRequiered = new string[] { "|", "||" }
                    };

                case TokenKind.Not:
                    return new TokenSyntax()
                    {
                        WhiteSpaceRequiered = new string[] { "not" },
                        NoWhiteSpaceRequiered = new string[] { "!" }
                    };

                case TokenKind.EQ:
                    return new TokenSyntax()
                    {
                        WhiteSpaceRequiered = new string[] { "eq" },
                        NoWhiteSpaceRequiered = new string[] { "=", "==", ":" }
                    };

                case TokenKind.NEQ:
                    return new TokenSyntax()
                    {
                        WhiteSpaceRequiered = new string[] { "neq" },
                        NoWhiteSpaceRequiered = new string[] { "!=", "!:" }
                    };

                default:
                    throw new NotSupportedException($"No syntax found for token kind: {tokenKind}");
            }
        }
    }
}
