namespace FWO.Report.Filter.Exceptions
{
    public class SyntaxException(string message, Range errorPosition) : FilterException(message, errorPosition)
    {
    }
}
