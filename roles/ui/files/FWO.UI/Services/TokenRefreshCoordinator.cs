using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Middleware.Client;
using FWO.Ui.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Coordinates the UI token refresh loop and keeps it stable across Blazor circuit restarts.
    /// </summary>
    public sealed class TokenRefreshCoordinator : ITokenRefreshCoordinator
    {
        private const string LogCategory = "Token Refresh Coordinator";
        private static readonly TimeSpan TokenRefreshCheckInterval = TimeSpan.FromSeconds(30);

        private readonly TokenService tokenService;
        private readonly AuthenticationStateProvider authenticationProvider;
        private readonly ApiConnection apiConnection;
        private readonly MiddlewareClient middlewareClient;
        private readonly UserConfig userConfig;
        private readonly IPeriodicTaskRunnerFactory periodicTaskRunnerFactory;
        private readonly NavigationManager navigationManager;
        private readonly SemaphoreSlim startStopLock = new(1, 1);

        private IPeriodicTaskRunner? runner;
        private bool started;

        /// <summary>
        /// Creates a new token refresh coordinator.
        /// </summary>
        public TokenRefreshCoordinator(
            TokenService tokenService,
            AuthenticationStateProvider authenticationProvider,
            ApiConnection apiConnection,
            MiddlewareClient middlewareClient,
            UserConfig userConfig,
            IPeriodicTaskRunnerFactory periodicTaskRunnerFactory,
            NavigationManager navigationManager)
        {
            this.tokenService = tokenService;
            this.authenticationProvider = authenticationProvider;
            this.apiConnection = apiConnection;
            this.middlewareClient = middlewareClient;
            this.userConfig = userConfig;
            this.periodicTaskRunnerFactory = periodicTaskRunnerFactory;
            this.navigationManager = navigationManager;
        }

        /// <inheritdoc />
        public async Task StartAsync()
        {
            await startStopLock.WaitAsync();
            try
            {
                if (started)
                {
                    return;
                }

                started = true;
                navigationManager.LocationChanged += OnLocationChanged;
                runner = periodicTaskRunnerFactory.Create(CheckAndRefreshTokenAsync, TokenRefreshCheckInterval, nameof(TokenRefreshCoordinator));
                runner.Start();
            }
            finally
            {
                startStopLock.Release();
            }

            await CheckAndRefreshTokenAsync();
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            IPeriodicTaskRunner? runnerToStop = await DetachRunnerAsync();
            if (runnerToStop != null)
            {
                // shut down outside the lock: the runner may need arbitrarily long to end its loop and must
                // not keep a concurrent Dispose waiting on the lock while it does
                await runnerToStop.DisposeAsync();
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }

        /// <summary>
        /// Stops the refresh loop synchronously. Prefer <see cref="StopAsync"/> when running on a thread the
        /// refresh callback depends on, because this method can only wait for a limited time.
        /// </summary>
        public void Dispose()
        {
            // detaching only takes the lock for the short bookkeeping below, so the bounded wait of the
            // runner's synchronous shutdown stays the only thing this thread can block on for long
            IPeriodicTaskRunner? runnerToStop;
            startStopLock.Wait();
            try
            {
                runnerToStop = DetachRunner();
            }
            finally
            {
                startStopLock.Release();
            }

            runnerToStop?.Dispose();
        }

        /// <summary>
        /// Detaches the running refresh loop from this coordinator while holding <see cref="startStopLock"/>.
        /// The caller has to shut the returned runner down.
        /// </summary>
        /// <returns>The runner to be shut down, or null if the coordinator was not started.</returns>
        private async Task<IPeriodicTaskRunner?> DetachRunnerAsync()
        {
            await startStopLock.WaitAsync();
            try
            {
                return DetachRunner();
            }
            finally
            {
                startStopLock.Release();
            }
        }

        /// <summary>
        /// Detaches the running refresh loop from this coordinator. The caller has to shut the returned
        /// runner down. Must only be called while <see cref="startStopLock"/> is held.
        /// </summary>
        /// <returns>The runner to be shut down, or null if the coordinator was not started.</returns>
        private IPeriodicTaskRunner? DetachRunner()
        {
            if (!started)
            {
                return null;
            }

            started = false;
            navigationManager.LocationChanged -= OnLocationChanged;
            IPeriodicTaskRunner? runnerToStop = runner;
            runner = null;
            return runnerToStop;
        }

        private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            await CheckAndRefreshTokenAsync();
        }

        private async Task CheckAndRefreshTokenAsync()
        {
            try
            {
                if (!await tokenService.HasAccessToken() || !await tokenService.HasRefreshToken())
                {
                    return;
                }

                if (await tokenService.IsAccessTokenExpired())
                {
                    Log.WriteDebug(LogCategory, "Access token expired, attempting refresh...");

                    bool refreshSuccess = await ((AuthStateProvider)authenticationProvider).RestoreAuthenticationState(apiConnection, middlewareClient, userConfig);

                    if (refreshSuccess)
                    {
                        Log.WriteAudit(LogCategory, $"Successfully restored session for User \"{userConfig.User.Name}\" with DN: \"{userConfig.User.Dn}\".");
                    }
                    else
                    {
                        Log.WriteAudit(LogCategory, $"Failed to restore session for User \"{userConfig.User.Name}\" with DN: \"{userConfig.User.Dn}\".");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteError(LogCategory, "Error during token check/refresh", ex);
            }
        }
    }
}
