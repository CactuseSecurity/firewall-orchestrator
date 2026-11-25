using FWO.Api.Client;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Services;
using FWO.Services;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace FWO.Test
{
    [TestFixture]
    public class InternalApiTokenServiceTest
    {
        private static readonly TimeSpan kShortRefreshLeadTime = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan kNearExpiryPollInterval = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan kNearExpiryWaitLimit = TimeSpan.FromSeconds(30);

        private sealed class ShortInternalLifetimeProvider : TokenLifetimeProvider
        {
            public override TimeSpan GetInternalServiceTokenLifetime()
            {
                return TimeSpan.FromSeconds(3);
            }
        }

        [Test]
        public void CreateInitialMiddlewareToken_CreatesShortLivedMiddlewareToken()
        {
            InternalApiTokenService tokenService = CreateTokenService();
            JwtSecurityTokenHandler tokenHandler = new();


            string token = tokenService.CreateInitialMiddlewareToken();
            JwtSecurityToken parsedToken = tokenHandler.ReadJwtToken(token);

            TokenLifetimeProvider provider = new();
            TimeSpan internalLifetime = provider.GetInternalServiceTokenLifetime();

            Assert.That(parsedToken.Claims.Any(claim => claim.Type == "x-hasura-default-role" && claim.Value == "middleware-server"), Is.True);
            Assert.That(parsedToken.ValidTo, Is.LessThanOrEqualTo(DateTime.UtcNow.Add(internalLifetime)));
            Assert.That(parsedToken.ValidTo, Is.GreaterThan(DateTime.UtcNow.Add(internalLifetime.Subtract(TimeSpan.FromSeconds(15)))));
        }

        [Test]
        public async Task EnsureFreshTokenAsync_WhenForced_RotatesTokenAndReconnectsApiSubscriptions()
        {
            InternalApiTokenService tokenService = CreateTokenService();
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            InternalApiTokenRefreshService refreshService = new(tokenService, apiConnection);
            JwtSecurityTokenHandler tokenHandler = new();

            string initialToken = tokenService.CreateInitialMiddlewareToken();
            JwtSecurityToken initialParsedToken = tokenHandler.ReadJwtToken(initialToken);

            string refreshedToken = await refreshService.EnsureFreshTokenAsync(force: true);
            JwtSecurityToken parsedToken = tokenHandler.ReadJwtToken(refreshedToken);

            Assert.That(parsedToken.Claims.Any(claim => claim.Type == "x-hasura-default-role" && claim.Value == "middleware-server"), Is.True);
            Assert.That(parsedToken.Id, Is.Not.EqualTo(initialParsedToken.Id));
            Assert.That(refreshedToken, Is.Not.EqualTo(initialToken));
            await apiConnection.Received(1).ReconnectSubscriptionsAsync(refreshedToken, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task EnsureFreshTokenAsync_WhenTokenIsStillFresh_DoesNotUpdateApiConnectionHeader()
        {
            InternalApiTokenService tokenService = CreateTokenService();
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            InternalApiTokenRefreshService refreshService = new(tokenService, apiConnection);

            string initialToken = tokenService.CreateInitialMiddlewareToken();

            string currentToken = await refreshService.EnsureFreshTokenAsync();

            Assert.That(currentToken, Is.EqualTo(initialToken));
            await apiConnection.DidNotReceive().ReconnectSubscriptionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task EnsureFreshTokenAsync_WithShortLifetimeAfterWaiting_RotatesTokenNearExpiry()
        {
            InternalApiTokenService tokenService = CreateTokenService(new ShortInternalLifetimeProvider());
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            InternalApiTokenRefreshService refreshService = new(
                tokenService,
                apiConnection,
                new TokenRefreshOptions
                {
                    RefreshLeadTime = kShortRefreshLeadTime,
                    RefreshCheckInterval = TimeSpan.FromMilliseconds(200)
                });
            JwtSecurityTokenHandler tokenHandler = new();

            string initialToken = tokenService.CreateInitialMiddlewareToken();
            JwtSecurityToken initialParsedToken = tokenHandler.ReadJwtToken(initialToken);

            await WaitUntilWithinRefreshLeadTime(initialParsedToken.ValidTo, kShortRefreshLeadTime);

            string refreshedToken = await refreshService.EnsureFreshTokenAsync();
            JwtSecurityToken refreshedParsedToken = tokenHandler.ReadJwtToken(refreshedToken);

            Assert.That(refreshedParsedToken.ValidTo, Is.GreaterThan(initialParsedToken.ValidTo));
            Assert.That(refreshedParsedToken.Id, Is.Not.EqualTo(initialParsedToken.Id));
            await apiConnection.Received(1).ReconnectSubscriptionsAsync(refreshedToken, Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// Waits until the token expires within the refresh lead time, measured like the refresh service does: on the wall
        /// clock (DateTime.UtcNow). A fixed delay measures monotonic time instead, so it is not enough where the wall
        /// clock is stepped back while waiting, e.g. by the time synchronization of a test VM.
        /// </summary>
        private static async Task WaitUntilWithinRefreshLeadTime(DateTime expiresUtc, TimeSpan refreshLeadTime)
        {
            using CancellationTokenSource waitLimit = new(kNearExpiryWaitLimit);
            while (expiresUtc > DateTime.UtcNow.Add(refreshLeadTime))
            {
                await Task.Delay(kNearExpiryPollInterval, waitLimit.Token);
            }
        }

        private static InternalApiTokenService CreateTokenService(TokenLifetimeProvider? tokenLifetimeProvider = null)
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey signingKey = new(rsa.ExportParameters(true));
            JwtWriter jwtWriter = new(signingKey);
            return new InternalApiTokenService(jwtWriter, tokenLifetimeProvider ?? new TokenLifetimeProvider());
        }
    }
}
