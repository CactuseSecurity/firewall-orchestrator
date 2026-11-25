using AGUI.Abstractions;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace FWO.Ai.Client.AgUi;

/// <summary>
/// Chat client of the FWO agent: the AG-UI chat client of the SDK on the <see cref="FwoAgUiTransport"/>, bounded by the
/// run timeout. Violations of the AG-UI event lifecycle, which the AG-UI chat client detects, are reported as
/// <see cref="AgentClientFailure.ProtocolError"/>, a run longer than the timeout as <see cref="AgentClientFailure.Timeout"/>.
/// </summary>
internal sealed class FwoAgentChatClient : DelegatingChatClient
{
    private readonly TimeSpan runTimeout;

    /// <summary>
    /// Wraps the AG-UI chat client.
    /// </summary>
    /// <param name="aguiChatClient">AG-UI chat client sending the runs</param>
    /// <param name="runTimeout">Longest time a run may take, until its last update has been read</param>
    internal FwoAgentChatClient(IChatClient aguiChatClient, TimeSpan runTimeout) : base(aguiChatClient)
    {
        this.runTimeout = runTimeout;
    }

    /// <summary>
    /// Runs and returns the whole response. The snapshot of the conversation the harness sent at the end of the run is
    /// kept as raw representation of the response, see <see cref="AgentConversation.TryGetSnapshot(ChatResponse, out List{ChatMessage}?)"/>.
    /// </summary>
    /// <exception cref="AgentClientException">The run could not be done, see <see cref="AgentClientFailure"/>.</exception>
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        List<ChatResponseUpdate> updates = [];
        await foreach (ChatResponseUpdate update in GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updates.Add(update);
        }
        ChatResponse response = updates.ToChatResponse();
        MessagesSnapshotEvent? snapshot = updates.Select(update => update.RawRepresentation).OfType<MessagesSnapshotEvent>().LastOrDefault();
        if (snapshot != null)
        {
            response.RawRepresentation = snapshot;
        }
        return response;
    }

    /// <inheritdoc/>
    /// <exception cref="AgentClientException">The run could not be done, see <see cref="AgentClientFailure"/>.</exception>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCancellation.CancelAfter(runTimeout);
        IAsyncEnumerable<ChatResponseUpdate> updates = base.GetStreamingResponseAsync(messages, options, runCancellation.Token);
        await using IAsyncEnumerator<ChatResponseUpdate> enumerator = updates.GetAsyncEnumerator(runCancellation.Token);
        while (await MoveNextAsync(enumerator, cancellationToken))
        {
            yield return enumerator.Current;
        }
    }

    /// <summary>
    /// Moves to the next update, mapping a timeout and a protocol violation to an <see cref="AgentClientException"/>.
    /// </summary>
    /// <param name="enumerator">Updates of the run</param>
    /// <param name="callerToken">Cancellation token of the caller: its cancellation is passed on as it is</param>
    /// <returns>Whether there is a next update</returns>
    /// <exception cref="AgentClientException">The run timed out or broke the AG-UI event lifecycle.</exception>
    private async Task<bool> MoveNextAsync(IAsyncEnumerator<ChatResponseUpdate> enumerator, CancellationToken callerToken)
    {
        try
        {
            return await enumerator.MoveNextAsync();
        }
        catch (OperationCanceledException exception) when (!callerToken.IsCancellationRequested)
        {
            throw new AgentClientException(AgentClientFailure.Timeout, $"The run took longer than the run timeout of {runTimeout}.",
                innerException: exception);
        }
        catch (InvalidOperationException exception) when (exception is not ObjectDisposedException)
        {
            throw new AgentClientException(AgentClientFailure.ProtocolError, "The AI harness broke the AG-UI event lifecycle.",
                innerException: exception);
        }
        catch (JsonException exception)
        {
            throw new AgentClientException(AgentClientFailure.ProtocolError, "The AI harness sent a tool call that could not be read.",
                innerException: exception);
        }
    }
}
