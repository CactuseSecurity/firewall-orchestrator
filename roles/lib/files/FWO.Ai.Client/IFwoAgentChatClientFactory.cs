using Microsoft.Extensions.AI;

namespace FWO.Ai.Client;

/// <summary>
/// Creates the chat clients of the FWO agent: <see cref="IChatClient"/>s on the AG-UI endpoint of the FWO AI harness
/// (FWO.Ai.Harness). Each run is sent with the JWT of the user, so the harness and the MCP server behind it apply that
/// user's roles and permissions.
/// </summary>
public interface IFwoAgentChatClientFactory
{
    /// <summary>
    /// Creates a chat client sending its runs with the access token of one user. The chat client is cheap: create one
    /// per user (e.g. per UI session) or per run. It does not need to be disposed; the HTTP client belongs to the factory.
    /// </summary>
    /// <param name="accessTokenProvider">Returns the JWT of the user for each run, refreshed if needed; null if the user
    /// has none, which fails the run with <see cref="AgentClientFailure.Unauthorized"/></param>
    /// <returns>The chat client. Its calls throw an <see cref="AgentClientException"/> for a run that could not be done.</returns>
    IChatClient CreateChatClient(Func<CancellationToken, Task<string?>> accessTokenProvider);
}
