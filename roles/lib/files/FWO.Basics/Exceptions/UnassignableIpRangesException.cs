namespace FWO.Basics.Exceptions
{
    /// <summary>
    /// Reports IP ranges that no configured network zone covers. Distinct from the general argument exceptions so
    /// that a caller can answer with a client error for this case alone and still log every other failure.
    /// </summary>
    public class UnassignableIpRangesException : Exception
    {
        /// <summary>
        /// Creates the exception from a message naming the ranges that could not be assigned.
        /// </summary>
        /// <param name="message">Message listing the unassignable IP ranges; it is returned to the caller.</param>
        public UnassignableIpRangesException(string message) : base(message) { }

        /// <summary>
        /// Creates the exception from a message naming the ranges that could not be assigned and the failure that
        /// caused it.
        /// </summary>
        /// <param name="message">Message listing the unassignable IP ranges; it is returned to the caller.</param>
        /// <param name="innerException">Failure that led to the ranges being unassignable.</param>
        public UnassignableIpRangesException(string message, Exception innerException) : base(message, innerException) { }
    }
}
