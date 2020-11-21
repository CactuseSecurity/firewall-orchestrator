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
            return tokenKind switch
            {
                TokenKind.Value => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { },
                    NoWhiteSpaceRequiered = new string[] { }
                },

                TokenKind.Time => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "time" },
                    NoWhiteSpaceRequiered = new string[] { }
                },

                TokenKind.Source => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "src", "source" },
                    NoWhiteSpaceRequiered = new string[] { }
                },

                TokenKind.Destination => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "dst", "dest", "destination" },
                    NoWhiteSpaceRequiered = new string[] { }
                },

                TokenKind.Action => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "action", "act", "enforce" },
                    NoWhiteSpaceRequiered = new string[] { }
                },

                TokenKind.Management => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "management", "mgmt", "manager", "mgm", "mgr" },
                    NoWhiteSpaceRequiered = new string[] { }
                },

                TokenKind.Gateway => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "gateway", "gw", "firewall", "fw", "dev" },
                    NoWhiteSpaceRequiered = new string[] { }
                },

                TokenKind.Service => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "svc", "service", "srv" },
                    NoWhiteSpaceRequiered = new string[] { }
                },

                TokenKind.BL => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { },
                    NoWhiteSpaceRequiered = new string[] { "(" }
                },

                TokenKind.BR => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { },
                    NoWhiteSpaceRequiered = new string[] { ")" }
                },

                TokenKind.And => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "and" },
                    NoWhiteSpaceRequiered = new string[] { "&", "&&" }
                },

                TokenKind.Or => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "or" },
                    NoWhiteSpaceRequiered = new string[] { "|", "||" }
                },

                TokenKind.Not => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "not" },
                    NoWhiteSpaceRequiered = new string[] { "!" }
                },

                TokenKind.EQ => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "eq" },
                    NoWhiteSpaceRequiered = new string[] { "=", "==", ":" }
                },

                TokenKind.NEQ => new TokenSyntax
                {
                    WhiteSpaceRequiered = new string[] { "neq" },
                    NoWhiteSpaceRequiered = new string[] { "!=", "!:" }
                },

                _ => throw new NotSupportedException($"No syntax found for token kind: {tokenKind}"),
            };
        }
    }
}
