using FWO.Logging;
using Microsoft.Extensions.Hosting;

namespace FWO.Services
{
    /// <summary>
    /// Base background service that periodically rotates a short-lived token before it expires.
    /// Owns the refresh loop, the double-checked locking around a rotation, and the lead-time based
    /// refresh decision so concrete services only have to supply the current token, its expiry, and
    /// how to acquire and apply a fresh one.
    /// </summary>
    /// <typeparam name="TToken">Type representing the held token (e.g. a raw JWT string or a token pair).</typeparam>
    public abstract class TokenRefreshBackgroundService<TToken> : BackgroundService
    {
        private readonly TokenRefreshOptions options;
        private readonly string logCategory;
        private readonly SemaphoreSlim refreshLock = new(1, 1);

        /// <summary>
        /// Creates a new token refresh background service.
        /// </summary>
        /// <param name="options">Refresh timing options. Defaults are used when null.</param>
        /// <param name="logCategory">Log category used for refresh-failure logging.</param>
        protected TokenRefreshBackgroundService(TokenRefreshOptions? options, string logCategory)
        {
            this.options = options ?? new TokenRefreshOptions();
            this.logCategory = logCategory;
        }

        /// <summary>
        /// UTC expiry of the currently held token, or null when no usable token is held yet.
        /// </summary>
        protected abstract DateTime? CurrentTokenExpiresUtc { get; }

        /// <summary>
        /// The currently held token, returned when no refresh is required.
        /// </summary>
        protected abstract TToken CurrentToken { get; }

        /// <summary>
        /// Acquires a fresh token and applies it (e.g. reconnects subscriptions and stores the new token).
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the acquisition.</param>
        /// <returns>The freshly acquired token.</returns>
        protected abstract Task<TToken> AcquireFreshTokenAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Ensures the held token is not within the refresh lead time of expiry, rotating it if needed.
        /// </summary>
        /// <param name="force">Rotate even when the current token is still fresh.</param>
        /// <param name="cancellationToken">Token used to cancel the refresh.</param>
        /// <returns>The current token after the freshness check.</returns>
        public async Task<TToken> EnsureFreshTokenAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            if (!force && !NeedsRefresh())
            {
                return CurrentToken;
            }

            await refreshLock.WaitAsync(cancellationToken);
            try
            {
                if (!force && !NeedsRefresh())
                {
                    return CurrentToken;
                }

                return await AcquireFreshTokenAsync(cancellationToken);
            }
            finally
            {
                refreshLock.Release();
            }
        }

        private bool NeedsRefresh()
        {
            DateTime? expiry = CurrentTokenExpiresUtc;
            return expiry is null || expiry.Value <= DateTime.UtcNow.Add(options.RefreshLeadTime);
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
                    Log.WriteError(logCategory, "Refreshing token failed.", exception);
                    await Task.Delay(options.RefreshCheckInterval, stoppingToken);
                }
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            refreshLock.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
