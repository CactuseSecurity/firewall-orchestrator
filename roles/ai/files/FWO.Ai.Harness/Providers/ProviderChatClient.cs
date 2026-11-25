using Microsoft.Extensions.AI;

namespace FWO.Ai.Harness.Providers;

/// <summary>
/// Chat client of a provider SDK that also owns the SDK client it was created from,
/// so that disposing the chat client frees the SDK client as well.
/// </summary>
internal sealed class ProviderChatClient : DelegatingChatClient
{
    private readonly IDisposable ownedClient;

    /// <summary>
    /// Wraps the chat client created from the given SDK client.
    /// </summary>
    /// <param name="innerClient">Chat client of the provider SDK</param>
    /// <param name="ownedClient">SDK client the chat client was created from, disposed with it</param>
    internal ProviderChatClient(IChatClient innerClient, IDisposable ownedClient) : base(innerClient)
    {
        this.ownedClient = ownedClient;
    }

    /// <summary>
    /// Disposes the inner chat client and the owned SDK client.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            ownedClient.Dispose();
        }
    }
}
