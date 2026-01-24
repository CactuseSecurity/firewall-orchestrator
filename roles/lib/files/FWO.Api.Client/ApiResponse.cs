namespace FWO.Api.Client
{
    /// <summary>
    /// Wraps API data with optional error information to avoid exceptions in callers.
    /// </summary>
    public sealed class ApiResponse<ResponseType>
    {
        /// <summary>
        /// The successfully deserialized result, if available.
        /// </summary>
        public ResponseType? Result { get; }
        /// <summary>
        /// Errors returned by the API, if any.
        /// </summary>
        public string[]? Errors { get; }
        /// <summary>
        /// Indicates whether the response contains errors.
        /// </summary>
        public bool HasErrors => Errors is { Length: > 0 };

        /// <summary>
        /// Creates a successful response wrapper.
        /// </summary>
        internal ApiResponse(ResponseType result)
        {
            Result = result;
        }

        /// <summary>
        /// Creates a response wrapper containing error messages.
        /// </summary>
        internal ApiResponse(params string[] errors)
        {
            Errors = errors;
        }
    }
}
