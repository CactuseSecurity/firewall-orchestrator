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
        private static readonly TimeSpan kTestTimeout = TimeSpan.FromSeconds(5);


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

            await coordinator1.StopAsync();
            Assert.That(runnerFactory.DisposeCallCount, Is.EqualTo(1));

            await coordinator2.StopAsync();
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

            await coordinator.StopAsync();
            Assert.That(runnerFactory.DisposeCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task StopAsync_WhenCalledTwice_DisposesRunnerOnlyOnce()
        {
            TrackingPeriodicTaskRunnerFactory runnerFactory = new();
            TokenRefreshCoordinator coordinator = CreateCoordinator(new MockProtectedSessionStorage(), runnerFactory);

            await coordinator.StartAsync();

            await coordinator.StopAsync();
            await coordinator.StopAsync();

            Assert.That(runnerFactory.DisposeCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task StopAsync_ShutsTheRunnerDownWithoutBlocking()
        {
            TrackingPeriodicTaskRunnerFactory runnerFactory = new();
            TokenRefreshCoordinator coordinator = CreateCoordinator(new MockProtectedSessionStorage(), runnerFactory);

            await coordinator.StartAsync();
            await coordinator.StopAsync();

            Assert.Multiple(() =>
            {
                // the blocking shutdown would deadlock when called from the render dispatcher
                Assert.That(runnerFactory.AsyncDisposeCallCount, Is.EqualTo(1));
                Assert.That(runnerFactory.SyncDisposeCallCount, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task DisposeAsync_StopsTheRunner()
        {
            TrackingPeriodicTaskRunnerFactory runnerFactory = new();
            TokenRefreshCoordinator coordinator = CreateCoordinator(new MockProtectedSessionStorage(), runnerFactory);

            await coordinator.StartAsync();
            await coordinator.DisposeAsync();

            Assert.That(runnerFactory.AsyncDisposeCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Dispose_StillStopsTheRunnerSynchronously()
        {
            TrackingPeriodicTaskRunnerFactory runnerFactory = new();
            TokenRefreshCoordinator coordinator = CreateCoordinator(new MockProtectedSessionStorage(), runnerFactory);

            await coordinator.StartAsync();
            coordinator.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(runnerFactory.SyncDisposeCallCount, Is.EqualTo(1));
                Assert.That(runnerFactory.DisposeCallCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Dispose_WhileStopAsyncShutsTheRunnerDown_DoesNotWaitForIt()
        {
            BlockingPeriodicTaskRunnerFactory runnerFactory = new();
            TokenRefreshCoordinator coordinator = CreateCoordinator(new MockProtectedSessionStorage(), runnerFactory);
            await coordinator.StartAsync();

            Task stopTask = coordinator.StopAsync();
            await runnerFactory.AsyncDisposeEntered.Task.WaitAsync(kTestTimeout);

            // the coordinator must not keep the start/stop lock while the runner shuts down, otherwise this
            // synchronous Dispose blocks for as long as the shutdown takes - the deadlock this class avoids
            Task disposeTask = Task.Run(coordinator.Dispose);
            Task finishedFirst = await Task.WhenAny(disposeTask, Task.Delay(kTestTimeout));

            runnerFactory.ReleaseAsyncDispose();
            await stopTask;

            Assert.That(finishedFirst, Is.EqualTo(disposeTask), "Dispose must not wait for an ongoing asynchronous shutdown");
        }

        private static TokenRefreshCoordinator CreateCoordinator(MockProtectedSessionStorage sessionStorage, IPeriodicTaskRunnerFactory runnerFactory)
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
            public int SyncDisposeCallCount;
            public int AsyncDisposeCallCount;

            public int DisposeCallCount => SyncDisposeCallCount + AsyncDisposeCallCount;

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
                factory.SyncDisposeCallCount++;
            }

            public ValueTask DisposeAsync()
            {
                factory.AsyncDisposeCallCount++;
                return ValueTask.CompletedTask;
            }
        }

        /// <summary>
        /// Hands out a runner whose asynchronous shutdown blocks until the test releases it.
        /// </summary>
        private sealed class BlockingPeriodicTaskRunnerFactory : IPeriodicTaskRunnerFactory
        {
            public TaskCompletionSource AsyncDisposeEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource asyncDisposeReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public IPeriodicTaskRunner Create(Func<Task> callback, TimeSpan interval, string taskName = "")
            {
                _ = callback;
                _ = interval;
                _ = taskName;
                return new BlockingPeriodicTaskRunner(this);
            }

            /// <summary>
            /// Lets the pending asynchronous shutdown finish.
            /// </summary>
            public void ReleaseAsyncDispose()
            {
                asyncDisposeReleased.TrySetResult();
            }

            private sealed class BlockingPeriodicTaskRunner : IPeriodicTaskRunner
            {
                private readonly BlockingPeriodicTaskRunnerFactory factory;

                public BlockingPeriodicTaskRunner(BlockingPeriodicTaskRunnerFactory factory)
                {
                    this.factory = factory;
                }

                public void Start()
                {
                }

                public void Dispose()
                {
                }

                public async ValueTask DisposeAsync()
                {
                    factory.AsyncDisposeEntered.TrySetResult();
                    await factory.asyncDisposeReleased.Task;
                }
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
