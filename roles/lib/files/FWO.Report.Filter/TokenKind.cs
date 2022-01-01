using System;

namespace FWO.Report.Filter
{
    public enum TokenKind
    {
        Value,
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
        Time,
        ReportType,
        Remove,
        RecertDisplay,
        FullText,
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