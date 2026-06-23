using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Middleware.Client;
using FWO.Ui.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Coordinates the UI token refresh loop and keeps it stable across Blazor circuit restarts.
    /// </summary>
    public sealed class TokenRefreshCoordinator : ITokenRefreshCoordinator
    {
        private const string SessionKeyStorageName = "token_refresh_coordinator_session_key";
        private const string LogCategory = "Token Refresh Coordinator";
        private static readonly TimeSpan TokenRefreshCheckInterval = TimeSpan.FromSeconds(30);
        private static readonly SessionRefreshRegistry SharedRegistry = new();

        private readonly ISessionStorage sessionStorage;
        private readonly TokenService tokenService;
        private readonly AuthenticationStateProvider authenticationProvider;
        private readonly ApiConnection apiConnection;
        private readonly MiddlewareClient middlewareClient;
        private readonly UserConfig userConfig;
        private readonly IPeriodicTaskRunnerFactory periodicTaskRunnerFactory;
        private readonly NavigationManager navigationManager;
        private readonly SemaphoreSlim startStopLock = new(1, 1);

        private IDisposable? sessionLease;
        private bool started;

        /// <summary>
        /// Creates a new token refresh coordinator.
        /// </summary>
        public TokenRefreshCoordinator(
            ISessionStorage sessionStorage,
            TokenService tokenService,
            AuthenticationStateProvider authenticationProvider,
            ApiConnection apiConnection,
            MiddlewareClient middlewareClient,
            UserConfig userConfig,
            IPeriodicTaskRunnerFactory periodicTaskRunnerFactory,
            NavigationManager navigationManager)
        {
            this.sessionStorage = sessionStorage;
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
                sessionLease = SharedRegistry.Register(await GetOrCreateSessionKeyAsync(), CheckAndRefreshTokenAsync, periodicTaskRunnerFactory);
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
                sessionLease?.Dispose();
                sessionLease = null;
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

        /// <summary>
        /// Clears the shared registry state. Intended for unit tests only.
        /// </summary>
        public static void ResetForTests()
        {
            SharedRegistry.Reset();
        }

        private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            await CheckAndRefreshTokenAsync();
        }

        private async Task<string> GetOrCreateSessionKeyAsync()
        {
            ProtectedBrowserStorageResult<string> result = await sessionStorage.GetAsync<string>(SessionKeyStorageName);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Value))
            {
                return result.Value;
            }

            string sessionKey = Guid.NewGuid().ToString("N");
            await sessionStorage.SetAsync(SessionKeyStorageName, sessionKey);
            return sessionKey;
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

        private sealed class SessionRefreshRegistry
        {
            private readonly object syncRoot = new();
            private readonly Dictionary<string, SessionState> sessionStates = new();

            public IDisposable Register(string sessionKey, Func<Task> refreshCallback, IPeriodicTaskRunnerFactory periodicTaskRunnerFactory)
            {
                SessionState? state = null;
                bool startRunner = false;

                lock (syncRoot)
                {
                    if (!sessionStates.TryGetValue(sessionKey, out state))
                    {
                        state = new SessionState();
                        state.SetRunner(periodicTaskRunnerFactory.Create(() => state!.InvokeCallbackAsync(), TokenRefreshCheckInterval, $"[{nameof(TokenRefreshCoordinator)}:{sessionKey}]"));
                        sessionStates.Add(sessionKey, state);
                        startRunner = true;
                    }

                    state!.SetCallback(refreshCallback);
                    state.AddLease();
                }

                if (startRunner)
                {
                    state!.Start();
                }

                return new SessionLease(this, sessionKey);
            }

            public void Release(string sessionKey)
            {
                SessionState? stateToDispose = null;

                lock (syncRoot)
                {
                    if (!sessionStates.TryGetValue(sessionKey, out SessionState? state))
                    {
                        return;
                    }

                    if (!state.ReleaseLease())
                    {
                        return;
                    }

                    sessionStates.Remove(sessionKey);
                    stateToDispose = state;
                }

                stateToDispose?.Dispose();
            }

            public void Reset()
            {
                List<SessionState> statesToDispose;

                lock (syncRoot)
                {
                    statesToDispose = sessionStates.Values.ToList();
                    sessionStates.Clear();
                }

                foreach (SessionState state in statesToDispose)
                {
                    state.Dispose();
                }
            }
        }

        private sealed class SessionLease : IDisposable
        {
            private readonly SessionRefreshRegistry registry;
            private readonly string sessionKey;
            private int disposed;

            public SessionLease(SessionRefreshRegistry registry, string sessionKey)
            {
                this.registry = registry;
                this.sessionKey = sessionKey;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 1)
                {
                    return;
                }

                registry.Release(sessionKey);
            }
        }

        private sealed class SessionState : IDisposable
        {
            private readonly object syncRoot = new();
            private Func<Task> callback = () => Task.CompletedTask;
            private IPeriodicTaskRunner? runner;
            private int leaseCount;
            private bool disposed;

            public void SetCallback(Func<Task> refreshCallback)
            {
                lock (syncRoot)
                {
                    callback = refreshCallback;
                }
            }

            public void SetRunner(IPeriodicTaskRunner periodicTaskRunner)
            {
                lock (syncRoot)
                {
                    runner = periodicTaskRunner;
                }
            }

            public void AddLease()
            {
                lock (syncRoot)
                {
                    leaseCount++;
                }
            }

            public bool ReleaseLease()
            {
                lock (syncRoot)
                {
                    if (leaseCount > 0)
                    {
                        leaseCount--;
                    }

                    if (leaseCount > 0 || disposed)
                    {
                        return false;
                    }

                    return true;
                }
            }

            public void Start()
            {
                IPeriodicTaskRunner? currentRunner;

                lock (syncRoot)
                {
                    currentRunner = runner;
                }

                currentRunner?.Start();
            }

            public Task InvokeCallbackAsync()
            {
                Func<Task> currentCallback;

                lock (syncRoot)
                {
                    if (disposed)
                    {
                        return Task.CompletedTask;
                    }

                    currentCallback = callback;
                }

                return currentCallback();
            }

            public void Dispose()
            {
                IPeriodicTaskRunner? currentRunner;

                lock (syncRoot)
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    currentRunner = runner;
                }

                currentRunner?.Dispose();
            }
        }
    }
}
