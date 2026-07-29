using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using System.Security.Claims;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Reports the lifetime and the logged in user of a Blazor circuit to the <see cref="UiSessionTracker"/>.
    /// Registered as scoped service, so one instance exists per circuit.
    /// </summary>
    public sealed class UiSessionCircuitHandler : CircuitHandler, IDisposable
    {
        /// <summary>
        /// Claim holding the distinguished name of the logged in user.
        /// </summary>
        private const string kUserDnClaim = "x-hasura-uuid";

        private readonly UiSessionTracker sessionTracker;
        private readonly AuthenticationStateProvider authenticationStateProvider;
        private bool disposed;

        /// <summary>
        /// Identifier this handler uses in the <see cref="UiSessionTracker"/>. The circuit id is not available
        /// before the circuit is opened, so an own id is used instead.
        /// </summary>
        public string SessionId { get; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Initializes a new instance of the <see cref="UiSessionCircuitHandler"/> class.
        /// </summary>
        /// <param name="sessionTracker">Tracker collecting all open sessions.</param>
        /// <param name="authenticationStateProvider">Provider of the authentication state of this circuit.</param>
        public UiSessionCircuitHandler(UiSessionTracker sessionTracker, AuthenticationStateProvider authenticationStateProvider)
        {
            this.sessionTracker = sessionTracker;
            this.authenticationStateProvider = authenticationStateProvider;
            this.authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        /// <inheritdoc />
        public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            sessionTracker.Register(SessionId);
            ApplyUser(await authenticationStateProvider.GetAuthenticationStateAsync());
        }

        /// <inheritdoc />
        public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            sessionTracker.SetConnected(SessionId, true);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            sessionTracker.SetConnected(SessionId, false);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            sessionTracker.Unregister(SessionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops listening for authentication state changes and drops the session from the tracker.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
                sessionTracker.Unregister(SessionId);
            }
        }

        private void OnAuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
        {
            _ = authenticationStateTask.ContinueWith(task =>
            {
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    ApplyUser(task.Result);
                }
            }, TaskScheduler.Default);
        }

        private void ApplyUser(AuthenticationState authenticationState)
        {
            ClaimsPrincipal user = authenticationState.User;
            bool authenticated = user.Identity?.IsAuthenticated == true;
            sessionTracker.SetUser(SessionId,
                authenticated ? user.Identity?.Name : "",
                authenticated ? user.FindFirst(kUserDnClaim)?.Value : "");
        }
    }
}
