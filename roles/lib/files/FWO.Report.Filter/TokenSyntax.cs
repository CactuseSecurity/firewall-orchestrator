namespace FWO.Report.Filter
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
                    WhiteSpaceRequiered: new string[] { "Disabled", "disabled", "inactive" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.SourceNegated => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "SourceNegated", "srcnegate", "sourcenegate", "sourcenegated", "source-negate", "source-negated" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.DestinationNegated => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "DestinationNegated", "dstnegate", "destinationnegate", "destinationnegated", "destination-negate", "destination-negated" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.ServiceNegated => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "ServiceNegated", "svcnegate", "servicenegate", "servicenegated", "service-negate", "service-negated" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Source => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "Source", "src", "source" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Destination => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "Destination", "dst", "dest", "destination" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Action => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "Action", "action", "act", "enforce" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Management => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "Management", "management", "mgmt", "manager", "mgm", "mgr" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Gateway => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "Gateway", "gateway", "gw", "firewall", "fw", "device", "dev" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.FullText => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "FullText", "fulltext", "full", "fulltextsearch", "fts", "text", "textsearch" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Service => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "Service", "svc", "service", "srv" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.DestinationPort => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "DestinationPort", "destinationport", "port", "dport", "dst_port", "dst-port", "dest-port", "destination-port", "dest_port", "destination_port" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Protocol => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "Protocol", "proto", "protocol" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.Remove => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "Remove", "remove" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.RecertDisplay => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "RecertDisplay", "recertdisplay", "recertdisp" },
                    NoWhiteSpaceRequiered: new string[] { }
                ),

                TokenKind.ReportType => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "ReportType", "type", "report-type", "reporttype" },
                    NoWhiteSpaceRequiered: new string[] {  }
                ),

                TokenKind.Time => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "Time", "time" },
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
                    NoWhiteSpaceRequiered: new string[] { "=", ":" }
                ),

                TokenKind.EEQ => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "eeq" },
                    NoWhiteSpaceRequiered: new string[] { "==", "::" }
                ),

                TokenKind.NEQ => new TokenSyntax
                (
                    WhiteSpaceRequiered: new string[] { "neq" },
                    NoWhiteSpaceRequiered: new string[] { "!=", "!:" }
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
