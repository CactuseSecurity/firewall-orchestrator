using Microsoft.Extensions.AI;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// Filter of the conversation a client sends with a run. The harness is stateless, so it takes the conversation of the
/// client as the history of the run, but only what a conversation of the harness itself holds:
/// <list type="bullet">
/// <item>user messages with texts and answers to tool approval requests,</item>
/// <item>assistant messages with texts, tool calls and tool approval requests,</item>
/// <item>tool messages with tool results.</item>
/// </list>
/// System and developer messages, messages of other roles and any other content (e.g. files, reasoning, metadata) are
/// dropped, so that the instructions of the agent only come from the MCP server. A tool call without a later result, and
/// a result without an earlier call, are dropped as well, as the providers reject such a history. The kept contents
/// are copied, so that nothing but their values is taken over from the client. A user can thus only mislead the
/// agent of their own conversation, whose tools still run with the user's JWT.
/// </summary>
internal static class ConversationFilter
{
    /// <summary>
    /// Returns the conversation the harness takes from the messages of a client.
    /// </summary>
    /// <param name="messages">Messages of the run</param>
    /// <returns>New messages with the kept contents; messages left without contents are dropped</returns>
    internal static List<ChatMessage> Apply(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        List<ChatMessage> takenMessages = messages.Select(TakeMessage).OfType<ChatMessage>().ToList();
        return DropUnmatchedToolCalls(takenMessages);
    }

    /// <summary>
    /// Tells whether a message holds nothing but tool approval requests and answers, as the AG-UI hosting adds them for
    /// the resume entries of a run.
    /// </summary>
    internal static bool HoldsApprovalsOnly(ChatMessage message)
    {
        return message.Contents.Count > 0
            && message.Contents.All(content => content is ToolApprovalRequestContent or ToolApprovalResponseContent);
    }

    /// <summary>
    /// Returns a copy of the message with the contents its role may hold, or null if none is left.
    /// </summary>
    private static ChatMessage? TakeMessage(ChatMessage message)
    {
        List<AIContent> contents = message.Contents.Select(content => TakeContent(message.Role, content)).OfType<AIContent>().ToList();
        return contents.Count > 0 ? new ChatMessage(message.Role, contents) : null;
    }

    /// <summary>
    /// Returns a copy of a content a message of the role may hold, or null if it is dropped.
    /// </summary>
    private static AIContent? TakeContent(ChatRole role, AIContent content)
    {
        if (content is TextContent text && (role == ChatRole.User || role == ChatRole.Assistant))
        {
            return string.IsNullOrWhiteSpace(text.Text) ? null : new TextContent(text.Text);
        }
        if (role == ChatRole.User)
        {
            return content as ToolApprovalResponseContent;
        }
        if (role == ChatRole.Assistant)
        {
            return content switch
            {
                FunctionCallContent call => new FunctionCallContent(call.CallId, call.Name, call.Arguments),
                ToolApprovalRequestContent approvalRequest => approvalRequest,
                _ => null
            };
        }
        return role == ChatRole.Tool && content is FunctionResultContent result ? new FunctionResultContent(result.CallId, result.Result) : null;
    }

    /// <summary>
    /// Drops the tool calls without a later result and the results without an earlier call; of several calls with the
    /// same id, and of several results of a call, the first is kept.
    /// </summary>
    private static List<ChatMessage> DropUnmatchedToolCalls(List<ChatMessage> messages)
    {
        HashSet<string> calledIds = new(StringComparer.Ordinal);
        HashSet<string> answeredIds = new(StringComparer.Ordinal);
        HashSet<AIContent> keptResults = new(ReferenceEqualityComparer.Instance);
        foreach (AIContent content in messages.SelectMany(message => message.Contents))
        {
            if (content is FunctionCallContent call)
            {
                calledIds.Add(call.CallId);
            }
            else if (content is FunctionResultContent result && calledIds.Contains(result.CallId) && answeredIds.Add(result.CallId))
            {
                keptResults.Add(result);
            }
        }
        HashSet<string> keptCallIds = new(StringComparer.Ordinal);
        List<ChatMessage> matchedMessages = [];
        foreach (ChatMessage message in messages)
        {
            List<AIContent> contents = message.Contents
                .Where(content => IsMatched(content, answeredIds, keptCallIds, keptResults))
                .ToList();
            if (contents.Count > 0)
            {
                matchedMessages.Add(new ChatMessage(message.Role, contents));
            }
        }
        return matchedMessages;
    }

    /// <summary>
    /// Tells whether a content is kept: a tool call that is answered and not kept yet, a result that answers a call, or
    /// any other content.
    /// </summary>
    private static bool IsMatched(AIContent content, HashSet<string> answeredIds, HashSet<string> keptCallIds, HashSet<AIContent> keptResults)
    {
        return content switch
        {
            FunctionCallContent call => answeredIds.Contains(call.CallId) && keptCallIds.Add(call.CallId),
            FunctionResultContent result => keptResults.Contains(result),
            _ => true
        };
    }
}
