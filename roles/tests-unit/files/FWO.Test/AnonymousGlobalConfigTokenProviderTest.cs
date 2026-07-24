using FWO.Data.Middleware;
using FWO.Test.Mocks;
using FWO.Ui.Services;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Reflection;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    public class AnonymousGlobalConfigTokenProviderTest
    {
        private static readonly string kTokenResponse = "{\"AccessToken\":\"access-token\",\"RefreshToken\":\"refresh-token\",\"AccessTokenExpires\":\"2026-07-24T10:15:00Z\",\"RefreshTokenExpires\":\"2026-07-24T11:15:00Z\"}";

        [Test]
        public async Task CreateTokenPairAsync_ReturnsTokenPairFromMiddlewareResponse()
        {
            TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(new SingleResponseHandler(CreateJsonResponse(HttpStatusCode.OK, kTokenResponse)));
            AnonymousGlobalConfigTokenProvider provider = CreateProvider(middlewareClient);

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
            TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(new SingleResponseHandler(CreateJsonResponse(HttpStatusCode.OK, "{\"RefreshToken\":\"refresh-token\"}")));
            AnonymousGlobalConfigTokenProvider provider = CreateProvider(middlewareClient);

            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.CreateTokenPairAsync(CancellationToken.None))!;

            Assert.That(exception.Message, Does.Contain("Could not create anonymous global config token"));
        }

        [Test]
        public void CreateTokenPairAsync_ThrowsAfterDispose()
        {
            TestMiddlewareClient middlewareClient = new();
            AnonymousGlobalConfigTokenProvider provider = CreateProvider(middlewareClient);
            provider.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(async () => await provider.CreateTokenPairAsync(CancellationToken.None));
        }

        private static AnonymousGlobalConfigTokenProvider CreateProvider(TestMiddlewareClient middlewareClient)
        {
            AnonymousGlobalConfigTokenProvider provider = new("https://middleware.example/");
            FieldInfo field = typeof(AnonymousGlobalConfigTokenProvider).GetField("middlewareClient", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(AnonymousGlobalConfigTokenProvider).FullName, "middlewareClient");
            ((IDisposable?)field.GetValue(provider))?.Dispose();
            field.SetValue(provider, middlewareClient);
            return provider;
        }

        private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string body)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        private sealed class SingleResponseHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage response;

            public SingleResponseHandler(HttpResponseMessage response)
            {
                this.response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(response);
            }
        }
    }
}
