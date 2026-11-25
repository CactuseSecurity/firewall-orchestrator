namespace FWO.Basics.OAuth
{
    /// <summary>
    /// Credentials of an OAuth 2.0 client for the client credentials grant (RFC 6749, section 4.4): the token endpoint,
    /// the client id and the client secret, and the scope if the authorization server needs one.
    /// A class rather than a record on purpose: the generated ToString of a record would print the client secret.
    /// </summary>
    public sealed class OAuthClientCredentials
    {
        /// <summary>
        /// Creates the credentials.
        /// </summary>
        /// <param name="tokenUrl">Absolute URL of the token endpoint</param>
        /// <param name="clientId">Client id</param>
        /// <param name="clientSecret">Client secret; never logged or put into error messages</param>
        /// <param name="scope">Scope to request; none if null or empty</param>
        /// <exception cref="ArgumentException">The token URL is not absolute, or the client id or secret is empty.</exception>
        public OAuthClientCredentials(Uri tokenUrl, string clientId, string clientSecret, string? scope = null)
        {
            ArgumentNullException.ThrowIfNull(tokenUrl);
            if (!tokenUrl.IsAbsoluteUri)
            {
                throw new ArgumentException("The token URL must be absolute.", nameof(tokenUrl));
            }
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("A client id is required.", nameof(clientId));
            }
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new ArgumentException("A client secret is required.", nameof(clientSecret));
            }
            TokenUrl = tokenUrl;
            ClientId = clientId;
            ClientSecret = clientSecret;
            Scope = string.IsNullOrWhiteSpace(scope) ? null : scope;
        }

        /// <summary>Absolute URL of the token endpoint.</summary>
        public Uri TokenUrl { get; }

        /// <summary>Client id.</summary>
        public string ClientId { get; }

        /// <summary>Client secret. Never logged or put into error messages.</summary>
        public string ClientSecret { get; }

        /// <summary>Scope to request; null if none is sent.</summary>
        public string? Scope { get; }
    }
}
