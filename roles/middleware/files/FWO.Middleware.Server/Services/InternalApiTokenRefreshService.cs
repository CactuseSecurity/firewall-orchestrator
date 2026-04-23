using FWO.Api.Client;
using FWO.Logging;
using Microsoft.Extensions.Hosting;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Periodically refreshes the shared middleware-server JWT used by the internal API connection.
    /// </summary>
    public class InternalApiTokenRefreshService : BackgroundService
    {
        private readonly InternalApiTokenService internalApiTokenService;
        private readonly ApiConnection apiConnection;

        /// <summary>
        /// Creates a new background refresh service for the internal API token.
        /// </summary>
        public InternalApiTokenRefreshService(InternalApiTokenService internalApiTokenService, ApiConnection apiConnection)
        {
            this.internalApiTokenService = internalApiTokenService;
            this.apiConnection = apiConnection;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await internalApiTokenService.EnsureFreshTokenAsync(apiConnection, cancellationToken: stoppingToken);
                    await Task.Delay(internalApiTokenService.RefreshCheckInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    Log.WriteError("Jwt generation", "Refreshing internal middleware JWT failed.", exception);
                    await Task.Delay(internalApiTokenService.RefreshCheckInterval, stoppingToken);
                }
            }
        }
    }
}
