using Microsoft.Extensions.AI;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// Filter of the response messages the harness returns in the conversation of a run, see <see cref="ConversationSnapshot"/>.
/// </summary>
internal static class UnansweredToolCallFilter
{
    /// <summary>
    /// Returns the response messages without the tool calls the agent loop left unanswered: after the maximum number
    /// of iterations the model is asked once more without tools, and a call it still makes there gets no result.
    /// The providers reject a history holding a tool call without result, so returning it would break every later turn.
    /// Only function calls without a function result in the same response are removed, and a message left without
    /// contents is dropped; tool approval requests and responses are kept as they are.
    /// </summary>
    /// <param name="responseMessages">Response messages of a turn</param>
    internal static List<ChatMessage> Apply(IEnumerable<ChatMessage> responseMessages)
    {
        ArgumentNullException.ThrowIfNull(responseMessages);
        List<ChatMessage> messages = responseMessages.ToList();
        HashSet<string> answeredCallIds = messages
            .SelectMany(message => message.Contents.OfType<FunctionResultContent>())
            .Select(result => result.CallId)
            .ToHashSet();
        List<ChatMessage> answeredMessages = [];
        foreach (ChatMessage message in messages)
        {
            List<AIContent> answeredContents = message.Contents
                .Where(content => content is not FunctionCallContent call || answeredCallIds.Contains(call.CallId))
                .ToList();
            if (answeredContents.Count == message.Contents.Count)
            {
                answeredMessages.Add(message);
            }
            else if (answeredContents.Count > 0)
            {
                ChatMessage answeredMessage = message.Clone();
                answeredMessage.Contents = answeredContents;
                answeredMessages.Add(answeredMessage);
            }
        }
        return answeredMessages;
    }
}
