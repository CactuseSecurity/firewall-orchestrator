using Microsoft.Extensions.AI;

namespace FWO.Ai.Tests;

/// <summary>
/// Rules the AI providers apply to the messages of a request, checked by the strict mode of
/// <see cref="ScriptedChatClient"/> and directly by tests of a stored history:
/// <list type="bullet">
/// <item>the conversation (all but the system messages) starts with a user message (Anthropic, Gemini),</item>
/// <item>a tool call directly follows a user or tool message, never another model message (Gemini),</item>
/// <item>every tool call is answered by its result in the message directly following it, and every result
/// answers a call made before (OpenAI, Anthropic),</item>
/// <item>no tool approval content reaches the provider, it is a concept of the agent only.</item>
/// </list>
/// </summary>
internal static class ProviderHistoryRules
{
    /// <summary>
    /// Returns the first rule the messages break, or null if they meet all rules.
    /// </summary>
    /// <param name="messages">Messages of a request, in the order they are sent</param>
    internal static string? FindViolation(IReadOnlyList<ChatMessage> messages)
    {
        List<ChatMessage> conversation = messages.Where(message => message.Role != ChatRole.System).ToList();
        if (conversation.Count > 0 && conversation[0].Role != ChatRole.User)
        {
            return $"The conversation starts with a {conversation[0].Role} message instead of a user message.";
        }
        HashSet<string> openCallIds = [];
        ChatRole? previousRole = null;
        foreach (ChatMessage message in conversation)
        {
            string? violation = FindViolationOfMessage(message, previousRole, openCallIds);
            if (violation != null)
            {
                return violation;
            }
            previousRole = message.Role;
        }
        return openCallIds.Count > 0 ? $"The tool call {openCallIds.First()} has no result." : null;
    }

    /// <summary>
    /// Checks one message against the calls left open by the messages before it, and updates the open calls.
    /// </summary>
    private static string? FindViolationOfMessage(ChatMessage message, ChatRole? previousRole, HashSet<string> openCallIds)
    {
        if (message.Contents.Any(content => content is ToolApprovalRequestContent or ToolApprovalResponseContent))
        {
            return "A tool approval content is sent to the provider.";
        }
        List<FunctionResultContent> results = message.Contents.OfType<FunctionResultContent>().ToList();
        if (openCallIds.Count > 0 && results.Count == 0)
        {
            return $"The tool call {openCallIds.First()} is not directly followed by its result.";
        }
        string? resultViolation = AnswerOpenCalls(results, openCallIds);
        if (resultViolation != null)
        {
            return resultViolation;
        }
        List<FunctionCallContent> calls = message.Contents.OfType<FunctionCallContent>().ToList();
        if (calls.Count == 0)
        {
            return null;
        }
        if (previousRole == ChatRole.Assistant)
        {
            return $"The tool call {calls[0].CallId} directly follows an assistant message.";
        }
        openCallIds.UnionWith(calls.Select(call => call.CallId));
        return null;
    }

    /// <summary>
    /// Removes the answered calls from the open calls; a result answering no open call breaks a rule.
    /// </summary>
    private static string? AnswerOpenCalls(List<FunctionResultContent> results, HashSet<string> openCallIds)
    {
        foreach (FunctionResultContent result in results)
        {
            if (!openCallIds.Remove(result.CallId))
            {
                return $"The tool result {result.CallId} answers no preceding tool call.";
            }
        }
        return null;
    }
}
