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
                    WhiteSpaceRequiered: [],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Disabled => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["disabled", "inactive"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.SourceNegated => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["sourcenegated", "srcnegate", "sourcenegate", "source-negate", "source-negated"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.DestinationNegated => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["destinationnegated", "dstnegate", "destinationnegate", "destination-negate", "destination-negated"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.ServiceNegated => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["servicenegated", "svcnegate", "servicenegate", "service-negate", "service-negated"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Owner => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["owner", "application", "app"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.LastHit => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["lasthit", "last-hit", "last-used", "lastused", "last-usage", "lastusage", "last-use", "lastuse"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Unused => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["not-used-for-days", "unused", "unused-days", "not-used"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Source => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["source", "src"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Destination => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["destination", "dst", "dest"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Action => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["action", "act", "enforce"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Management => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["management", "mgmt", "manager", "mgm", "mgr"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Gateway => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["gateway", "gw", "firewall", "fw", "device", "dev"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.FullText => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["fulltext", "full", "fulltextsearch", "fts", "text", "textsearch"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Service => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["service", "svc", "srv"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.DestinationPort => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["destinationport", "port", "dport", "dst_port", "dst-port", "dest-port", "destination-port", "dest_port", "destination_port"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Protocol => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["protocol", "proto"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Remove => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["remove"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.RecertDisplay => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["recertdisplay", "recertdisp"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.ReportType => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["reporttype", "type", "report-type"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.Time => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["time"],
                    NoWhiteSpaceRequiered: []
                ),

                TokenKind.BL => new TokenSyntax
                (
                    WhiteSpaceRequiered: [],
                    NoWhiteSpaceRequiered: ["("]
                ),

                TokenKind.BR => new TokenSyntax
                (
                    WhiteSpaceRequiered: [],
                    NoWhiteSpaceRequiered: [")"]
                ),

                TokenKind.And => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["and"],
                    NoWhiteSpaceRequiered: ["&", "&&"]
                ),

                TokenKind.Or => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["or"],
                    NoWhiteSpaceRequiered: ["|", "||"]
                ),

                TokenKind.Not => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["not"],
                    NoWhiteSpaceRequiered: ["!"]
                ),

                TokenKind.EQ => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["eq"],
                    NoWhiteSpaceRequiered: ["="]
                ),

                TokenKind.EEQ => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["eeq"],
                    NoWhiteSpaceRequiered: ["=="]
                ),

                TokenKind.NEQ => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["neq"],
                    NoWhiteSpaceRequiered: ["!="]
                ),

                TokenKind.LSS => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["lss"],
                    NoWhiteSpaceRequiered: ["<"]
                ),

                TokenKind.GRT => new TokenSyntax
                (
                    WhiteSpaceRequiered: ["grt"],
                    NoWhiteSpaceRequiered: [">"]
                ),

                _ => throw new NotSupportedException($"No syntax found for token kind: {tokenKind}"),
            };
        }
    }
}
