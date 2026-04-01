namespace FWO.Api.Client
{
    /// <summary>
    /// Supplies bearer tokens for API connections and can invalidate cached authentication state.
    /// </summary>
    public interface IAuthTokenProvider
    {
        /// <summary>
        /// Gets the current bearer token, minting or refreshing it if needed.
        /// </summary>
        /// <returns>A bearer token for the current connection context.</returns>
        string GetBearerToken();

        /// <summary>
        /// Invalidates any cached token state so the next retrieval forces a refresh.
        /// </summary>
        void Invalidate();
    }
}
