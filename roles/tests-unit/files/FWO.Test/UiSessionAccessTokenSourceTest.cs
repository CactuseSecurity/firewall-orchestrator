using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FWO.Data.Middleware;
using FWO.Test.Mocks;
using FWO.Ui.Services;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of providing the access token of the UI session, e.g. for the AI assistant.
    /// </summary>
    [TestFixture]
    internal class UiSessionAccessTokenSourceTest
    {
        private const string kSigningKey = "ThisIsATestSecretKeyForJwtTokenGeneration123456";
        private const string kRefreshToken = "refresh-token";

        private TokenService tokenService = null!;
        private int restoreCount;

        [SetUp]
        public void SetUp()
        {
            tokenService = new TokenService(new MockMiddlewareClient(), new MockProtectedSessionStorage());
            restoreCount = 0;
        }

        [Test]
        public async Task GetAccessToken_ValidToken_ReturnsItWithoutRestoring()
        {
            string accessToken = CreateJwt(DateTime.UtcNow.AddHours(1));
            await tokenService.SetTokenPair(CreateTokenPair(accessToken));
            SessionAccessTokenSource tokenSource = new(tokenService, CountRestore);

            string? result = await tokenSource.GetAccessToken();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(accessToken));
                Assert.That(restoreCount, Is.Zero);
            });
        }

        [Test]
        public async Task GetAccessToken_ExpiringToken_RestoresTheSessionFirst()
        {
            await tokenService.SetTokenPair(CreateTokenPair(CreateJwt(DateTime.UtcNow.AddSeconds(10))));
            string refreshedToken = CreateJwt(DateTime.UtcNow.AddHours(1));
            SessionAccessTokenSource tokenSource = new(tokenService, async () =>
            {
                await CountRestore();
                await tokenService.SetTokenPair(CreateTokenPair(refreshedToken));
            });

            string? result = await tokenSource.GetAccessToken();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(refreshedToken));
                Assert.That(restoreCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task GetAccessToken_WithoutSession_ReturnsNullWithoutRestoring()
        {
            SessionAccessTokenSource tokenSource = new(tokenService, CountRestore);

            string? result = await tokenSource.GetAccessToken();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Null);
                Assert.That(restoreCount, Is.Zero);
            });
        }

        private Task CountRestore()
        {
            restoreCount++;
            return Task.CompletedTask;
        }

        private static TokenPair CreateTokenPair(string accessToken)
        {
            return new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = kRefreshToken,
                AccessTokenExpires = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
            };
        }

        private static string CreateJwt(DateTime expiresAt)
        {
            SigningCredentials credentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(kSigningKey)), SecurityAlgorithms.HmacSha256);
            JwtSecurityToken token = new(issuer: "test_issuer", audience: "test_audience", expires: expiresAt,
                notBefore: expiresAt.AddHours(-2), signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
