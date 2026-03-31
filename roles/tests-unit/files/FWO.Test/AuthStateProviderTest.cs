using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data.Middleware;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Events;
using FWO.Test.Mocks;
using FWO.Ui.Auth;
using FWO.Ui.Services;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;

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

        private static string GenerateJwtToken(RsaSecurityKey privateKey, string role)
        {
            SigningCredentials credentials = new(privateKey, SecurityAlgorithms.RsaSha256);

            JwtSecurityToken token = new(
                issuer: FWO.Basics.JwtConstants.Issuer,
                audience: FWO.Basics.JwtConstants.Audience,
                claims:
                [
                    new Claim("role", role)
                ],
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
