using AGUI.Abstractions;
using Microsoft.Extensions.AI;
using System.Diagnostics.CodeAnalysis;

namespace FWO.Ai.Client;

/// <summary>
/// Reads the conversation state from the updates of a run. The harness is stateless: a caller sends the whole
/// conversation with every run. At the end of each run the harness sends the conversation as it used it (an AG-UI
/// MESSAGES_SNAPSHOT): the messages sent, with the messages of an MCP prompt and the summary of a compacted history,
/// followed by the answer and the tool calls and results of the run. The caller sends that conversation with its next
/// run, followed by the new user message or the answers to the tool approval requests of the run.
/// </summary>
/// <remarks>
/// A tool that is not read-only runs only after the user approved it: the run then ends with a
/// <see cref="ToolApprovalRequestContent"/> per tool call, which is not part of the snapshot. The next run sends the
/// snapshot followed by one user message with the answers (<see cref="ToolApprovalRequestContent.CreateResponse"/>).
/// </remarks>
public static class AgentConversation
{
    /// <summary>
    /// Returns the conversation of a MESSAGES_SNAPSHOT update.
    /// </summary>
    /// <param name="update">Update of a run</param>
    /// <param name="messages">The conversation to send with the next run, if the update is a snapshot</param>
    /// <returns>Whether the update is a snapshot of the conversation</returns>
    public static bool TryGetSnapshot(ChatResponseUpdate update, [NotNullWhen(true)] out List<ChatMessage>? messages)
    {
        ArgumentNullException.ThrowIfNull(update);
        return TryReadSnapshot(update.RawRepresentation, out messages);
    }

    /// <summary>
    /// Returns the conversation the harness sent at the end of a run read with
    /// <see cref="IChatClient.GetResponseAsync"/> of an FWO agent chat client.
    /// </summary>
    /// <param name="response">Response of a run</param>
    /// <param name="messages">The conversation to send with the next run, if the harness sent one</param>
    /// <returns>Whether the harness sent the conversation</returns>
    public static bool TryGetSnapshot(ChatResponse response, [NotNullWhen(true)] out List<ChatMessage>? messages)
    {
        ArgumentNullException.ThrowIfNull(response);
        return TryReadSnapshot(response.RawRepresentation, out messages);
    }

    /// <summary>
    /// Returns the thread of the run named by its RUN_STARTED update, to send the next run of the conversation on
    /// (<see cref="ChatOptions.ConversationId"/>).
    /// </summary>
    /// <param name="update">Update of a run</param>
    /// <param name="threadId">Thread of the run, if the update starts it</param>
    /// <returns>Whether the update names the thread of the run</returns>
    public static bool TryGetThreadId(ChatResponseUpdate update, [NotNullWhen(true)] out string? threadId)
    {
        ArgumentNullException.ThrowIfNull(update);
        threadId = (update.RawRepresentation as RunStartedEvent)?.ThreadId;
        return !string.IsNullOrEmpty(threadId);
    }

    /// <summary>
    /// Returns the messages of a snapshot event.
    /// </summary>
    private static bool TryReadSnapshot(object? rawRepresentation, [NotNullWhen(true)] out List<ChatMessage>? messages)
    {
        if (rawRepresentation is not MessagesSnapshotEvent snapshot)
        {
            messages = null;
            return false;
        }
        IEnumerable<AGUIMessage> snapshotMessages = snapshot.Messages ?? [];
        messages = snapshotMessages.AsChatMessages().ToList();
        return true;
    }
}
