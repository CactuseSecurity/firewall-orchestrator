using System;

namespace FWO.Ui.Filter
{
    public enum TokenKind
    {
        Value,
        Source,
        Destination,
        BL, // (
        BR, // )
        And,
        Or,
        Not,
        EQ, // ==
        NEQ // !=
    }
}