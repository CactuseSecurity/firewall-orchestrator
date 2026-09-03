using FWO.Data.Middleware;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Outcome of a token refresh attempt, separating a refusal that settles the session
    /// from a failure that only says this attempt did not complete.
    /// </summary>
    /// <param name="Tokens">The refreshed pair, or null when the attempt produced none.</param>
    /// <param name="Retryable">
    /// True when the failure says nothing about the refresh token, because the middleware or
    /// the API could not be reached or the call was abandoned. The stored pair has to be kept
    /// in that case: discarding a token that is still good turns a momentary outage into a
    /// forced re-login for every session.
    /// </param>
    public record TokenRefreshResult(TokenPair? Tokens, bool Retryable)
    {
        /// <summary>
        /// No pair was obtained and the reason is the middleware's verdict on this token, so
        /// the session is over.
        /// </summary>
        public static TokenRefreshResult Settled { get; } = new(null, false);

        /// <summary>
        /// No pair was obtained, but nothing was learned about the refresh token, so the
        /// stored pair stays and the next attempt can use it.
        /// </summary>
        public static TokenRefreshResult Retry { get; } = new(null, true);
    }
}
