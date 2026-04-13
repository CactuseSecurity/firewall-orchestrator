using FWO.Api.Client;
using FWO.Config.Api.Data;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Events;
using FWO.Test.Mocks;
using FWO.Ui.Auth;
using FWO.Ui.Services;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    public class AuthStateProviderTest
    {
        private static readonly FieldInfo JwtPublicKeyField = typeof(ConfigFile).GetField("jwtPublicKey", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(ConfigFile).FullName, "jwtPublicKey");

        private static readonly FieldInfo JwtPrivateKeyField = typeof(ConfigFile).GetField("jwtPrivateKey", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(ConfigFile).FullName, "jwtPrivateKey");

        private static readonly FieldInfo UserField = typeof(AuthStateProvider).GetField("user", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(AuthStateProvider).FullName, "user");

        private static readonly MethodInfo ApplyTokenPairMethod = typeof(AuthStateProvider).GetMethod("ApplyTokenPair", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(typeof(AuthStateProvider).FullName, "ApplyTokenPair");

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
        public async Task RefreshAuthenticationState_WhenNoSessionExists_ShouldNotPublishJwtExpiredEvent()
        {
            MockMiddlewareClient mockMiddlewareClient = new();
            MockProtectedSessionStorage mockSessionStorage = new();
            EventMediator eventMediator = new();
            TokenService tokenService = new(mockMiddlewareClient, mockSessionStorage);
            AuthStateProvider authStateProvider = new(tokenService, eventMediator);

            int publishCount = 0;
            eventMediator.Subscribe<JwtExpiredEvent>(nameof(JwtExpiredEvent), _ => publishCount++);

            bool refreshed = await authStateProvider.RefreshAuthenticationState(new MockApiConnection(), mockMiddlewareClient, new UserConfig(), new CircuitHandlerService(eventMediator));

            Assert.That(refreshed, Is.False);
            Assert.That(publishCount, Is.EqualTo(0));
        }

        [Test]
        public async Task RefreshAuthenticationState_WhenAuthenticatedRefreshFails_ShouldPublishJwtExpiredEvent()
        {
            const string userDn = "cn=test-user,dc=example,dc=com";

            MockMiddlewareClient mockMiddlewareClient = new()
            {
                ShouldRefreshSucceed = false
            };
            MockProtectedSessionStorage mockSessionStorage = new();
            EventMediator eventMediator = new();
            TokenService tokenService = new(mockMiddlewareClient, mockSessionStorage);
            AuthStateProvider authStateProvider = new(tokenService, eventMediator);
            UserConfig userConfig = new();
            userConfig.User.Dn = userDn;

            await tokenService.SetTokenPair(new TokenPair
            {
                AccessToken = "expired-token",
                RefreshToken = "refresh-token",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(-5),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
            });
            SetAuthenticatedUser(authStateProvider, userDn);

            string? publishedUserDn = null;
            int publishCount = 0;
            eventMediator.Subscribe<JwtExpiredEvent>(nameof(JwtExpiredEvent), _ =>
            {
                publishCount++;
                publishedUserDn = _.EventArgs?.UserDn;
            });

            bool refreshed = await authStateProvider.RefreshAuthenticationState(new MockApiConnection(), mockMiddlewareClient, userConfig, new CircuitHandlerService(eventMediator));

            Assert.That(refreshed, Is.False);
            Assert.That(publishCount, Is.EqualTo(1));
            Assert.That(publishedUserDn, Is.EqualTo(userDn));
        }

        [Test]
        public async Task ApplyTokenPair_WhenJwtIsRejected_ShouldNotPersistTokenPair()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey privateKey = new(rsa.ExportParameters(true));
            RsaSecurityKey publicKey = new(rsa.ExportParameters(false));
            JwtPrivateKeyField.SetValue(null, privateKey);
            JwtPublicKeyField.SetValue(null, publicKey);

            MockMiddlewareClient mockMiddlewareClient = new();
            MockProtectedSessionStorage mockSessionStorage = new();
            EventMediator eventMediator = new();
            TokenService tokenService = new(mockMiddlewareClient, mockSessionStorage);
            AuthStateProvider authStateProvider = new(tokenService, eventMediator);

            TokenPair rejectedTokenPair = new()
            {
                AccessToken = GenerateJwtToken(privateKey, Roles.Anonymous),
                RefreshToken = "rejected-refresh-token",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(10),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
            };

            AuthenticationException ex = Assert.ThrowsAsync<AuthenticationException>(async () =>
                await InvokeApplyTokenPair(authStateProvider, rejectedTokenPair, new MockApiConnection(), mockMiddlewareClient, new UserConfig(), new CircuitHandlerService(eventMediator)))
                ?? throw new AssertionException("Expected AuthenticationException.");

            Assert.That(ex.Message, Is.EqualTo("not_authorized"));
            Assert.That(await tokenService.GetAccessToken(), Is.Null);
            Assert.That(mockSessionStorage.ContainsKey("token_pair"), Is.False);
        }

        [Test]
        public async Task RestoreAuthenticationState_WhenAccessTokenExpired_ShouldRefreshAndAuthenticate()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey privateKey = new(rsa.ExportParameters(true));
            RsaSecurityKey publicKey = new(rsa.ExportParameters(false));
            JwtPrivateKeyField.SetValue(null, privateKey);
            JwtPublicKeyField.SetValue(null, publicKey);

            MockMiddlewareClient mockMiddlewareClient = new();
            MockProtectedSessionStorage mockSessionStorage = new();
            EventMediator eventMediator = new();
            TokenService tokenService = new(mockMiddlewareClient, mockSessionStorage);
            AuthStateProvider authStateProvider = new(tokenService, eventMediator);
            UserConfig userConfig = new();
            TestApiConnection apiConnection = new();
            CircuitHandlerService circuitHandler = new(eventMediator);

            await tokenService.SetTokenPair(new TokenPair
            {
                AccessToken = GenerateJwtToken(privateKey, Roles.Reporter, DateTime.UtcNow.AddMinutes(-5), BuildJwtClaims()),
                RefreshToken = "refresh-token",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(-5),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
            });

            string refreshedAccessToken = GenerateJwtToken(privateKey, Roles.Reporter, DateTime.UtcNow.AddMinutes(10), BuildJwtClaims());
            TokenPair refreshedTokenPair = new()
            {
                AccessToken = refreshedAccessToken,
                RefreshToken = "rotated-refresh-token",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(10),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(2)
            };

            mockMiddlewareClient.NextRefreshTokenResponse = refreshedTokenPair;

            bool restored = await authStateProvider.RestoreAuthenticationState(apiConnection, mockMiddlewareClient, userConfig, circuitHandler);
            AuthenticationState authenticationState = await authStateProvider.GetAuthenticationStateAsync();
            TokenPair? storedTokenPair = await tokenService.GetTokenPair();

            Assert.That(restored, Is.True);
            Assert.That(authenticationState.User.Identity?.IsAuthenticated, Is.True);
            Assert.That(mockMiddlewareClient.RefreshTokenCallCount, Is.EqualTo(1));
            Assert.That(storedTokenPair?.AccessToken, Is.EqualTo(refreshedAccessToken));
            Assert.That(storedTokenPair?.RefreshToken, Is.EqualTo("rotated-refresh-token"));
            Assert.That(userConfig.User.Dn, Is.EqualTo(TestApiConnection.TestUserDn));
            Assert.That(userConfig.User.Tenant?.Id, Is.EqualTo(TestApiConnection.TestTenantId));
            Assert.That(userConfig.User.Roles, Is.EquivalentTo(new[] { Roles.Reporter }));
            Assert.That(userConfig.User.Ownerships, Is.EquivalentTo(new[] { 3, 7 }));
            Assert.That(userConfig.User.RecertOwnerships, Is.EquivalentTo(new[] { 9 }));
            Assert.That(circuitHandler.User?.Dn, Is.EqualTo(TestApiConnection.TestUserDn));
        }

        [Test]
        public async Task RestoreAuthenticationState_WhenRefreshFails_ShouldClearStoredTokenPair()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey privateKey = new(rsa.ExportParameters(true));
            RsaSecurityKey publicKey = new(rsa.ExportParameters(false));
            JwtPrivateKeyField.SetValue(null, privateKey);
            JwtPublicKeyField.SetValue(null, publicKey);

            MockMiddlewareClient mockMiddlewareClient = new()
            {
                ShouldRefreshSucceed = false
            };
            MockProtectedSessionStorage mockSessionStorage = new();
            EventMediator eventMediator = new();
            TokenService tokenService = new(mockMiddlewareClient, mockSessionStorage);
            AuthStateProvider authStateProvider = new(tokenService, eventMediator);

            await tokenService.SetTokenPair(new TokenPair
            {
                AccessToken = GenerateJwtToken(privateKey, Roles.Reporter, DateTime.UtcNow.AddMinutes(-5), BuildJwtClaims()),
                RefreshToken = "refresh-token",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(-5),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
            });

            bool restored = await authStateProvider.RestoreAuthenticationState(new TestApiConnection(), mockMiddlewareClient, new UserConfig(), new CircuitHandlerService(eventMediator));
            AuthenticationState authenticationState = await authStateProvider.GetAuthenticationStateAsync();

            Assert.That(restored, Is.False);
            Assert.That(authenticationState.User.Identity?.IsAuthenticated, Is.False);
            Assert.That(await tokenService.GetTokenPair(), Is.Null);
            Assert.That(mockSessionStorage.ContainsKey("token_pair"), Is.False);
            Assert.That(mockMiddlewareClient.RefreshTokenCallCount, Is.EqualTo(1));
            Assert.That(mockMiddlewareClient.RevokeRefreshTokenCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RestoreAuthenticationState_WhenAccessTokenIsStillValid_ShouldNotRefresh()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey privateKey = new(rsa.ExportParameters(true));
            RsaSecurityKey publicKey = new(rsa.ExportParameters(false));
            JwtPrivateKeyField.SetValue(null, privateKey);
            JwtPublicKeyField.SetValue(null, publicKey);

            MockMiddlewareClient mockMiddlewareClient = new();
            MockProtectedSessionStorage mockSessionStorage = new();
            EventMediator eventMediator = new();
            TokenService tokenService = new(mockMiddlewareClient, mockSessionStorage);
            AuthStateProvider authStateProvider = new(tokenService, eventMediator);
            UserConfig userConfig = new();

            string validAccessToken = GenerateJwtToken(privateKey, Roles.Reporter, DateTime.UtcNow.AddMinutes(10), BuildJwtClaims());
            await tokenService.SetTokenPair(new TokenPair
            {
                AccessToken = validAccessToken,
                RefreshToken = "refresh-token",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(10),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
            });

            bool restored = await authStateProvider.RestoreAuthenticationState(new TestApiConnection(), mockMiddlewareClient, userConfig, new CircuitHandlerService(eventMediator));
            TokenPair? storedTokenPair = await tokenService.GetTokenPair();

            Assert.That(restored, Is.True);
            Assert.That(mockMiddlewareClient.RefreshTokenCallCount, Is.EqualTo(0));
            Assert.That(storedTokenPair?.AccessToken, Is.EqualTo(validAccessToken));
            Assert.That(userConfig.User.Dn, Is.EqualTo(TestApiConnection.TestUserDn));
        }

        private static void SetAuthenticatedUser(AuthStateProvider authStateProvider, string userDn)
        {
            ClaimsIdentity identity = new(
            [
                new Claim("x-hasura-uuid", userDn)
            ], "Test");
            ClaimsPrincipal principal = new(identity);
            UserField.SetValue(authStateProvider, principal);
        }

        private static async Task InvokeApplyTokenPair(AuthStateProvider authStateProvider, TokenPair tokenPair, ApiConnection apiConnection, MockMiddlewareClient middlewareClient, UserConfig userConfig, CircuitHandlerService circuitHandler)
        {
            Task applyTask = (Task)(ApplyTokenPairMethod.Invoke(authStateProvider, new object[] { tokenPair, apiConnection, middlewareClient, userConfig, circuitHandler })
                ?? throw new InvalidOperationException("ApplyTokenPair returned null."));

            await applyTask;
        }

        private static string GenerateJwtToken(RsaSecurityKey privateKey, string role, DateTime? expires = null, IEnumerable<Claim>? additionalClaims = null)
        {
            SigningCredentials credentials = new(privateKey, SecurityAlgorithms.RsaSha256);
            List<Claim> claims = [new Claim("role", role)];

            if (additionalClaims != null)
            {
                claims.AddRange(additionalClaims);
            }

            JwtSecurityToken token = new(
                issuer: FWO.Basics.JwtConstants.Issuer,
                audience: FWO.Basics.JwtConstants.Audience,
                claims: claims,
                expires: expires ?? DateTime.UtcNow.AddMinutes(10),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static List<Claim> BuildJwtClaims()
        {
            return
            [
                new Claim(JwtRegisteredClaimNames.UniqueName, "test-user"),
                new Claim("x-hasura-uuid", TestApiConnection.TestUserDn),
                new Claim("x-hasura-tenant-id", TestApiConnection.TestTenantId.ToString()),
                new Claim("x-hasura-allowed-roles", JsonSerializer.Serialize(new[] { Roles.Reporter })),
                new Claim("x-hasura-editable-owners", "{ 3,7 }"),
                new Claim("x-hasura-recertifiable-owners", "{ 9 }")
            ];
        }

        private sealed class TestApiConnection : SimulatedApiConnection
        {
            internal const string TestUserDn = "cn=test-user,dc=example,dc=com";
            internal const int TestTenantId = 7;

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
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

            public override void SetAuthHeader(string jwt)
            { }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                if (typeof(SubscriptionResponseType) == typeof(ConfigItem[]))
                {
                    subscriptionUpdateHandler((SubscriptionResponseType)(object)Array.Empty<ConfigItem>());
                }

                return new SimulatedApiSubscription<SubscriptionResponseType>(
                    this,
                    new GraphQLHttpClient(new GraphQLHttpClientOptions(), new SystemTextJsonSerializer(), new HttpClient()),
                    new GraphQLRequest(subscription, variables, operationName),
                    exceptionHandler,
                    subscriptionUpdateHandler);
            }
        }
    }
}
