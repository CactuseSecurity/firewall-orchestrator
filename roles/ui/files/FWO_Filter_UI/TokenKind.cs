using System;

namespace FWO.Ui.Filter
{
    public enum TokenKind
    {
        Value,
        Source,
        Destination,
        Service,
        Protocol,
        DestinationPort,
        Action,
        Management,
        Gateway,
        Time,
        FullText,
        BL, // (
        BR, // )
        And,
        Or,
        Not,
        EQ, // ==
        NEQ // !=
    }
}