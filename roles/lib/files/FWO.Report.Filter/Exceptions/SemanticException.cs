namespace FWO.Report.Filter.Exceptions
{
    public class SemanticException(string message, Range errorPosition) : FilterException(message, errorPosition)
    {
    }
}
