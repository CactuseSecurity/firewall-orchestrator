using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FWO.Data.Middleware;
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
            await using LocalMiddlewareServer server = new();
            server.EnqueueResponse(kTokenResponse);
            using AnonymousGlobalConfigTokenProvider provider = new(server.BaseUrl);

            TokenPair tokenPair = await provider.CreateTokenPairAsync(CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(tokenPair.AccessToken, Is.EqualTo("access-token"));
                Assert.That(tokenPair.RefreshToken, Is.EqualTo("refresh-token"));
            });
        }

        [Test]
        public async Task CreateTokenPairAsync_ThrowsWhenAccessTokenIsMissing()
        {
            await using LocalMiddlewareServer server = new();
            server.EnqueueResponse("{\"RefreshToken\":\"refresh-token\"}");
            using AnonymousGlobalConfigTokenProvider provider = new(server.BaseUrl);

            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.CreateTokenPairAsync(CancellationToken.None))!;

            Assert.That(exception.Message, Does.Contain("Could not create anonymous global config token"));
        }

        [Test]
        public async Task CreateTokenPairAsync_ThrowsAfterDispose()
        {
            await using LocalMiddlewareServer server = new();
            using AnonymousGlobalConfigTokenProvider provider = new(server.BaseUrl);
            provider.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(async () => await provider.CreateTokenPairAsync(CancellationToken.None));
        }

        private sealed class LocalMiddlewareServer : IAsyncDisposable
        {
            private readonly HttpListener listener = new();
            private readonly ConcurrentQueue<string> responses = new();
            private readonly CancellationTokenSource cancellationTokenSource = new();
            private readonly Task listenerTask;

            public string BaseUrl { get; }

            public LocalMiddlewareServer()
            {
                int port = GetFreePort();
                BaseUrl = $"http://127.0.0.1:{port}/";
                listener.Prefixes.Add($"http://127.0.0.1:{port}/api/");
                listener.Start();
                listenerTask = Task.Run(ListenAsync);
            }

            public void EnqueueResponse(string body)
            {
                responses.Enqueue(body);
            }

            private async Task ListenAsync()
            {
                try
                {
                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        HttpListenerContext context = await listener.GetContextAsync();
                        string body = responses.TryDequeue(out string? responseBody) ? responseBody : "{}";
                        byte[] bytes = Encoding.UTF8.GetBytes(body);

                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = bytes.Length;
                        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                        context.Response.OutputStream.Close();
                    }
                }
                catch (HttpListenerException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            public async ValueTask DisposeAsync()
            {
                cancellationTokenSource.Cancel();
                listener.Close();
                try
                {
                    await listenerTask;
                }
                catch
                {
                }

                cancellationTokenSource.Dispose();
            }

            private static int GetFreePort()
            {
                TcpListener tcpListener = new(IPAddress.Loopback, 0);
                tcpListener.Start();
                int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
                tcpListener.Stop();
                return port;
            }
        }
    }
}
