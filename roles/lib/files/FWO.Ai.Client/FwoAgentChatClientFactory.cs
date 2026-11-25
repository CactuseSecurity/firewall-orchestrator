using AGUI.Client;
using FWO.Ai.Client.AgUi;
using Microsoft.Extensions.AI;

namespace FWO.Ai.Client;

/// <summary>
/// Creates the chat clients of the FWO agent on the AG-UI endpoint of the FWO AI harness, published by the middleware's
/// Apache at <c>&lt;middleware server uri&gt;agui</c>. Each chat client is the AG-UI chat client of the AG-UI .NET SDK
/// (<see cref="AGUIChatClient"/>) on a transport sending the runs with the user's JWT. The harness is stateless: a
/// caller sends the whole conversation with every run, see <see cref="AgentConversation"/>.
/// </summary>
public sealed class FwoAgentChatClientFactory : IFwoAgentChatClientFactory, IDisposable
{
    /// <summary>
    /// Path of the AG-UI endpoint relative to the middleware server uri. Keep in step with HarnessServerSetup.kAgUiPath
    /// of the harness and ai_harness_agui_path of the installer.
    /// </summary>
    public const string kAgUiPath = "agui";

    /// <summary>Default of the longest time a run may take.</summary>
    public static readonly TimeSpan kDefaultRunTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default of the maximum size of the response of a run (64 MiB): the answer, the tool calls and the snapshot of the
    /// conversation.
    /// </summary>
    internal const long kMaxResponseBytes = 64L * 1024 * 1024;

    /// <summary>Longest run timeout the cancellation of a run supports.</summary>
    internal static readonly TimeSpan kMaxRunTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>
    /// Lifetime of a pooled connection of an own HTTP client, so that a changed address of the middleware host is
    /// picked up.
    /// </summary>
    internal static readonly TimeSpan kPooledConnectionLifetime = TimeSpan.FromMinutes(2);

    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly TimeSpan runTimeout;
    private bool disposed;

    /// <summary>
    /// Creates a factory with an HTTP client of its own, which it disposes.
    /// </summary>
    /// <param name="middlewareServerUri">Uri of the middleware server, e.g. https://host:8888/</param>
    /// <param name="runTimeout">Longest time a run may take; <see cref="kDefaultRunTimeout"/> if null</param>
    /// <exception cref="ArgumentException">The uri is relative or uses another scheme than http or https.</exception>
    public FwoAgentChatClientFactory(Uri middlewareServerUri, TimeSpan? runTimeout = null)
    {
        Endpoint = AgUiEndpoint.Create(middlewareServerUri);
        this.runTimeout = ValidateRunTimeout(runTimeout);
        httpClient = CreateHttpClient();
        ownsHttpClient = true;
    }

    /// <summary>
    /// Creates a factory using the given HTTP client, which it does not dispose.
    /// </summary>
    /// <param name="httpClient">HTTP client to send the runs with</param>
    /// <param name="middlewareServerUri">Uri of the middleware server, e.g. https://host:8888/</param>
    /// <param name="runTimeout">Longest time a run may take; <see cref="kDefaultRunTimeout"/> if null</param>
    /// <exception cref="ArgumentException">The uri is relative or uses another scheme than http or https.</exception>
    public FwoAgentChatClientFactory(HttpClient httpClient, Uri middlewareServerUri, TimeSpan? runTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        Endpoint = AgUiEndpoint.Create(middlewareServerUri);
        this.runTimeout = ValidateRunTimeout(runTimeout);
        this.httpClient = httpClient;
        ownsHttpClient = false;
    }

    /// <summary>The AG-UI endpoint the runs are posted to.</summary>
    internal Uri Endpoint { get; }

    /// <summary>The HTTP client the runs are sent with.</summary>
    internal HttpClient HttpClient => httpClient;

    /// <summary>Longest time a run may take.</summary>
    internal TimeSpan RunTimeout => runTimeout;

    /// <summary>Maximum size of the response of a run, see <see cref="kMaxResponseBytes"/>.</summary>
    internal long MaxResponseBytes { get; init; } = kMaxResponseBytes;

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The factory has been disposed.</exception>
    public IChatClient CreateChatClient(Func<CancellationToken, Task<string?>> accessTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(accessTokenProvider);
        ObjectDisposedException.ThrowIf(disposed, this);
        FwoAgUiTransport transport = new(httpClient, Endpoint, accessTokenProvider, MaxResponseBytes);
        AGUIChatClient aguiChatClient = new(new AGUIChatClientOptions { Transport = transport });
        return new FwoAgentChatClient(aguiChatClient, runTimeout);
    }

    /// <summary>
    /// Disposes the HTTP client if the factory created it.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        if (ownsHttpClient)
        {
            httpClient.Dispose();
        }
    }

    /// <summary>
    /// Creates an HTTP client of its own: certificates are validated against the trust store of the host (where the
    /// FWO internal CA is installed), redirects are not followed so that the token is only sent to the endpoint, and
    /// the run timeout bounds a run instead of the timeout of the client.
    /// </summary>
    private static HttpClient CreateHttpClient()
    {
        SocketsHttpHandler handler = new() { PooledConnectionLifetime = kPooledConnectionLifetime, AllowAutoRedirect = false };
        return new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>
    /// Returns the run timeout, the default if none is given.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The timeout is not positive or too long.</exception>
    private static TimeSpan ValidateRunTimeout(TimeSpan? runTimeout)
    {
        TimeSpan timeout = runTimeout ?? kDefaultRunTimeout;
        if (timeout <= TimeSpan.Zero || timeout > kMaxRunTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(runTimeout), "The run timeout must be positive and at most 24 days.");
        }
        return timeout;
    }
}
