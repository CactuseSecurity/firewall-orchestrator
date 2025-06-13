namespace FWO.Report.Filter.Exceptions
{
    public class FilterException(string message, Range errorPosition) : Exception(message)
    {
        public readonly Range ErrorPosition = errorPosition;
    }
}
