﻿namespace FWO.Report.Filter
{
    class TokenSyntax
    {
        public string[] WhiteSpaceRequiered { get; private set; }
        public string[] NoWhiteSpaceRequiered { get; private set; }

        public TokenSyntax(string[] WhiteSpaceRequiered, string[] NoWhiteSpaceRequiered)
        {
            this.WhiteSpaceRequiered = WhiteSpaceRequiered;
            this.NoWhiteSpaceRequiered = NoWhiteSpaceRequiered;
        }

        public static TokenSyntax Get(TokenKind tokenKind)
        {
            return tokenKind switch
            {
                TokenKind.Value => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Disabled => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "disabled", "inactive" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.SourceNegated => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "sourcenegated", "srcnegate", "sourcenegate", "source-negate", "source-negated" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.DestinationNegated => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "destinationnegated", "dstnegate", "destinationnegate", "destination-negate", "destination-negated" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.ServiceNegated => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "servicenegated", "svcnegate", "servicenegate", "service-negate", "service-negated" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Source => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "source", "src" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Destination => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "destination", "dst", "dest"  },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Action => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "action", "act", "enforce" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Management => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "management", "mgmt", "manager", "mgm", "mgr" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Gateway => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "gateway", "gw", "firewall", "fw", "device", "dev" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.FullText => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "fulltext", "full", "fulltextsearch", "fts", "text", "textsearch" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Service => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "service", "svc", "srv" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.DestinationPort => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "destinationport", "port", "dport", "dst_port", "dst-port", "dest-port", "destination-port", "dest_port", "destination_port" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Protocol => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "protocol", "proto" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Remove => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "remove" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.RecertDisplay => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "recertdisplay", "recertdisp" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.ReportType => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "reporttype", "type", "report-type"  },
                    NoWhiteSpaceRequiered: new string[] {  }
                ),

                TokenKind.Time => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "time" },
                    NoWhiteSpaceRequiered: new string[] {  }
                ),

                TokenKind.BL => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { },
                    NoWhiteSpaceRequiered: new string[] { "(" }
                ),

                TokenKind.BR => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { },
                    NoWhiteSpaceRequiered: new string[] { ")" }
                ),

                TokenKind.And => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "and" },
                    NoWhiteSpaceRequiered: new string[] { "&", "&&" }
                ),

                TokenKind.Or => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "or" },
                    NoWhiteSpaceRequiered: new string[] { "|", "||" }
                ),

                TokenKind.Not => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "not" },
                    NoWhiteSpaceRequiered: new string[] { "!" }
                ),

                TokenKind.EQ => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "eq" },
                    NoWhiteSpaceRequiered: new string[] { "=" }
                ),

                TokenKind.EEQ => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "eeq" },
                    NoWhiteSpaceRequiered: new string[] { "==" }
                ),

                TokenKind.NEQ => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "neq" },
                    NoWhiteSpaceRequiered: new string[] { "!=" }
                ),

                TokenKind.LSS => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "lss" },
                    NoWhiteSpaceRequiered: new string[] { "<" }
                ),

                TokenKind.GRT => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "grt" },
                    NoWhiteSpaceRequiered: new string[] { ">" }
                ),

                _ => throw new NotSupportedException($"No syntax found for token kind: {tokenKind}"),
            };
        }
    }
}
