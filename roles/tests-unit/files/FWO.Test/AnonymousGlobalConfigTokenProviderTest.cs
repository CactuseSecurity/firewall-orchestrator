using System.Net;
using FWO.Data.Middleware;
using FWO.Test.Mocks;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AnonymousGlobalConfigTokenProviderTest
    {
        private static readonly string kTokenResponse = "{\"AccessToken\":\"access-token\",\"RefreshToken\":\"refresh-token\",\"AccessTokenExpires\":\"2026-07-24T10:15:00Z\",\"RefreshTokenExpires\":\"2026-07-24T11:15:00Z\"}";

        [Test]
        public async Task CreateTokenPairAsync_ReturnsTokenPairFromMiddlewareResponse()
        {
            using TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.OK, kTokenResponse));
            using AnonymousGlobalConfigTokenProvider provider = new(middlewareClient);

            TokenPair tokenPair = await provider.CreateTokenPairAsync(CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(tokenPair.AccessToken, Is.EqualTo("access-token"));
                Assert.That(tokenPair.RefreshToken, Is.EqualTo("refresh-token"));
            });
        }

        [Test]
        public void CreateTokenPairAsync_ThrowsWhenAccessTokenIsMissing()
        {
            using TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.OK, "{\"RefreshToken\":\"refresh-token\"}"));
            using AnonymousGlobalConfigTokenProvider provider = new(middlewareClient);

            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(() => provider.CreateTokenPairAsync(CancellationToken.None))!;

            Assert.That(exception.Message, Does.Contain("Could not create anonymous global config token"));
        }

        [Test]
        public void CreateTokenPairAsync_ThrowsOnNonSuccessStatus()
        {
            using TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.InternalServerError, kTokenResponse));
            using AnonymousGlobalConfigTokenProvider provider = new(middlewareClient);

            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(() => provider.CreateTokenPairAsync(CancellationToken.None))!;

            Assert.That(exception.Message, Does.Contain("Could not create anonymous global config token"));
        }

        [Test]
        public void CreateTokenPairAsync_ThrowsAfterDispose()
        {
            using TestMiddlewareClient middlewareClient = new();
            AnonymousGlobalConfigTokenProvider provider = new(middlewareClient);
            provider.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(() => provider.CreateTokenPairAsync(CancellationToken.None));
        }
    }
}
