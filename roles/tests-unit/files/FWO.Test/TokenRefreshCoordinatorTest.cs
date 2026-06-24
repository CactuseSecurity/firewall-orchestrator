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
        [Test]
        public async Task StartAsync_CreatesSeparateRunnerPerCoordinatorInstance()
        {
            MockProtectedSessionStorage sessionStorage = new();
            TrackingPeriodicTaskRunnerFactory runnerFactory = new();

            TokenRefreshCoordinator coordinator1 = CreateCoordinator(sessionStorage, runnerFactory);
            TokenRefreshCoordinator coordinator2 = CreateCoordinator(sessionStorage, runnerFactory);

            await coordinator1.StartAsync();

            Assert.That(runnerFactory.CreateCallCount, Is.EqualTo(1));
            Assert.That(runnerFactory.StartCallCount, Is.EqualTo(1));

            await coordinator2.StartAsync();

            Assert.That(runnerFactory.CreateCallCount, Is.EqualTo(2));
            Assert.That(runnerFactory.StartCallCount, Is.EqualTo(2));

            coordinator1.Stop();
            Assert.That(runnerFactory.DisposeCallCount, Is.EqualTo(1));

            coordinator2.Stop();
            Assert.That(runnerFactory.DisposeCallCount, Is.EqualTo(2));
        }

        [Test]
        public async Task StartAsync_WhenCalledTwiceOnSameInstance_StartsOnlyOneRunner()
        {
            TrackingPeriodicTaskRunnerFactory runnerFactory = new();
            TokenRefreshCoordinator coordinator = CreateCoordinator(new MockProtectedSessionStorage(), runnerFactory);

            await coordinator.StartAsync();
            await coordinator.StartAsync();

            Assert.That(runnerFactory.CreateCallCount, Is.EqualTo(1));
            Assert.That(runnerFactory.StartCallCount, Is.EqualTo(1));

            coordinator.Stop();
            Assert.That(runnerFactory.DisposeCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Stop_WhenCalledTwice_DisposesRunnerOnlyOnce()
        {
            TrackingPeriodicTaskRunnerFactory runnerFactory = new();
            TokenRefreshCoordinator coordinator = CreateCoordinator(new MockProtectedSessionStorage(), runnerFactory);

            coordinator.StartAsync().GetAwaiter().GetResult();

            coordinator.Stop();
            coordinator.Stop();

            Assert.That(runnerFactory.DisposeCallCount, Is.EqualTo(1));
        }

        private static TokenRefreshCoordinator CreateCoordinator(MockProtectedSessionStorage sessionStorage, TrackingPeriodicTaskRunnerFactory runnerFactory)
        {
            MockMiddlewareClient middlewareClient = new();
            TokenService tokenService = new(middlewareClient, sessionStorage);

            return new TokenRefreshCoordinator(
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
