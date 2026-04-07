using FWO.Api.Client;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Services;
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

            Assert.That(parsedToken.Claims.Any(claim => claim.Type == "x-hasura-default-role" && claim.Value == "middleware-server"), Is.True);
            Assert.That(parsedToken.ValidTo, Is.LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(16)));
            Assert.That(parsedToken.ValidTo, Is.GreaterThan(DateTime.UtcNow.AddMinutes(10)));
        }

        [Test]
        public async Task EnsureFreshTokenAsync_WhenForced_RotatesTokenAndUpdatesApiConnectionHeader()
        {
            InternalApiTokenService tokenService = CreateTokenService();
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            JwtSecurityTokenHandler tokenHandler = new();

            tokenService.CreateInitialMiddlewareToken();

            string refreshedToken = await tokenService.EnsureFreshTokenAsync(apiConnection, force: true);
            JwtSecurityToken parsedToken = tokenHandler.ReadJwtToken(refreshedToken);

            Assert.That(parsedToken.Claims.Any(claim => claim.Type == "x-hasura-default-role" && claim.Value == "middleware-server"), Is.True);
            apiConnection.Received(1).SetAuthHeader(refreshedToken);
        }

        [Test]
        public async Task EnsureFreshTokenAsync_WhenTokenIsStillFresh_DoesNotUpdateApiConnectionHeader()
        {
            InternalApiTokenService tokenService = CreateTokenService();
            ApiConnection apiConnection = Substitute.For<ApiConnection>();

            string initialToken = tokenService.CreateInitialMiddlewareToken();

            string currentToken = await tokenService.EnsureFreshTokenAsync(apiConnection);

            Assert.That(currentToken, Is.EqualTo(initialToken));
            apiConnection.DidNotReceive().SetAuthHeader(Arg.Any<string>());
        }

        [Test]
        public async Task EnsureFreshTokenAsync_WithShortLifetimeAfterWaiting_RotatesTokenNearExpiry()
        {
            InternalApiTokenService tokenService = CreateTokenService(
                new ShortInternalLifetimeProvider(),
                new InternalApiTokenServiceOptions
                {
                    RefreshLeadTime = TimeSpan.FromSeconds(1),
                    RefreshCheckInterval = TimeSpan.FromMilliseconds(200)
                });
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            JwtSecurityTokenHandler tokenHandler = new();

            string initialToken = tokenService.CreateInitialMiddlewareToken();
            JwtSecurityToken initialParsedToken = tokenHandler.ReadJwtToken(initialToken);

            await Task.Delay(TimeSpan.FromMilliseconds(2300));

            string refreshedToken = await tokenService.EnsureFreshTokenAsync(apiConnection);
            JwtSecurityToken refreshedParsedToken = tokenHandler.ReadJwtToken(refreshedToken);

            Assert.That(refreshedParsedToken.ValidTo, Is.GreaterThan(initialParsedToken.ValidTo));
            apiConnection.Received(1).SetAuthHeader(refreshedToken);
        }

        private static InternalApiTokenService CreateTokenService(TokenLifetimeProvider? tokenLifetimeProvider = null, InternalApiTokenServiceOptions? options = null)
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey signingKey = new(rsa.ExportParameters(true));
            JwtWriter jwtWriter = new(signingKey);
            return new InternalApiTokenService(jwtWriter, tokenLifetimeProvider ?? new TokenLifetimeProvider(), options);
        }
    }
}
