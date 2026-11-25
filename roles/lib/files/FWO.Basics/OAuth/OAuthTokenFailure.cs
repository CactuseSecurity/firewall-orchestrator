namespace FWO.Basics.OAuth
{
    /// <summary>
    /// Why an OAuth 2.0 token request failed, see <see cref="Exceptions.OAuthTokenException"/>.
    /// </summary>
    public enum OAuthTokenFailure
    {
        /// <summary>The token endpoint did not answer: connection failure or timeout. A later retry may succeed.</summary>
        Unreachable,

        /// <summary>The server certificate of the token endpoint was refused; a retry cannot fix this.</summary>
        CertificateRejected,

        /// <summary>The token endpoint answered with an HTTP error, e.g. because it refused the client credentials.</summary>
        Rejected,

        /// <summary>The token endpoint answered with success but without a usable access token.</summary>
        InvalidResponse
    }
}
