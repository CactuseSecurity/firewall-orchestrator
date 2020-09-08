namespace FWO_Filter
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
        EQ, // ==
        NEQ, // !=
    }
}