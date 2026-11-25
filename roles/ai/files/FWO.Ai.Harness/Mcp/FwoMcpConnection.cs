using FWO.Basics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace FWO.Ai.Harness.Mcp;

/// <summary>
/// Connection to the FWO MCP server (FWO.Ai.Mcp), which offers the tools and prompts of the harness.
/// Every request carries the calling user's JWT, so the MCP server applies that user's roles to tools and prompts.
/// </summary>
public sealed class FwoMcpConnection : IAsyncDisposable
{
    /// <summary>Client name sent to the MCP server.</summary>
    internal const string kClientName = "FWO.Ai.Harness";

    private const string kUnknownVersion = "0.0.0";
    private const string kMcpEndpointPurpose = "MCP endpoint";

    private readonly McpClient mcpClient;
    private readonly HttpClientTransport transport;

    /// <summary>
    /// Wraps a connected MCP client and the transport it was created with.
    /// </summary>
    private FwoMcpConnection(McpClient mcpClient, HttpClientTransport transport)
    {
        this.mcpClient = mcpClient;
        this.transport = transport;
    }

    /// <summary>
    /// Connects to the MCP server. The endpoint is checked before any request is sent, as each request carries the JWT.
    /// </summary>
    /// <param name="settings">Endpoint and the provider of the user's JWT</param>
    /// <param name="innerHandler">Handler sending the requests, e.g. of a test server; a new
    /// <see cref="SocketsHttpHandler"/> if null. The connection takes ownership of it.</param>
    /// <param name="loggerFactory">Logger factory of the MCP client</param>
    /// <param name="cancellationToken">Cancels connecting</param>
    /// <exception cref="ArgumentException">The endpoint is not https and not a loopback address.</exception>
    public static async Task<FwoMcpConnection> ConnectAsync(McpConnectionSettings settings, HttpMessageHandler? innerHandler = null,
        ILoggerFactory? loggerFactory = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settings.Endpoint);
        ArgumentNullException.ThrowIfNull(settings.AccessTokenProvider);
        EndpointGuard.RequireHttpsOrLoopback(settings.Endpoint, kMcpEndpointPurpose);

        BearerTokenHandler tokenHandler = new(settings.AccessTokenProvider) { InnerHandler = innerHandler ?? new SocketsHttpHandler() };
        HttpClient httpClient = new(tokenHandler);
        HttpClientTransport transport = new(CreateTransportOptions(settings), httpClient, loggerFactory, ownsHttpClient: true);
        try
        {
            McpClient mcpClient = await McpClient.CreateAsync(transport, CreateClientOptions(), loggerFactory, cancellationToken);
            return new FwoMcpConnection(mcpClient, transport);
        }
        catch
        {
            await transport.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Lists the tools the user may call. The tools are AI functions that call the MCP server through this connection,
    /// i.e. with the user's JWT.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request</param>
    public async Task<List<AITool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
        return mcpTools.Cast<AITool>().ToList();
    }

    /// <summary>
    /// Gets a prompt with the given arguments and returns its messages.
    /// </summary>
    /// <param name="promptName">Name of the prompt</param>
    /// <param name="arguments">Arguments of the prompt, by argument name</param>
    /// <param name="cancellationToken">Cancels the request</param>
    /// <exception cref="ArgumentException">The prompt name is blank.</exception>
    /// <exception cref="McpException">The MCP server refused the prompt or its arguments.</exception>
    public async Task<List<ChatMessage>> GetPromptMessagesAsync(string promptName, IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promptName);
        GetPromptResult promptResult = await mcpClient.GetPromptAsync(promptName, arguments, cancellationToken: cancellationToken);
        IList<ChatMessage> messages = promptResult.ToChatMessages();
        return messages.ToList();
    }

    /// <summary>
    /// Closes the connection and frees the HTTP client.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await mcpClient.DisposeAsync();
        await transport.DisposeAsync();
    }

    /// <summary>
    /// Creates the transport options: streamable HTTP to the given endpoint.
    /// </summary>
    private static HttpClientTransportOptions CreateTransportOptions(McpConnectionSettings settings)
    {
        HttpClientTransportOptions transportOptions = new()
        {
            Endpoint = settings.Endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            Name = kClientName
        };
        if (settings.ConnectionTimeout is TimeSpan connectionTimeout)
        {
            transportOptions.ConnectionTimeout = connectionTimeout;
        }
        return transportOptions;
    }

    /// <summary>
    /// Creates the client options, telling the MCP server the name and version of the harness.
    /// </summary>
    private static McpClientOptions CreateClientOptions()
    {
        string version = typeof(FwoMcpConnection).Assembly.GetName().Version?.ToString() ?? kUnknownVersion;
        return new McpClientOptions
        {
            ClientInfo = new Implementation { Name = kClientName, Version = version }
        };
    }
}
