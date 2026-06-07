using FWO.Data.Middleware;
using FWO.Logging;
using Microsoft.Extensions.Hosting;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Refreshes the anonymous token used by the singleton global configuration subscription.
    /// </summary>
    public class GlobalConfigTokenRefreshService : BackgroundService
    {
        private const string LogCategory = "Global Config Token Refresh";

        private readonly GlobalConfigApiConnection globalConfigApiConnection;
        private readonly IAnonymousGlobalConfigTokenProvider tokenProvider;
        private readonly GlobalConfigTokenState tokenState;
        private readonly GlobalConfigTokenRefreshOptions options;
        private readonly SemaphoreSlim refreshLock = new(1, 1);

        /// <summary>
        /// Creates a refresh service for the singleton global configuration subscription token.
        /// </summary>
        /// <param name="globalConfigApiConnection">Connection used by the singleton global configuration.</param>
        /// <param name="tokenProvider">Provider for fresh anonymous token pairs.</param>
        /// <param name="tokenState">State containing the current anonymous token pair.</param>
        /// <param name="options">Refresh timing options.</param>
        public GlobalConfigTokenRefreshService(
            GlobalConfigApiConnection globalConfigApiConnection,
            IAnonymousGlobalConfigTokenProvider tokenProvider,
            GlobalConfigTokenState tokenState,
            GlobalConfigTokenRefreshOptions? options = null)
        {
            this.globalConfigApiConnection = globalConfigApiConnection;
            this.tokenProvider = tokenProvider;
            this.tokenState = tokenState;
            this.options = options ?? new GlobalConfigTokenRefreshOptions();
        }

        /// <summary>
        /// Ensures that the singleton global configuration subscription uses a token that has not nearly expired.
        /// </summary>
        /// <param name="force">Refresh even when the current token is still fresh.</param>
        /// <param name="cancellationToken">Token used to cancel the refresh.</param>
        /// <returns>The current token pair after the freshness check.</returns>
        public async Task<TokenPair> EnsureFreshTokenAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            if (!force && !NeedsRefresh())
            {
                return tokenState.CurrentTokenPair;
            }

            await refreshLock.WaitAsync(cancellationToken);
            try
            {
                if (!force && !NeedsRefresh())
                {
                    return tokenState.CurrentTokenPair;
                }

                TokenPair refreshedTokenPair = await tokenProvider.CreateTokenPairAsync(cancellationToken);
                await globalConfigApiConnection.ApiConnection.ReconnectSubscriptionsAsync(refreshedTokenPair.AccessToken, cancellationToken);
                tokenState.CurrentTokenPair = refreshedTokenPair;

                Log.WriteInfo(LogCategory, $"Refreshed anonymous global config token. Access token expires at {NormalizeTokenExpiration(refreshedTokenPair.AccessTokenExpires):yyyy-MM-dd'T'HH:mm:ss'Z'}.");

                return refreshedTokenPair;
            }
            finally
            {
                refreshLock.Release();
            }
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EnsureFreshTokenAsync(cancellationToken: stoppingToken);
                    await Task.Delay(options.RefreshCheckInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    Log.WriteError(LogCategory, "Refreshing anonymous global config token failed.", exception);
                    await Task.Delay(options.RefreshCheckInterval, stoppingToken);
                }
            }
        }

        private bool NeedsRefresh()
        {
            return string.IsNullOrWhiteSpace(tokenState.CurrentTokenPair.AccessToken)
                || NormalizeTokenExpiration(tokenState.CurrentTokenPair.AccessTokenExpires) <= DateTime.UtcNow.Add(options.RefreshLeadTime);
        }

        private static DateTime NormalizeTokenExpiration(DateTime expiration)
        {
            if (expiration == default)
            {
                return DateTime.MinValue;
            }

            return expiration.Kind switch
            {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(expiration, DateTimeKind.Utc),
                _ => expiration.ToUniversalTime()
            };
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            refreshLock.Dispose();
            base.Dispose();
        }
    }
}
