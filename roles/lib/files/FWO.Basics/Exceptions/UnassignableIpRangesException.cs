namespace FWO.Basics.Exceptions
{
    /// <summary>
    /// Reports IP ranges that no configured network zone covers. Distinct from the general argument exceptions so
    /// that a caller can answer with a client error for this case alone and still log every other failure.
    /// </summary>
    public class UnassignableIpRangesException : Exception
    {
        public UnassignableIpRangesException(string message) : base(message) { }
        public UnassignableIpRangesException(string message, Exception innerException) : base(message, innerException) { }
    }
}
