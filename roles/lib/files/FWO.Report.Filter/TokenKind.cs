﻿namespace FWO.Report.Filter
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
        Remove,
        RecertDisplay,
        FullText,
        BL, // (
        BR, // )
        And,
        Or,
        Not,
        EQ, // ==
        NEQ, // !=
        LSS, // <
        GRT, // >
    }
}