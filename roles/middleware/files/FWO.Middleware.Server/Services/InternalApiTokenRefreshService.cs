using FWO.Api.Client;
using FWO.Services;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Periodically refreshes the shared middleware-server JWT used by the internal API connection.
    /// </summary>
    public class InternalApiTokenRefreshService : TokenRefreshBackgroundService<string>
    {
        private readonly InternalApiTokenService internalApiTokenService;
        private readonly ApiConnection apiConnection;

        /// <summary>
        /// Creates a new background refresh service for the internal API token.
        /// </summary>
        public InternalApiTokenRefreshService(InternalApiTokenService internalApiTokenService, ApiConnection apiConnection, TokenRefreshOptions? options = null)
            : base(options, "Jwt generation")
        {
            this.internalApiTokenService = internalApiTokenService;
            this.apiConnection = apiConnection;
        }

        /// <inheritdoc />
        protected override DateTime? CurrentTokenExpiresUtc => internalApiTokenService.CurrentTokenExpiresUtc;

        /// <inheritdoc />
        protected override string CurrentToken => internalApiTokenService.CurrentToken;

        /// <inheritdoc />
        protected override Task<string> AcquireFreshTokenAsync(CancellationToken cancellationToken)
        {
            return internalApiTokenService.RefreshAndApplyAsync(apiConnection, cancellationToken);
        }
    }
}
