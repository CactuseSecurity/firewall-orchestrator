namespace FWO.Basics.OAuth
{
    /// <summary>
    /// Access token issued by an OAuth 2.0 token endpoint.
    /// A class rather than a record on purpose: the generated ToString of a record would print the token.
    /// </summary>
    public sealed class OAuthAccessToken
    {
        /// <summary>
        /// Creates the token.
        /// </summary>
        /// <param name="value">The access token; never logged or put into error messages</param>
        /// <param name="expiresAt">When the token expires; null if the token endpoint did not say</param>
        /// <exception cref="ArgumentException">The token is empty.</exception>
        public OAuthAccessToken(string value, DateTimeOffset? expiresAt)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The access token must not be empty.", nameof(value));
            }
            Value = value;
            ExpiresAt = expiresAt;
        }

        /// <summary>The access token, sent as bearer token. Never logged or put into error messages.</summary>
        public string Value { get; }

        /// <summary>When the token expires, taken from expires_in; null if the token endpoint did not say.</summary>
        public DateTimeOffset? ExpiresAt { get; }
    }
}
