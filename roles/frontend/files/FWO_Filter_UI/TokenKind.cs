namespace FWO.Ui.Filter
{
    public enum TokenKind
    {
        Text, 
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