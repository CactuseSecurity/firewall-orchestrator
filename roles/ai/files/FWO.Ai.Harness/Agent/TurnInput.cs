using AGUI.Abstractions;
using AGUI.Server;
using FWO.Ai.Harness.Mcp;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// Input of a turn of the stateless harness: the conversation the client sends with every run, ending with the new
/// turn. The harness keeps no history, so the client sends the conversation the harness returned at the end of the
/// previous run (see <see cref="ConversationSnapshot"/>) followed by the new turn. Of the messages of a run, the turn takes:
/// <list type="bullet">
/// <item>the conversation so far, as far as <see cref="ConversationFilter"/> keeps it,</item>
/// <item>the answers to tool approval requests (<see cref="ToolApprovalResponseContent"/>), which AG-UI sends as resume
/// entries: an approved tool call runs as the resume entry names it, with the user's JWT,</item>
/// <item>the new user input: the last message if it is a user message, or instead the messages of the MCP prompt the
/// run names (see <see cref="PromptRequest"/>), or instead the answers to the tool approval requests.</item>
/// </list>
/// A turn answering tool approval requests continues the turn the requests interrupted, so the conversation it sends
/// ends with the history of that turn, not with new input. The history (everything before the new input) and the new
/// input are kept apart, so that only the history is compacted. The AG-UI state and context are ignored.
/// </summary>
internal sealed class TurnInput
{
    private readonly List<ChatMessage> newInput;
    private readonly PromptRequest? prompt;

    /// <summary>
    /// Creates the input from its parts, see <see cref="Read"/>.
    /// </summary>
    private TurnInput(List<ChatMessage> history, List<ChatMessage> newInput, PromptRequest? prompt)
    {
        History = history;
        this.newInput = newInput;
        this.prompt = prompt;
    }

    /// <summary>
    /// The conversation before the new input of the turn, as far as <see cref="ConversationFilter"/> keeps it.
    /// </summary>
    internal IReadOnlyList<ChatMessage> History { get; }

    /// <summary>
    /// Name of the MCP prompt the run names as new input, null for a run sending a text or answering approval requests.
    /// </summary>
    internal string? PromptName => prompt?.Name;

    /// <summary>
    /// Whether the run names a prompt and sends no messages besides it (no conversation so far and no approval
    /// messages), so that the prompt starts a new conversation.
    /// </summary>
    internal bool IsPromptOnly => prompt != null && History.Count == 0 && newInput.Count == 0;

    /// <summary>
    /// Reads the input of a turn from the messages of a run and, for an AG-UI run, from the AG-UI input the hosting
    /// passes in the run options. The number of messages is not limited here: the request body limit of the harness
    /// bounds it, and a history of many messages is compacted (see <see cref="HistoryCompaction.kMaxHistoryMessages"/>).
    /// </summary>
    /// <param name="messages">Messages of the run: the conversation of the client</param>
    /// <param name="options">Run options; for an AG-UI run they carry the AG-UI input</param>
    /// <exception cref="ArgumentException">The run has no new input, more than one kind of new input, or a malformed
    /// prompt.</exception>
    /// <exception cref="NotSupportedException">The AG-UI input declares client tools.</exception>
    internal static TurnInput Read(IEnumerable<ChatMessage> messages, AgentRunOptions? options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        RunAgentInput? aguiInput = GetAguiInput(options);
        // client tools would also change how the AG-UI hosting reports the approval requests of the harness's tools
        if (aguiInput?.Tools is { Count: > 0 })
        {
            throw new NotSupportedException("The harness does not run client tools.");
        }
        List<ChatMessage> filteredMessages = ConversationFilter.Apply(messages);
        List<ChatMessage> history = filteredMessages.Where(message => !ConversationFilter.HoldsApprovalsOnly(message)).ToList();
        // the AG-UI hosting appends the approval requests and answers of the resume entries after the messages
        List<ChatMessage> approvals = filteredMessages.Where(ConversationFilter.HoldsApprovalsOnly).ToList();
        PromptRequest? prompt = PromptRequest.FromForwardedProperties(aguiInput?.ForwardedProperties);
        bool answersApprovals = approvals.Any(message => message.Contents.OfType<ToolApprovalResponseContent>().Any());
        ValidateNewInput(history, prompt, answersApprovals);
        if (answersApprovals || prompt != null)
        {
            return new TurnInput(history, approvals, prompt);
        }
        List<ChatMessage> newText = history.GetRange(history.Count - 1, 1);
        history.RemoveAt(history.Count - 1);
        return new TurnInput(history, newText, prompt);
    }

    /// <summary>
    /// Returns the new input of the turn: the new user message, the answers to the approval requests, or the messages
    /// of the prompt the run names.
    /// </summary>
    /// <param name="mcpConnection">Connection to the MCP server with the user's JWT, to get the prompt from</param>
    /// <param name="cancellationToken">Cancels getting the prompt</param>
    /// <exception cref="InvalidOperationException">The prompt has no messages.</exception>
    internal async Task<List<ChatMessage>> GetNewMessagesAsync(FwoMcpConnection mcpConnection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mcpConnection);
        List<ChatMessage> turnMessages = [.. newInput];
        if (prompt != null)
        {
            List<ChatMessage> promptMessages = await mcpConnection.GetPromptMessagesAsync(prompt.Name, prompt.Arguments, cancellationToken);
            if (promptMessages.Count == 0)
            {
                throw new InvalidOperationException("The prompt has no messages.");
            }
            turnMessages.AddRange(promptMessages);
        }
        return turnMessages;
    }

    /// <summary>
    /// Requires one kind of new input of the user: the answers to approval requests, a prompt, or a new text (the last
    /// message of the conversation, a user message). A turn answering approval requests takes no new text, as its
    /// conversation ends with the turn the requests interrupted. The new text has no length limit of its own, as it may
    /// hold large data such as the output of a tool; like the whole conversation, it is bounded by the request body
    /// limit of the harness (<see cref="Hosting.HarnessSettings.MaxRequestBodyBytes"/>).
    /// </summary>
    /// <exception cref="ArgumentException">The new input is missing or of more than one kind.</exception>
    private static void ValidateNewInput(List<ChatMessage> history, PromptRequest? prompt, bool answersApprovals)
    {
        if (answersApprovals)
        {
            if (prompt != null)
            {
                throw new ArgumentException("A turn either answers tool approval requests or names a prompt, not both.", nameof(prompt));
            }
            return;
        }
        bool sendsText = history.Count > 0 && history[^1].Role == ChatRole.User;
        if (prompt != null)
        {
            if (sendsText)
            {
                throw new ArgumentException("A turn either names a prompt or sends a text, not both.", nameof(prompt));
            }
            return;
        }
        if (!sendsText)
        {
            throw new ArgumentException("The turn contains no new user input.", nameof(history));
        }
    }

    /// <summary>
    /// Returns the AG-UI input the AG-UI hosting passes in the chat options of the run options, or null for other runs.
    /// </summary>
    private static RunAgentInput? GetAguiInput(AgentRunOptions? options)
    {
        ChatOptions? chatOptions = (options as ChatClientAgentRunOptions)?.ChatOptions;
        return chatOptions != null && chatOptions.TryGetRunAgentInput(out RunAgentInput? aguiInput) ? aguiInput : null;
    }
}
