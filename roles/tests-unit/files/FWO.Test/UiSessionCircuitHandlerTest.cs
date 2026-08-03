using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Authorization;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiSessionCircuitHandlerTest
    {
        private const string kUserDnClaim = "x-hasura-uuid";
        private const int kStateChangeTimeoutMs = 2000;

        private static async Task OpenCircuit(UiSessionCircuitHandler handler)
        {
            await handler.OnCircuitOpenedAsync(null!, CancellationToken.None);
        }

        [Test]
        public async Task OnCircuitOpened_RegistersAnonymousSession()
        {
            UiSessionTracker tracker = new();
            SessionTestAuthStateProvider authStateProvider = new();
            using UiSessionCircuitHandler handler = new(tracker, authStateProvider);

            await OpenCircuit(handler);
            UiSessionOverview overview = tracker.GetOverview();

            Assert.Multiple(() =>
            {
                Assert.That(overview.OpenSessions, Is.EqualTo(1));
                Assert.That(overview.AuthenticatedSessions, Is.EqualTo(0));
                Assert.That(overview.LoggedInUsers, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task OnCircuitOpened_TakesOverAlreadyAuthenticatedUser()
        {
            UiSessionTracker tracker = new();
            SessionTestAuthStateProvider authStateProvider = new();
            authStateProvider.SetUser("tim", "uid=tim,ou=operator");
            using UiSessionCircuitHandler handler = new(tracker, authStateProvider);

            await OpenCircuit(handler);
            UiSessionOverview overview = tracker.GetOverview();

            Assert.Multiple(() =>
            {
                Assert.That(overview.LoggedInUsers, Is.EqualTo(1));
                Assert.That(overview.Sessions[0].UserName, Is.EqualTo("tim"));
                Assert.That(overview.Sessions[0].UserDn, Is.EqualTo("uid=tim,ou=operator"));
            });
        }

        [Test]
        public async Task AuthenticationStateChanged_StoresLoggedInUser()
        {
            UiSessionTracker tracker = new();
            SessionTestAuthStateProvider authStateProvider = new();
            using UiSessionCircuitHandler handler = new(tracker, authStateProvider);
            await OpenCircuit(handler);

            authStateProvider.SetUser("auditor", "uid=auditor,ou=operator");
            authStateProvider.RaiseAuthenticationStateChanged();

            Assert.That(() => tracker.GetOverview().LoggedInUsers, Is.EqualTo(1).After(kStateChangeTimeoutMs, 20));
            Assert.That(tracker.GetOverview().Sessions[0].UserName, Is.EqualTo("auditor"));
        }

        [Test]
        public async Task AuthenticationStateChanged_LogoutClearsUser()
        {
            UiSessionTracker tracker = new();
            SessionTestAuthStateProvider authStateProvider = new();
            authStateProvider.SetUser("tim", "uid=tim,ou=operator");
            using UiSessionCircuitHandler handler = new(tracker, authStateProvider);
            await OpenCircuit(handler);

            authStateProvider.SetAnonymous();
            authStateProvider.RaiseAuthenticationStateChanged();

            Assert.That(() => tracker.GetOverview().LoggedInUsers, Is.EqualTo(0).After(kStateChangeTimeoutMs, 20));
            Assert.That(tracker.GetOverview().OpenSessions, Is.EqualTo(1));
        }

        [Test]
        public async Task ConnectionUpAndDown_UpdateConnectionState()
        {
            UiSessionTracker tracker = new();
            SessionTestAuthStateProvider authStateProvider = new();
            using UiSessionCircuitHandler handler = new(tracker, authStateProvider);
            await OpenCircuit(handler);

            await handler.OnConnectionUpAsync(null!, CancellationToken.None);
            int connected = tracker.GetOverview().ConnectedSessions;

            await handler.OnConnectionDownAsync(null!, CancellationToken.None);
            int disconnected = tracker.GetOverview().ConnectedSessions;

            Assert.Multiple(() =>
            {
                Assert.That(connected, Is.EqualTo(1));
                Assert.That(disconnected, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task OnCircuitClosed_RemovesSession()
        {
            UiSessionTracker tracker = new();
            SessionTestAuthStateProvider authStateProvider = new();
            using UiSessionCircuitHandler handler = new(tracker, authStateProvider);
            await OpenCircuit(handler);

            await handler.OnCircuitClosedAsync(null!, CancellationToken.None);

            Assert.That(tracker.GetOverview().OpenSessions, Is.EqualTo(0));
        }

        [Test]
        public async Task Dispose_RemovesSession()
        {
            UiSessionTracker tracker = new();
            SessionTestAuthStateProvider authStateProvider = new();
            UiSessionCircuitHandler handler = new(tracker, authStateProvider);
            await OpenCircuit(handler);

            handler.Dispose();
            // disposing twice must stay harmless
            handler.Dispose();

            Assert.That(tracker.GetOverview().OpenSessions, Is.EqualTo(0));
        }

        [Test]
        public async Task Dispose_StopsListeningForAuthenticationStateChanges()
        {
            UiSessionTracker tracker = new();
            SessionTestAuthStateProvider authStateProvider = new();
            UiSessionCircuitHandler disposedHandler = new(tracker, authStateProvider);
            await OpenCircuit(disposedHandler);
            disposedHandler.Dispose();
            // re-create the entry the disposed handler used, so that a still attached handler would fill it
            tracker.Register(disposedHandler.SessionId);

            // a second handler stays attached and marks the point at which the notification has been
            // processed, so that the assertion does not have to wait for a fixed span of time
            using UiSessionCircuitHandler attachedHandler = new(tracker, authStateProvider);
            await OpenCircuit(attachedHandler);
            authStateProvider.SetUser("tim", "uid=tim,ou=operator");
            authStateProvider.RaiseAuthenticationStateChanged();

            Assert.That(() => tracker.GetOverview().LoggedInUsers, Is.EqualTo(1).After(kStateChangeTimeoutMs, 20));
            List<UiSession> sessions = tracker.GetOverview().Sessions;
            Assert.Multiple(() =>
            {
                Assert.That(sessions, Has.Count.EqualTo(2));
                // only the attached handler stored the user, the disposed one ignored the notification
                Assert.That(FindSession(sessions, disposedHandler.SessionId).Authenticated, Is.False);
                Assert.That(FindSession(sessions, attachedHandler.SessionId).UserName, Is.EqualTo("tim"));
            });
        }

        private static UiSession FindSession(List<UiSession> sessions, string sessionId)
        {
            return sessions.Find(session => session.SessionId == sessionId)
                ?? throw new AssertionException($"session {sessionId} was not found");
        }

        [Test]
        public async Task Handlers_OfSeveralCircuitsUseSeparateSessions()
        {
            UiSessionTracker tracker = new();
            SessionTestAuthStateProvider firstProvider = new();
            SessionTestAuthStateProvider secondProvider = new();
            firstProvider.SetUser("tim", "uid=tim,ou=operator");
            secondProvider.SetUser("auditor", "uid=auditor,ou=operator");
            using UiSessionCircuitHandler firstHandler = new(tracker, firstProvider);
            using UiSessionCircuitHandler secondHandler = new(tracker, secondProvider);

            await OpenCircuit(firstHandler);
            await OpenCircuit(secondHandler);
            UiSessionOverview overview = tracker.GetOverview();

            Assert.Multiple(() =>
            {
                Assert.That(overview.OpenSessions, Is.EqualTo(2));
                Assert.That(overview.LoggedInUsers, Is.EqualTo(2));
            });
        }

        internal sealed class SessionTestAuthStateProvider : AuthenticationStateProvider
        {
            private ClaimsPrincipal principal = new(new ClaimsIdentity());

            public void SetUser(string userName, string userDn)
            {
                List<Claim> claims = [new Claim(ClaimTypes.Name, userName), new Claim(kUserDnClaim, userDn)];
                principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            }

            public void SetAnonymous()
            {
                principal = new ClaimsPrincipal(new ClaimsIdentity());
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }

            public void RaiseAuthenticationStateChanged()
            {
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            }
        }
    }
}
