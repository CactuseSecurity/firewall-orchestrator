using FWO.Api.Client;
using FWO.Data.Middleware;
using FWO.Ui.Services;
using NSubstitute;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class GlobalConfigTokenRefreshServiceTest
    {
        [Test]
        public async Task EnsureFreshTokenAsync_WhenTokenIsFresh_DoesNotRequestNewToken()
        {
            TokenPair initialTokenPair = CreateTokenPair("initial-token", DateTime.UtcNow.AddMinutes(10));
            TestAnonymousTokenProvider tokenProvider = new(CreateTokenPair("unused-token", DateTime.UtcNow.AddMinutes(20)));
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            GlobalConfigTokenRefreshService service = CreateService(apiConnection, tokenProvider, initialTokenPair);

            TokenPair currentTokenPair = await service.EnsureFreshTokenAsync();

            Assert.That(currentTokenPair.AccessToken, Is.EqualTo("initial-token"));
            Assert.That(tokenProvider.RequestCount, Is.EqualTo(0));
            await apiConnection.DidNotReceive().ReconnectSubscriptionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task EnsureFreshTokenAsync_WhenTokenIsNearExpiry_RequestsNewTokenAndReconnectsSubscriptions()
        {
            TokenPair initialTokenPair = CreateTokenPair("initial-token", DateTime.UtcNow.AddSeconds(30));
            TokenPair refreshedTokenPair = CreateTokenPair("refreshed-token", DateTime.UtcNow.AddMinutes(15));
            TestAnonymousTokenProvider tokenProvider = new(refreshedTokenPair);
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            GlobalConfigTokenRefreshService service = CreateService(apiConnection, tokenProvider, initialTokenPair);

            TokenPair currentTokenPair = await service.EnsureFreshTokenAsync();

            Assert.That(currentTokenPair.AccessToken, Is.EqualTo("refreshed-token"));
            Assert.That(tokenProvider.RequestCount, Is.EqualTo(1));
            await apiConnection.Received(1).ReconnectSubscriptionsAsync("refreshed-token", Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task EnsureFreshTokenAsync_WhenForced_RefreshesFreshToken()
        {
            TokenPair initialTokenPair = CreateTokenPair("initial-token", DateTime.UtcNow.AddMinutes(10));
            TokenPair refreshedTokenPair = CreateTokenPair("forced-token", DateTime.UtcNow.AddMinutes(15));
            TestAnonymousTokenProvider tokenProvider = new(refreshedTokenPair);
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            GlobalConfigTokenRefreshService service = CreateService(apiConnection, tokenProvider, initialTokenPair);

            TokenPair currentTokenPair = await service.EnsureFreshTokenAsync(force: true);

            Assert.That(currentTokenPair.AccessToken, Is.EqualTo("forced-token"));
            Assert.That(tokenProvider.RequestCount, Is.EqualTo(1));
            await apiConnection.Received(1).ReconnectSubscriptionsAsync("forced-token", Arg.Any<CancellationToken>());
        }

        private static GlobalConfigTokenRefreshService CreateService(ApiConnection apiConnection, IAnonymousGlobalConfigTokenProvider tokenProvider, TokenPair initialTokenPair)
        {
            return new GlobalConfigTokenRefreshService(
                new GlobalConfigApiConnection(apiConnection),
                tokenProvider,
                new GlobalConfigTokenState(initialTokenPair),
                new GlobalConfigTokenRefreshOptions
                {
                    RefreshLeadTime = TimeSpan.FromMinutes(2),
                    RefreshCheckInterval = TimeSpan.FromMinutes(1)
                });
        }

        private static TokenPair CreateTokenPair(string accessToken, DateTime accessTokenExpires)
        {
            return new TokenPair
            {
                AccessToken = accessToken,
                AccessTokenExpires = accessTokenExpires
            };
        }

        private sealed class TestAnonymousTokenProvider : IAnonymousGlobalConfigTokenProvider
        {
            private readonly TokenPair tokenPair;

            public TestAnonymousTokenProvider(TokenPair tokenPair)
            {
                this.tokenPair = tokenPair;
            }

            public int RequestCount { get; private set; }

            public Task<TokenPair> CreateTokenPairAsync(CancellationToken cancellationToken)
            {
                RequestCount++;
                return Task.FromResult(tokenPair);
            }
        }
    }
}
