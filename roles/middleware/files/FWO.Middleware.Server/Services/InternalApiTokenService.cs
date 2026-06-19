using FWO.Api.Client;
using FWO.Logging;
using System.IdentityModel.Tokens.Jwt;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Maintains a short-lived middleware-server token for the shared internal API connection.
    /// The refresh loop and lead-time decision are provided by <see cref="InternalApiTokenRefreshService"/>.
    /// </summary>
    public class InternalApiTokenService
    {
        private readonly JwtWriter jwtWriter;
        private readonly TokenLifetimeProvider tokenLifetimeProvider;
        private readonly JwtSecurityTokenHandler tokenHandler = new();

        private string currentToken = "";
        private DateTime currentTokenExpiresUtc = DateTime.MinValue;

        /// <summary>
        /// Creates a new internal API token service.
        /// </summary>
        public InternalApiTokenService(JwtWriter jwtWriter, TokenLifetimeProvider tokenLifetimeProvider)
        {
            this.jwtWriter = jwtWriter;
            this.tokenLifetimeProvider = tokenLifetimeProvider;
        }

        /// <summary>
        /// Gets the current middleware-server JWT, or an empty string when none has been created yet.
        /// </summary>
        public string CurrentToken => currentToken;

        /// <summary>
        /// Gets the UTC expiry of the current middleware-server JWT, or null when none has been created yet.
        /// </summary>
        public DateTime? CurrentTokenExpiresUtc => string.IsNullOrWhiteSpace(currentToken) ? null : currentTokenExpiresUtc;

        /// <summary>
        /// Creates the first middleware-server JWT and stores its expiry metadata.
        /// </summary>
        public string CreateInitialMiddlewareToken()
        {
            return CreateNewMiddlewareToken();
        }

        /// <summary>
        /// Mints a fresh middleware-server JWT, applies it to the shared internal API connection, and audits the rotation.
        /// </summary>
        /// <param name="apiConnection">Connection whose subscriptions are reconnected with the refreshed token.</param>
        /// <param name="cancellationToken">Token used to cancel the reconnect.</param>
        /// <returns>The refreshed middleware-server JWT.</returns>
        public async Task<string> RefreshAndApplyAsync(ApiConnection apiConnection, CancellationToken cancellationToken = default)
        {
            string refreshedToken = CreateNewMiddlewareToken();
            await apiConnection.ReconnectSubscriptionsAsync(refreshedToken, cancellationToken);

            Log.WriteAudit(nameof(InternalApiTokenService), BuildTokenAuditText(refreshedToken, "Rotated internal middleware JWT."));

            return refreshedToken;
        }

        private string CreateNewMiddlewareToken()
        {
            TimeSpan lifetime = tokenLifetimeProvider.GetInternalServiceTokenLifetime();
            currentToken = jwtWriter.CreateJWTMiddlewareServer(lifetime);
            currentTokenExpiresUtc = tokenHandler.ReadJwtToken(currentToken).ValidTo;
            return currentToken;
        }

        /// <summary>
        /// Builds the log text for a middleware token refresh.
        /// </summary>
        /// <param name="jwt">Refreshed JWT.</param>
        /// <param name="actionText">Human-readable action prefix.</param>
        /// <returns>Log message text containing jti and expiry information.</returns>
        private static string BuildTokenAuditText(string jwt, string actionText)
        {
            JwtSecurityToken accessToken = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
            return $"{actionText} access_jti={accessToken.Id}, access_expires={accessToken.ValidTo.ToLocalTime():yyyy-MM-dd'T'HH:mm:sszzz}";
        }
    }
}
