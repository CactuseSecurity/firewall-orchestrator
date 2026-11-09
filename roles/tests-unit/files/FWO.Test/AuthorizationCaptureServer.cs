using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FWO.Test
{
    /// <summary>
    /// Minimal HTTP server on the loopback interface that answers with fixed responses and records the Authorization
    /// header of every request, so tests can check what a REST client really sends.
    /// </summary>
    internal sealed class AuthorizationCaptureServer : IDisposable
    {
        private const string kNoHeader = "";

        private readonly HttpListener listener = new();
        private readonly Dictionary<string, (HttpStatusCode StatusCode, string Content)> responses;
        private readonly Task serveTask;

        /// <summary>
        /// Base url of the server, ending with a slash.
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Path (without query) and Authorization header of every request received, in order.
        /// </summary>
        public ConcurrentQueue<(string Path, string Authorization)> Requests { get; } = new();

        /// <summary>
        /// Starts the server on a free port.
        /// </summary>
        /// <param name="responses">Response status and body per path (relative to the base url, without query).</param>
        public AuthorizationCaptureServer(Dictionary<string, (HttpStatusCode StatusCode, string Content)> responses)
        {
            this.responses = responses;
            BaseUrl = $"http://127.0.0.1:{GetFreePort()}/";
            listener.Prefixes.Add(BaseUrl);
            listener.Start();
            serveTask = Task.Run(Serve);
        }

        private static int GetFreePort()
        {
            TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private async Task Serve()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    return;
                }
                string path = context.Request.Url?.AbsolutePath.TrimStart('/') ?? "";
                Requests.Enqueue((path, context.Request.Headers["Authorization"] ?? kNoHeader));
                (HttpStatusCode statusCode, string content) = responses.TryGetValue(path, out var response) ? response : (HttpStatusCode.NotFound, "");
                byte[] body = Encoding.UTF8.GetBytes(content);
                context.Response.StatusCode = (int)statusCode;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        public void Dispose()
        {
            listener.Stop();
            listener.Close();
            serveTask.Wait(TimeSpan.FromSeconds(5));
        }
    }
}
