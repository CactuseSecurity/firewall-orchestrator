using System;

namespace FWO.Ui.Filter
{
    public enum TokenKind
    {
        Value,
        Source,
        Destination,
        Service,
        Action,
        Time,
        BL, // (
        BR, // )
        And,
        Or,
        Not,
        EQ, // ==
        NEQ // !=
    }
}