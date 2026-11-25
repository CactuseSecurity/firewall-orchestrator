using System.Net;
using System.Net.Http.Headers;

namespace FWO.Ai.Client.AgUi;

/// <summary>
/// Creates the exceptions of runs that could not be done. The messages never contain the access token.
/// </summary>
internal static class AgUiFailures
{
    /// <summary>
    /// Throws if the harness refused the run, mapping the HTTP status to the failure.
    /// </summary>
    /// <param name="response">Response of the harness</param>
    /// <param name="now">Current time, to compute the wait time of a Retry-After date</param>
    /// <exception cref="AgentClientException">The response status is no success.</exception>
    internal static void ThrowIfRefused(HttpResponseMessage response, DateTimeOffset now)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        HttpStatusCode statusCode = response.StatusCode;
        throw new AgentClientException(ToFailure(statusCode), $"The AI harness refused the run with HTTP status {(int)statusCode}.",
            statusCode, GetRetryAfter(response.Headers.RetryAfter, now));
    }

    /// <summary>
    /// Returns the failure an HTTP status of the harness stands for.
    /// </summary>
    internal static AgentClientFailure ToFailure(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => AgentClientFailure.Unauthorized,
            HttpStatusCode.Forbidden => AgentClientFailure.Forbidden,
            HttpStatusCode.TooManyRequests => AgentClientFailure.RateLimited,
            HttpStatusCode.RequestEntityTooLarge => AgentClientFailure.RequestTooLarge,
            HttpStatusCode.NotFound or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                => AgentClientFailure.Unavailable,
            _ => AgentClientFailure.ServerError
        };
    }

    /// <summary>
    /// Returns the time to wait from a Retry-After header, given as delay or as date; null without the header.
    /// </summary>
    /// <param name="retryAfter">Retry-After header of the response</param>
    /// <param name="now">Current time, to compute the wait time of a date</param>
    internal static TimeSpan? GetRetryAfter(RetryConditionHeaderValue? retryAfter, DateTimeOffset now)
    {
        if (retryAfter?.Delta is TimeSpan delay)
        {
            return delay;
        }
        if (retryAfter?.Date is DateTimeOffset retryDate)
        {
            return retryDate > now ? retryDate - now : TimeSpan.Zero;
        }
        return null;
    }

    /// <summary>
    /// Creates the exception of a response that is no valid AG-UI event stream or ends without finishing the run.
    /// </summary>
    internal static AgentClientException Protocol(string message, Exception? innerException = null)
    {
        return new AgentClientException(AgentClientFailure.ProtocolError, message, innerException: innerException);
    }
}
