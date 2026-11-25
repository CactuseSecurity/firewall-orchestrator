using AGUI.Abstractions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// The conversation the stateless harness sends at the end of each run, as AG-UI MESSAGES_SNAPSHOT: the messages the
/// run sent to the model (the conversation of the client, with the messages of an MCP prompt and the summary of a
/// compacted history) followed by the response of the run. The client sends it with its next run. Left out are the
/// tool approval requests and answers, as an open request reaches the client as interrupt of the run and an answered
/// one as the tool call and result it caused, and the tool calls the agent loop left unanswered
/// (<see cref="UnansweredToolCallFilter"/>), as the providers reject a history holding them. A run that fails after it
/// ran a tool ends with the conversation up to the last tool result (see <see cref="CreateAfterFailure"/>), so that the
/// client keeps the tool calls that were made.
/// </summary>
internal static class ConversationSnapshot
{
    /// <summary>
    /// Serializer options of the snapshot: the AG-UI contracts first, then those of Microsoft.Extensions.AI, which also
    /// serialize the tool results of the MCP server.
    /// </summary>
    private static readonly JsonSerializerOptions kJsonOptions = CreateJsonOptions();

    /// <summary>
    /// Creates the update holding the snapshot of the conversation, which the AG-UI hosting sends as it is.
    /// </summary>
    /// <param name="runMessages">Messages the run sent to the model</param>
    /// <param name="responseUpdates">Updates of the response of the run</param>
    internal static AgentResponseUpdate Create(IEnumerable<ChatMessage> runMessages, IEnumerable<AgentResponseUpdate> responseUpdates)
    {
        ArgumentNullException.ThrowIfNull(runMessages);
        ArgumentNullException.ThrowIfNull(responseUpdates);
        IEnumerable<ChatMessage> responseMessages = UnansweredToolCallFilter.Apply(responseUpdates.ToAgentResponse().Messages);
        return CreateUpdate(runMessages.Concat(responseMessages));
    }

    /// <summary>
    /// Creates the update holding the snapshot of a run that failed after it ran a tool: the messages the run sent to the
    /// model followed by its response up to the last tool result. The tool calls ran, possibly changing data, so they
    /// belong to the conversation, also so that the model does not ask for an approved call again. Text the model wrote
    /// after the last tool result is left out, as its answer failed.
    /// </summary>
    /// <param name="runMessages">Messages the run sent to the model</param>
    /// <param name="responseUpdates">Updates of the response received before the run failed</param>
    /// <returns>The update; null if the response holds no tool result, as the client then keeps the conversation it sent.</returns>
    internal static AgentResponseUpdate? CreateAfterFailure(IEnumerable<ChatMessage> runMessages, IEnumerable<AgentResponseUpdate> responseUpdates)
    {
        ArgumentNullException.ThrowIfNull(runMessages);
        ArgumentNullException.ThrowIfNull(responseUpdates);
        List<ChatMessage> responseMessages = UnansweredToolCallFilter.Apply(responseUpdates.ToAgentResponse().Messages);
        int lastToolResultIndex = responseMessages.FindLastIndex(message => message.Contents.OfType<FunctionResultContent>().Any());
        if (lastToolResultIndex < 0)
        {
            return null;
        }
        return CreateUpdate(runMessages.Concat(responseMessages.Take(lastToolResultIndex + 1)));
    }

    /// <summary>
    /// Creates the update holding the given conversation without tool approval requests and answers.
    /// </summary>
    private static AgentResponseUpdate CreateUpdate(IEnumerable<ChatMessage> messages)
    {
        List<ChatMessage> conversation = WithoutApprovals(messages);
        MessagesSnapshotEvent snapshot = new() { Messages = conversation.AsAGUIMessages(kJsonOptions).ToList() };
        return new AgentResponseUpdate(new ChatResponseUpdate { RawRepresentation = snapshot });
    }

    /// <summary>
    /// Returns the messages without tool approval requests and answers; messages left without contents are dropped.
    /// </summary>
    internal static List<ChatMessage> WithoutApprovals(IEnumerable<ChatMessage> messages)
    {
        List<ChatMessage> keptMessages = [];
        foreach (ChatMessage message in messages)
        {
            List<AIContent> contents = message.Contents
                .Where(content => content is not (ToolApprovalRequestContent or ToolApprovalResponseContent))
                .ToList();
            if (contents.Count == message.Contents.Count)
            {
                keptMessages.Add(message);
            }
            else if (contents.Count > 0)
            {
                ChatMessage keptMessage = message.Clone();
                keptMessage.Contents = contents;
                keptMessages.Add(keptMessage);
            }
        }
        return keptMessages;
    }

    /// <summary>
    /// Creates the serializer options of the snapshot.
    /// </summary>
    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(AIJsonUtilities.DefaultOptions);
        options.TypeInfoResolverChain.Insert(0, AGUIJsonUtilities.DefaultTypeInfoResolver);
        options.MakeReadOnly();
        return options;
    }
}
