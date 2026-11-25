namespace FWO.Ai.Harness.Mcp;

/// <summary>
/// Settings of the connection to the FWO MCP server (FWO.Ai.Mcp).
/// </summary>
public class McpConnectionSettings
{
    /// <summary>
    /// MCP endpoint, e.g. https://&lt;middleware host&gt;:8888/mcp. Must use https unless it is a loopback address,
    /// as the user's JWT is sent to it.
    /// </summary>
    public required Uri Endpoint { get; init; }

    /// <summary>
    /// Returns the JWT of the calling FWO user. It is asked for on every HTTP request, so a renewed token is used
    /// as soon as it is available. The MCP server applies this user's roles to the tools and prompts.
    /// </summary>
    public required Func<CancellationToken, ValueTask<string>> AccessTokenProvider { get; init; }

    /// <summary>Timeout for connecting to the MCP server; the MCP client's default if not set.</summary>
    public TimeSpan? ConnectionTimeout { get; init; }
}
