using FWO.Api.Client;
using System.IdentityModel.Tokens.Jwt;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Lazily mints and caches internal service JWTs for middleware-owned API connections.
    /// </summary>
    public sealed class InternalServiceJwtProvider : IAuthTokenProvider
    {
        private readonly Func<TimeSpan?, string> tokenFactory;
        private readonly TimeSpan tokenLifetime;
        private readonly TimeSpan refreshLeadTime;
        private readonly object syncRoot = new();

        private string? currentToken;
        private DateTime expiresAtUtc = DateTime.MinValue;

        /// <summary>
        /// Creates a provider that mints short-lived internal JWTs on demand.
        /// </summary>
        /// <param name="tokenFactory">Factory used to create a JWT for the requested lifetime.</param>
        /// <param name="tokenLifetime">Requested JWT lifetime.</param>
        /// <param name="refreshLeadTime">Time before expiry at which the cached token should be renewed.</param>
        public InternalServiceJwtProvider(Func<TimeSpan?, string> tokenFactory, TimeSpan tokenLifetime, TimeSpan refreshLeadTime)
        {
            this.tokenFactory = tokenFactory;
            this.tokenLifetime = tokenLifetime;
            this.refreshLeadTime = refreshLeadTime;

            if (tokenLifetime <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(tokenLifetime), "Token lifetime must be positive.");
            }
            if (refreshLeadTime <= TimeSpan.Zero || refreshLeadTime >= tokenLifetime)
            {
                throw new ArgumentOutOfRangeException(nameof(refreshLeadTime), "Refresh lead time must be positive and shorter than the token lifetime.");
            }
        }

        /// <summary>
        /// Returns a cached bearer token or mints a fresh one if the current token is missing or near expiry.
        /// </summary>
        /// <returns>A bearer token for internal service communication.</returns>
        public string GetBearerToken()
        {
            lock (syncRoot)
            {
                if (TokenNeedsRefresh())
                {
                    MintTokenLocked();
                }

                return currentToken ?? throw new InvalidOperationException("Internal JWT could not be minted.");
            }
        }

        /// <summary>
        /// Invalidates the cached token so the next call mints a fresh JWT.
        /// </summary>
        public void Invalidate()
        {
            lock (syncRoot)
            {
                currentToken = null;
                expiresAtUtc = DateTime.MinValue;
            }
        }

        private bool TokenNeedsRefresh()
        {
            return string.IsNullOrWhiteSpace(currentToken) || DateTime.UtcNow >= expiresAtUtc - refreshLeadTime;
        }

        private void MintTokenLocked()
        {
            string mintedToken = tokenFactory(tokenLifetime);
            JwtSecurityToken parsedToken = new JwtSecurityTokenHandler().ReadJwtToken(mintedToken);
            currentToken = mintedToken;
            expiresAtUtc = parsedToken.ValidTo;
        }
    }
}
