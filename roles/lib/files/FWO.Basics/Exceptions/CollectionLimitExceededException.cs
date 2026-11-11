namespace FWO.Basics.Exceptions
{
    /// <summary>
    /// Indicates that a loaded collection exceeded the maximum number of elements supported by the operation.
    /// This is an expected validation condition and must be localized by the UI.
    /// </summary>
    public sealed class CollectionLimitExceededException : Exception
    {
        /// <summary>
        /// Creates an exception for a collection exceeding the specified limit.
        /// </summary>
        /// <param name="limit">Maximum number of supported collection elements.</param>
        public CollectionLimitExceededException(int limit)
        {
            Limit = limit;
        }

        /// <summary>
        /// Gets the maximum number of supported collection elements.
        /// </summary>
        public int Limit { get; }
    }
}
