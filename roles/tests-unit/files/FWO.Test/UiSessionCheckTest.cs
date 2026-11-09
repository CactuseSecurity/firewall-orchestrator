using System.Net;
using FWO.Data.Middleware;
using FWO.Test.Mocks;
using FWO.Ui.Services;
using NUnit.Framework;
using RestSharp;

namespace FWO.Test
{
    /// <summary>
    /// Tests the session check of the token service.
    /// </summary>
    [TestFixture]
    internal class UiSessionCheckTest
    {
        private const string kRefreshToken = "refresh-token";

        [TestCase(HttpStatusCode.OK, "true", SessionCheckOutcome.Live)]
        [TestCase(HttpStatusCode.OK, " false ", SessionCheckOutcome.Ended)]
        [TestCase(HttpStatusCode.OK, "not a bool", SessionCheckOutcome.Unknown)]
        [TestCase(HttpStatusCode.OK, "", SessionCheckOutcome.Unknown)]
        [TestCase(HttpStatusCode.OK, null, SessionCheckOutcome.Unknown)]
        [TestCase(HttpStatusCode.OK, "{}", SessionCheckOutcome.Unknown)]
        [TestCase(HttpStatusCode.OK, " TRUE ", SessionCheckOutcome.Live)]
        [TestCase(HttpStatusCode.Unauthorized, "", SessionCheckOutcome.Ended)]
        [TestCase(HttpStatusCode.Forbidden, "", SessionCheckOutcome.Ended)]
        [TestCase(HttpStatusCode.ServiceUnavailable, "", SessionCheckOutcome.Unknown)]
        [TestCase(HttpStatusCode.TooManyRequests, "", SessionCheckOutcome.Unknown)]
        public async Task CheckSession_InterpretsTheAnswer(HttpStatusCode statusCode, string? content, SessionCheckOutcome expected)
        {
            MockMiddlewareClient middlewareClient = new()
            {
                SessionCheckResponse = MockMiddlewareClient.CreateSessionCheckResponse(statusCode, content)
            };
            TokenService tokenService = await CreateLoggedInTokenService(middlewareClient);

            SessionCheckOutcome outcome = await tokenService.CheckSession();

            Assert.Multiple(() =>
            {
                Assert.That(outcome, Is.EqualTo(expected));
                Assert.That(middlewareClient.LastSessionCheckRequest?.RefreshToken, Is.EqualTo(kRefreshToken));
            });
        }

        [Test]
        public async Task CheckSession_IsUnknownWithoutAnswer()
        {
            MockMiddlewareClient middlewareClient = new()
            {
                SessionCheckResponse = new RestResponse<bool>(new RestRequest()) { ResponseStatus = ResponseStatus.Error }
            };
            TokenService tokenService = await CreateLoggedInTokenService(middlewareClient);

            Assert.That(await tokenService.CheckSession(), Is.EqualTo(SessionCheckOutcome.Unknown));
        }

        [Test]
        public async Task CheckSession_IsUnknownWhenTheCallFails()
        {
            MockMiddlewareClient middlewareClient = new() { SessionCheckException = new HttpRequestException("connection refused") };
            TokenService tokenService = await CreateLoggedInTokenService(middlewareClient);

            Assert.That(await tokenService.CheckSession(), Is.EqualTo(SessionCheckOutcome.Unknown));
        }

        [Test]
        public async Task CheckSession_DoesNotAskWithoutRefreshToken()
        {
            MockMiddlewareClient middlewareClient = new();
            TokenService tokenService = new(middlewareClient, new MockProtectedSessionStorage());

            SessionCheckOutcome outcome = await tokenService.CheckSession();

            Assert.Multiple(() =>
            {
                Assert.That(outcome, Is.EqualTo(SessionCheckOutcome.Unknown));
                Assert.That(middlewareClient.CheckSessionCallCount, Is.Zero);
            });
        }

        [Test]
        public async Task ClearStoredTokenPair_DropsTheTokensWithoutRevokingThem()
        {
            MockMiddlewareClient middlewareClient = new();
            MockProtectedSessionStorage sessionStorage = new();
            TokenService tokenService = new(middlewareClient, sessionStorage);
            await tokenService.SetTokenPair(new TokenPair { AccessToken = "access", RefreshToken = kRefreshToken });

            await tokenService.ClearStoredTokenPair();

            Assert.Multiple(async () =>
            {
                Assert.That(await tokenService.GetTokenPair(), Is.Null);
                Assert.That(sessionStorage.ContainsKey("token_pair"), Is.False);
                Assert.That(middlewareClient.RevokeRefreshTokenCallCount, Is.Zero);
            });
        }

        private static async Task<TokenService> CreateLoggedInTokenService(MockMiddlewareClient middlewareClient)
        {
            TokenService tokenService = new(middlewareClient, new MockProtectedSessionStorage());
            await tokenService.SetTokenPair(new TokenPair
            {
                AccessToken = "access",
                RefreshToken = kRefreshToken,
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(10),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(1)
            });
            return tokenService;
        }
    }
}
