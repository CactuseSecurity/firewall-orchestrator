using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Services.EventMediator;
using FWO.Test.Mocks;
using FWO.Ui.Auth;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    public class TokenRefreshCoordinatorTest
    {
        private static readonly TimeSpan kTestTimeout = TimeSpan.FromSeconds(5);
        private static readonly FieldInfo JwtPublicKeyField = typeof(ConfigFile).GetField("jwtPublicKey", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(ConfigFile).FullName, "jwtPublicKey");
        private static readonly FieldInfo JwtPrivateKeyField = typeof(ConfigFile).GetField("jwtPrivateKey", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(ConfigFile).FullName, "jwtPrivateKey");

        private RsaSecurityKey? originalJwtPublicKey;
        private RsaSecurityKey? originalJwtPrivateKey;

        [SetUp]
        public void Setup()
        {
            originalJwtPublicKey = (RsaSecurityKey?)JwtPublicKeyField.GetValue(null);
            originalJwtPrivateKey = (RsaSecurityKey?)JwtPrivateKeyField.GetValue(null);
        }

        [TearDown]
        public void TearDown()
        {
            JwtPublicKeyField.SetValue(null, originalJwtPublicKey);
            JwtPrivateKeyField.SetValue(null, originalJwtPrivateKey);
        }


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

        [Test]
        public async Task StartAsync_WhenStoredAccessTokenIsExpired_RestoresAuthenticationState()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey privateKey = new(rsa.ExportParameters(true));
            RsaSecurityKey publicKey = new(rsa.ExportParameters(false));
            JwtPrivateKeyField.SetValue(null, privateKey);
            JwtPublicKeyField.SetValue(null, publicKey);

            MockMiddlewareClient middlewareClient = new();
            MockProtectedSessionStorage sessionStorage = new();
            TokenService tokenService = new(middlewareClient, sessionStorage);
            EventMediator eventMediator = new();
            TestNavigationManager navigationManager = new();
            AuthStateProvider authStateProvider = new(tokenService, eventMediator, navigationManager);
            RecordingApiConnection apiConnection = new();
            UserConfig userConfig = new();
            TokenRefreshCoordinator coordinator = new(
                tokenService,
                authStateProvider,
                apiConnection,
                middlewareClient,
                userConfig,
                new TrackingPeriodicTaskRunnerFactory(),
                navigationManager);

            await tokenService.SetTokenPair(new TokenPair
            {
                AccessToken = GenerateJwtToken(privateKey, Roles.Reporter, DateTime.UtcNow.AddMinutes(-5)),
                RefreshToken = "refresh-token",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(-5),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
            });

            string refreshedToken = GenerateJwtToken(privateKey, Roles.Reporter, DateTime.UtcNow.AddMinutes(10));
            middlewareClient.NextRefreshTokenResponse = new TokenPair
            {
                AccessToken = refreshedToken,
                RefreshToken = "rotated-refresh-token",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(10),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(2)
            };

            await coordinator.StartAsync();
            AuthenticationState authenticationState = await authStateProvider.GetAuthenticationStateAsync();

            Assert.Multiple(() =>
            {
                Assert.That(middlewareClient.RefreshTokenCallCount, Is.EqualTo(1));
                Assert.That(authenticationState.User.Identity?.IsAuthenticated, Is.True);
                Assert.That(userConfig.User.Dn, Is.EqualTo(RecordingApiConnection.TestUserDn));
                Assert.That(apiConnection.ReconnectSubscriptionsCallCount, Is.EqualTo(1));
            });

            await coordinator.StopAsync();
        }

        [Test]
        public async Task StartAsync_WhenNoRefreshTokenExists_DoesNotAttemptRestore()
        {
            MockMiddlewareClient middlewareClient = new();
            MockProtectedSessionStorage sessionStorage = new();
            TokenService tokenService = new(middlewareClient, sessionStorage);
            EventMediator eventMediator = new();
            TestNavigationManager navigationManager = new();
            AuthStateProvider authStateProvider = new(tokenService, eventMediator, navigationManager);
            TokenRefreshCoordinator coordinator = new(
                tokenService,
                authStateProvider,
                new RecordingApiConnection(),
                middlewareClient,
                new UserConfig(),
                new TrackingPeriodicTaskRunnerFactory(),
                navigationManager);

            await tokenService.SetTokenPair(new TokenPair
            {
                AccessToken = "access-only",
                RefreshToken = "",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(10),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
            });

            await coordinator.StartAsync();

            Assert.That(middlewareClient.RefreshTokenCallCount, Is.EqualTo(0));

            await coordinator.StopAsync();
        }

        [Test]
        public async Task StartAsync_WhenSessionRestoreFails_ClearsStoredTokens()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey privateKey = new(rsa.ExportParameters(true));
            RsaSecurityKey publicKey = new(rsa.ExportParameters(false));
            JwtPrivateKeyField.SetValue(null, privateKey);
            JwtPublicKeyField.SetValue(null, publicKey);

            MockMiddlewareClient middlewareClient = new()
            {
                ShouldRefreshSucceed = false
            };
            MockProtectedSessionStorage sessionStorage = new();
            TokenService tokenService = new(middlewareClient, sessionStorage);
            EventMediator eventMediator = new();
            TestNavigationManager navigationManager = new();
            AuthStateProvider authStateProvider = new(tokenService, eventMediator, navigationManager);
            TokenRefreshCoordinator coordinator = new(
                tokenService,
                authStateProvider,
                new RecordingApiConnection(),
                middlewareClient,
                new UserConfig(),
                new TrackingPeriodicTaskRunnerFactory(),
                navigationManager);

            await tokenService.SetTokenPair(new TokenPair
            {
                AccessToken = GenerateJwtToken(privateKey, Roles.Reporter, DateTime.UtcNow.AddMinutes(-5)),
                RefreshToken = "refresh-token",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(-5),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
            });

            await coordinator.StartAsync();

            Assert.That(middlewareClient.RefreshTokenCallCount, Is.EqualTo(1));
            Assert.That(await tokenService.GetTokenPair(), Is.Null);
            Assert.That(sessionStorage.ContainsKey("token_pair"), Is.False);

            await coordinator.StopAsync();
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

        private static string GenerateJwtToken(RsaSecurityKey privateKey, string role, DateTime expiresAtUtc)
        {
            JwtSecurityToken token = new(
                issuer: FWO.Basics.JwtConstants.Issuer,
                audience: FWO.Basics.JwtConstants.Audience,
                claims:
                [
                    new Claim(JwtRegisteredClaimNames.UniqueName, "test-user"),
                    new Claim("role", role),
                    new Claim("x-hasura-uuid", RecordingApiConnection.TestUserDn),
                    new Claim("x-hasura-tenant-id", RecordingApiConnection.TestTenantId.ToString()),
                    new Claim("x-hasura-default-role", Roles.Reporter),
                    new Claim("x-hasura-allowed-roles", "[\"reporter\"]"),
                    new Claim("x-hasura-editable-owners", "{ 3,7 }"),
                    new Claim("x-hasura-recertifiable-owners", "{ 9 }"),
                    new Claim("x-hasura-workflow-visibility-groups", "{ 2,4 }")
                ],
                expires: expiresAtUtc,
                signingCredentials: new SigningCredentials(privateKey, SecurityAlgorithms.RsaSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
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

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            internal const string TestUserDn = "cn=test-user,dc=example,dc=com";
            internal const int TestTenantId = 7;

            public int ReconnectSubscriptionsCallCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                object response = typeof(QueryResponseType) switch
                {
                    var responseType when responseType == typeof(UiUser[]) => new[]
                    {
                        new UiUser
                        {
                            DbId = 42,
                            Dn = TestUserDn,
                            Name = "test-user",
                            Language = "English"
                        }
                    },
                    var responseType when responseType == typeof(Tenant[]) => new[]
                    {
                        new Tenant
                        {
                            Id = TestTenantId,
                            Name = "Test Tenant"
                        }
                    },
                    _ => throw new NotImplementedException($"Unexpected query type {typeof(QueryResponseType).Name}. Query: {query}")
                };

                return Task.FromResult((QueryResponseType)response);
            }

            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
            {
                ReconnectSubscriptionsCallCount++;
                return Task.CompletedTask;
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
