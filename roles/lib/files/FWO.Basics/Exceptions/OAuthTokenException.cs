using FWO.Basics.OAuth;

namespace FWO.Basics.Exceptions
{
    /// <summary>
    /// An OAuth 2.0 token request failed. The message and the properties never contain the client secret or a token;
    /// the error description is the token endpoint's own text, shortened.
    /// </summary>
    public class OAuthTokenException : Exception
    {
        /// <summary>
        /// Creates the exception.
        /// </summary>
        /// <param name="failure">Why the request failed</param>
        /// <param name="message">Message without client secret or token</param>
        /// <param name="statusCode">HTTP status of the answer; null if there was none</param>
        /// <param name="error">OAuth error code of the answer (field error), e.g. invalid_client; null if there was none</param>
        /// <param name="errorDescription">Error description of the answer (field error_description); null if there was none</param>
        /// <param name="innerException">Cause, e.g. the connection failure</param>
        public OAuthTokenException(OAuthTokenFailure failure, string message, int? statusCode = null, string? error = null,
            string? errorDescription = null, Exception? innerException = null) : base(message, innerException)
        {
            Failure = failure;
            StatusCode = statusCode;
            Error = error;
            ErrorDescription = errorDescription;
        }

        /// <summary>Why the request failed.</summary>
        public OAuthTokenFailure Failure { get; }

        /// <summary>HTTP status of the answer; null if the token endpoint did not answer.</summary>
        public int? StatusCode { get; }

        /// <summary>OAuth error code of the answer (field error), e.g. invalid_client; null if there was none.</summary>
        public string? Error { get; }

        /// <summary>Error description of the answer (field error_description), shortened; null if there was none.</summary>
        public string? ErrorDescription { get; }
    }
}
