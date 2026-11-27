namespace FWO.Basics.Exceptions
{
    /// <summary>
    /// Indicates that a generated document, e.g. an export file, exceeded the maximum size in bytes supported by the
    /// operation. This is an expected validation condition and must be localized by the UI.
    /// </summary>
    public sealed class SizeLimitExceededException : Exception
    {
        /// <summary>
        /// Creates an exception for a document exceeding the specified size.
        /// </summary>
        /// <param name="limitBytes">Maximum supported size in bytes.</param>
        public SizeLimitExceededException(long limitBytes)
            : base($"The document is larger than the supported maximum of {limitBytes} bytes.")
        {
            LimitBytes = limitBytes;
        }

        /// <summary>
        /// Gets the maximum supported size in bytes.
        /// </summary>
        public long LimitBytes { get; }
    }
}
