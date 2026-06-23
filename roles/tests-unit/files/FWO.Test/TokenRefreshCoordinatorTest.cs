using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Auth;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    public class TokenRefreshCoordinatorTest
    {
        [SetUp]
        public void SetUp()
        {
            TokenRefreshCoordinator.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            TokenRefreshCoordinator.ResetForTests();
        }

        [Test]
        public async Task StartAsync_ReusesOneSharedRunnerPerSessionKey()
        {
            MockProtectedSessionStorage sessionStorage = new();
            TrackingPeriodicTaskRunnerFactory runnerFactory = new();

            TokenRefreshCoordinator coordinator1 = CreateCoordinator(sessionStorage, runnerFactory);
            TokenRefreshCoordinator coordinator2 = CreateCoordinator(sessionStorage, runnerFactory);

            await coordinator1.StartAsync();

            Assert.That(sessionStorage.ContainsKey("token_refresh_coordinator_session_key"), Is.True);
            Assert.That(runnerFactory.CreateCallCount, Is.EqualTo(1));
            Assert.That(runnerFactory.StartCallCount, Is.EqualTo(1));

            string storedSessionKey = await ReadSessionKey(sessionStorage);
            Assert.That(storedSessionKey, Is.Not.Null.And.Not.Empty);

            await coordinator2.StartAsync();

            Assert.That(await ReadSessionKey(sessionStorage), Is.EqualTo(storedSessionKey));
            Assert.That(runnerFactory.CreateCallCount, Is.EqualTo(1));
            Assert.That(runnerFactory.StartCallCount, Is.EqualTo(1));

            coordinator1.Stop();
            Assert.That(runnerFactory.DisposeCallCount, Is.EqualTo(0));

            coordinator2.Stop();
            Assert.That(runnerFactory.DisposeCallCount, Is.EqualTo(1));
        }

        private static async Task<string> ReadSessionKey(MockProtectedSessionStorage sessionStorage)
        {
            return (await sessionStorage.GetAsync<string>("token_refresh_coordinator_session_key")).Value ?? "";
        }

        private static TokenRefreshCoordinator CreateCoordinator(MockProtectedSessionStorage sessionStorage, TrackingPeriodicTaskRunnerFactory runnerFactory)
        {
            MockMiddlewareClient middlewareClient = new();
            TokenService tokenService = new(middlewareClient, sessionStorage);

            return new TokenRefreshCoordinator(
                sessionStorage,
                tokenService,
                new TestAuthenticationStateProvider(),
                new MockApiConnection(),
                middlewareClient,
                new UserConfig(),
                runnerFactory,
                new TestNavigationManager());
        }

        private sealed class TrackingPeriodicTaskRunnerFactory : IPeriodicTaskRunnerFactory
        {
            public int CreateCallCount;
            public int StartCallCount;
            public int DisposeCallCount;

            public IPeriodicTaskRunner Create(Func<Task> callback, TimeSpan interval, string taskName = "")
            {
                _ = callback;
                _ = interval;
                _ = taskName;
                CreateCallCount++;
                return new TrackingPeriodicTaskRunner(this);
            }
        }

        private sealed class TrackingPeriodicTaskRunner : IPeriodicTaskRunner
        {
            private readonly TrackingPeriodicTaskRunnerFactory factory;

            public TrackingPeriodicTaskRunner(TrackingPeriodicTaskRunnerFactory factory)
            {
                this.factory = factory;
            }

            public void Start()
            {
                factory.StartCallCount++;
            }

            public void Dispose()
            {
                factory.DisposeCallCount++;
            }
        }

        private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
        {
            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                ClaimsPrincipal principal = new(new ClaimsIdentity());
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private sealed class TestNavigationManager : NavigationManager
        {
            public TestNavigationManager()
            {
                Initialize("http://localhost/", "http://localhost/");
            }

            protected override void NavigateToCore(string uri, bool forceLoad)
            {
            }
        }
    }
}
