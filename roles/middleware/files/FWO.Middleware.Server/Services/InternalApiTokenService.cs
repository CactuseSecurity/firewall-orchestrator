using FWO.Api.Client;
using FWO.Logging;
using System.IdentityModel.Tokens.Jwt;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Maintains a short-lived middleware-server token for the shared internal API connection.
    /// </summary>
    public class InternalApiTokenService
    {
        private readonly JwtWriter jwtWriter;
        private readonly TokenLifetimeProvider tokenLifetimeProvider;
        private readonly InternalApiTokenServiceOptions options;
        private readonly JwtSecurityTokenHandler tokenHandler = new();
        private readonly SemaphoreSlim refreshLock = new(1, 1);

        private string currentToken = "";
        private DateTime currentTokenExpiresUtc = DateTime.MinValue;

        /// <summary>
        /// Creates a new internal API token service.
        /// </summary>
        public InternalApiTokenService(JwtWriter jwtWriter, TokenLifetimeProvider tokenLifetimeProvider, InternalApiTokenServiceOptions? options = null)
        {
            this.jwtWriter = jwtWriter;
            this.tokenLifetimeProvider = tokenLifetimeProvider;
            this.options = options ?? new InternalApiTokenServiceOptions();
        }

        /// <summary>
        /// Gets the interval used by the background refresh loop.
        /// </summary>
        public TimeSpan RefreshCheckInterval => options.RefreshCheckInterval;

        /// <summary>
        /// Creates the first middleware-server JWT and stores its expiry metadata.
        /// </summary>
        public string CreateInitialMiddlewareToken()
        {
            return CreateNewMiddlewareToken();
        }

        /// <summary>
        /// Ensures that the shared internal API connection uses a fresh middleware-server JWT.
        /// </summary>
        public async Task<string> EnsureFreshTokenAsync(ApiConnection apiConnection, bool force = false, CancellationToken cancellationToken = default)
        {
            if (!force && !NeedsRefresh())
            {
                return currentToken;
            }

            await refreshLock.WaitAsync(cancellationToken);
            try
            {
                if (!force && !NeedsRefresh())
                {
                    return currentToken;
                }

                string refreshedToken = CreateNewMiddlewareToken();
                apiConnection.SetAuthHeader(refreshedToken);
                Log.WriteDebug("Jwt generation", $"Rotated internal middleware JWT. Valid until: {TimeZoneInfo.ConvertTimeFromUtc(currentTokenExpiresUtc, TimeZoneInfo.Local)}.");
                return refreshedToken;
            }
            finally
            {
                refreshLock.Release();
            }
        }

        private bool NeedsRefresh()
        {
            return string.IsNullOrWhiteSpace(currentToken) || currentTokenExpiresUtc <= DateTime.UtcNow.Add(options.RefreshLeadTime);
        }

        private string CreateNewMiddlewareToken()
        {
            TimeSpan lifetime = tokenLifetimeProvider.GetInternalServiceTokenLifetime();
            currentToken = jwtWriter.CreateJWTMiddlewareServer(lifetime);
            currentTokenExpiresUtc = tokenHandler.ReadJwtToken(currentToken).ValidTo;
            return currentToken;
        }
    }
}
