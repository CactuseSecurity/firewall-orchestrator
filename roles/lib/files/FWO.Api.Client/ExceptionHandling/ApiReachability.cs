using GraphQL.Client.Http;
using System.Net;

namespace FWO.Api.Client.ExceptionHandling
{
    /// <summary>
    /// Classifies the exceptions that mean "the API did not give a usable answer", so a
    /// caller does not have to know the GraphQL client library's exception taxonomy.
    /// </summary>
    /// <remarks>
    /// The two cases arrive as unrelated types, and only one of them derives from
    /// <see cref="HttpRequestException"/>. Catching that type alone silently misses every
    /// failure in which the API host answers but the API itself was never reached - which
    /// is what happens whenever Apache reports a proxy error for a service that is down or
    /// restarting.
    /// </remarks>
    public static class ApiReachability
    {
        /// <summary>
        /// Decides whether an exception from an API call says the API could not be reached,
        /// rather than saying something about the request that was sent.
        /// </summary>
        /// <param name="exception">Exception raised by an API call.</param>
        /// <returns>
        /// True when the call failed for a reason that is independent of its content, so the
        /// caller may retry it and must not report it as a rejection of the request.
        /// </returns>
        public static bool IndicatesUnreachableApi(Exception exception)
        {
            return exception switch
            {
                // No HTTP answer at all: connection refused or reset, name resolution
                // failure, or a TLS handshake the peer declined - a client certificate the
                // API rejects ends here, because that handshake never completes.
                HttpRequestException => true,
                // An answer arrived, but not from the API: the reverse proxy reports on its
                // own behalf that it could not serve the request. A 4xx is deliberately not
                // included - that is an answer about the request itself - and neither is a
                // plain 500, which means the API was reached and failed.
                GraphQLHttpRequestException graphQlException => IsProxyFailure(graphQlException.StatusCode),
                // The HttpClient timeout surfaces as a cancellation carrying a
                // TimeoutException. A cancellation requested by a caller's own token has no
                // such inner exception and is not an API failure.
                TaskCanceledException canceled => canceled.InnerException is TimeoutException,
                _ => false
            };
        }

        /// <summary>
        /// Whether a status code was produced by a reverse proxy that could not reach the
        /// service behind it, as opposed to by the service itself.
        /// </summary>
        /// <param name="statusCode">Status code the API host answered with.</param>
        /// <returns>True for the proxy's own "cannot serve this" codes.</returns>
        private static bool IsProxyFailure(HttpStatusCode statusCode)
        {
            return statusCode is HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;
        }
    }
}
