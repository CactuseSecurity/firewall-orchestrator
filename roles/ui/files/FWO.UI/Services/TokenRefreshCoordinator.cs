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
            await startStopLock.WaitAsync();
            try
            {
                IPeriodicTaskRunner? runnerToStop = DetachRunner();
                if (runnerToStop != null)
                {
                    await runnerToStop.DisposeAsync();
                }
            }
            finally
            {
                startStopLock.Release();
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
            startStopLock.Wait();
            try
            {
                DetachRunner()?.Dispose();
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

                    await ((AuthStateProvider)authenticationProvider).RestoreAuthenticationState(apiConnection, middlewareClient, userConfig);
                }
            }
            catch (Exception ex)
            {
                Log.WriteError(LogCategory, "Error during token check/refresh", ex);
            }
        }
    }
}
