namespace FWO.Report.Filter
{
    public enum TokenKind
    {
        Value,
        Owner,
        Disabled,
        SourceNegated,
        DestinationNegated,
        ServiceNegated,
        Source,
        Destination,
        Service,
        Protocol,
        DestinationPort,
        Action,
        Management,
        Gateway,
        Remove,
        ReportType,
        Time,
        RecertDisplay,
        FullText,
        LastHit,
        Unused,
        BL, // (
        BR, // )
        And,
        Or,
        Not,
        EEQ, // Exact equals
        EQ, // ==
        NEQ, // !=
        LSS, // <
        GRT, // >
    }
}