using System.Net;

namespace FWO.Ai.Client;

/// <summary>
/// Why a run could not be done.
/// </summary>
public enum AgentClientFailure
{
    /// <summary>
    /// The access token was missing, unusable as bearer token, or rejected by the harness as invalid or expired (401);
    /// logging in again solves all of them.
    /// </summary>
    Unauthorized,

    /// <summary>The user may not use the agent (403).</summary>
    Forbidden,

    /// <summary>Too many runs of the user (429), see <see cref="AgentClientException.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary>The request was too large (413), e.g. a conversation too long to be sent again.</summary>
    RequestTooLarge,

    /// <summary>The harness is not installed or not reachable (connection error, 404, 502, 503, 504).</summary>
    Unavailable,

    /// <summary>The harness failed, e.g. because no AI provider is configured (500 or another status).</summary>
    ServerError,

    /// <summary>The run took longer than the run timeout of the client.</summary>
    Timeout,

    /// <summary>The response was not a valid AG-UI event stream, or ended without finishing the run.</summary>
    ProtocolError
}

/// <summary>
/// A run could not be done: the harness refused it, could not be reached or broke the protocol.
/// </summary>
public sealed class AgentClientException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="failure">Why the run could not be done</param>
    /// <param name="message">Description of the failure, for the log</param>
    /// <param name="statusCode">HTTP status of the response, if there was one</param>
    /// <param name="retryAfter">Time to wait before the next run, if the harness sent one</param>
    /// <param name="innerException">Cause of the failure</param>
    public AgentClientException(AgentClientFailure failure, string message, HttpStatusCode? statusCode = null,
        TimeSpan? retryAfter = null, Exception? innerException = null) : base(message, innerException)
    {
        Failure = failure;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    /// <summary>Why the run could not be done.</summary>
    public AgentClientFailure Failure { get; }

    /// <summary>HTTP status of the response, if there was one.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Time to wait before the next run, if the harness sent one (Retry-After).</summary>
    public TimeSpan? RetryAfter { get; }
}
