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
        public void Stop()
        {
            startStopLock.Wait();
            try
            {
                if (!started)
                {
                    return;
                }

                started = false;
                navigationManager.LocationChanged -= OnLocationChanged;
                runner?.Dispose();
                runner = null;
            }
            finally
            {
                startStopLock.Release();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Stop();
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
