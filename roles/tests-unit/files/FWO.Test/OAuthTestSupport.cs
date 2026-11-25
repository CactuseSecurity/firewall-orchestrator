using System.Net;
using System.Text;

namespace FWO.Test
{
    /// <summary>
    /// Simulated OAuth 2.0 token endpoint: records every request and answers with the queued answers in turn. An answer
    /// can also be an exception, e.g. a connection failure, or wait until a test releases it.
    /// </summary>
    internal sealed class OAuthTestEndpoint : HttpMessageHandler
    {
        /// <summary>A request as the token endpoint saw it.</summary>
        internal sealed record RecordedRequest(HttpMethod Method, string Url, string Body, string? Accept, string? Authorization);

        internal Queue<Func<HttpResponseMessage>> Answers { get; } = new();

        internal List<RecordedRequest> Requests { get; } = [];

        /// <summary>If set, every answer waits until this task completes.</summary>
        internal Task? Gate { get; set; }

        internal bool IsDisposed { get; private set; }

        internal void Answer(HttpStatusCode statusCode, string content)
        {
            Answers.Enqueue(() => new HttpResponseMessage(statusCode) { Content = new StringContent(content, Encoding.UTF8, "application/json") });
        }

        internal void AnswerToken(string accessToken, long? expiresInSeconds = null)
        {
            string expiresIn = expiresInSeconds == null ? "" : $",\"expires_in\":{expiresInSeconds}";
            Answer(HttpStatusCode.OK, $"{{\"access_token\":\"{accessToken}\",\"token_type\":\"Bearer\"{expiresIn}}}");
        }

        internal void Fail(Exception exception)
        {
            Answers.Enqueue(() => throw exception);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Requests)
            {
                Requests.Add(new RecordedRequest(request.Method, request.RequestUri?.AbsoluteUri ?? "", body,
                    request.Headers.Accept.ToString(), request.Headers.Authorization?.ToString()));
            }
            if (Gate != null)
            {
                await Gate.WaitAsync(cancellationToken);
            }
            Func<HttpResponseMessage> answer;
            lock (Answers)
            {
                answer = Answers.Dequeue();
            }
            return answer();
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Clock that only moves when a test moves it.
    /// </summary>
    internal sealed class OAuthTestClock(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>
    /// Content whose stream fails while it is read, like a connection that drops in the middle of the answer.
    /// </summary>
    internal sealed class FailingReadContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new IOException("connection reset");

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new FailingReadStream());

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        private sealed class FailingReadStream : MemoryStream
        {
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("connection reset");
        }
    }
}
