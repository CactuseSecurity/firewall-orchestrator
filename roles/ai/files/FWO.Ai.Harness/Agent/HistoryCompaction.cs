using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// Compaction of the history of a run, the conversation the client sends before the new input. Once it exceeds the
/// context window minus the reserve, or holds more than <see cref="kMaxHistoryMessages"/> messages, all of it is
/// replaced by one summary written by the model, before the turn is sent with the new input, which is never summarized.
/// A history of a single message group (a user message, an answer, or a tool call with its results) is left as it is,
/// as the framework does not compact it. The harness returns the compacted conversation (see
/// <see cref="ConversationSnapshot"/>), so the client sends the summary with its next runs and it is written once.
/// Nothing else is compacted or dropped.
/// </summary>
internal static class HistoryCompaction
{
    /// <summary>
    /// Number of history messages above which the history is compacted whatever its tokens, so that a long conversation
    /// of short messages (e.g. many tool calls with small results) is compacted as well, also with a large context window.
    /// </summary>
    internal const int kMaxHistoryMessages = 10000;

    /// <summary>
    /// Instruction for writing the summary: the default of Microsoft Agent Framework
    /// (SummarizationCompactionStrategy.DefaultSummarizationPrompt), plus keeping identifiers exactly as given,
    /// as later turns refer to firewall objects by them.
    /// </summary>
    internal const string kSummarizationPrompt =
        """
        You are a conversation summarizer. Produce a concise summary of the conversation that preserves:

        - Key facts, decisions, and user preferences
        - Important context needed for future turns
        - Tool call outcomes and their significance

        Keep names, UIDs, IP addresses, networks, ports and other identifiers when relevant, exactly as given.

        Omit pleasantries and redundant exchanges. Be factual and brief.
        """;

    /// <summary>
    /// Creates the reducer applied to the history before a turn is sent. It compacts a history above the token threshold
    /// or above <see cref="kMaxHistoryMessages"/> messages, and summarizes the whole history in one model call: it keeps
    /// none of the most recent message groups (the framework keeps 8 by default), and it does not stop once the history
    /// would be just below the limit (the default target), so that the compacted history is far below the threshold,
    /// also if its last groups are large (e.g. tool results), and not summarized again in nearly every turn. The summary
    /// is kept as a user message, see <see cref="SummaryAsUserMessageReducer"/>.
    /// </summary>
    /// <param name="summarizerClient">Chat client writing the summary; the plain provider client, without tools and
    /// without agent loop</param>
    /// <param name="contextWindowTokens">Context window of the model in tokens</param>
    /// <param name="reserveTokens">Tokens kept free of the conversation, see <see cref="AgentSettings.CompactionReserveTokens"/></param>
    internal static IChatReducer CreateReducer(IChatClient summarizerClient, int contextWindowTokens, int reserveTokens)
    {
        ArgumentNullException.ThrowIfNull(summarizerClient);
        int compactionThresholdTokens = contextWindowTokens - reserveTokens;
        CompactionTrigger compactionTrigger = CompactionTriggers.Any(
            CompactionTriggers.TokensExceed(compactionThresholdTokens),
            CompactionTriggers.MessagesExceed(kMaxHistoryMessages));
        SummarizationCompactionStrategy summarization = new(
            summarizerClient,
            compactionTrigger,
            minimumPreservedGroups: 0,
            summarizationPrompt: kSummarizationPrompt,
            target: CompactionTriggers.Never);
        return new SummaryAsUserMessageReducer(summarization.AsChatReducer());
    }

    /// <summary>
    /// Keeps the summary as a user message instead of the assistant message Microsoft Agent Framework writes.
    /// The summary is the first message of the conversation, as the instructions are not part of it, and the new input
    /// after it starts with a tool call of the model in a turn answering tool approval requests. Providers expect a
    /// history to start with a user message, and Gemini rejects a tool call right after a model message. As a user
    /// message, the summary is summarized again with the later messages at the next compaction.
    /// </summary>
    private sealed class SummaryAsUserMessageReducer : IChatReducer
    {
        private readonly IChatReducer summarizingReducer;

        /// <summary>
        /// Wraps the reducer writing the summary.
        /// </summary>
        /// <param name="summarizingReducer">Reducer of the summarization strategy</param>
        internal SummaryAsUserMessageReducer(IChatReducer summarizingReducer)
        {
            this.summarizingReducer = summarizingReducer;
        }

        /// <summary>
        /// Compacts the history if needed and turns a new summary into a user message.
        /// </summary>
        /// <param name="messages">Conversation of the run</param>
        /// <param name="cancellationToken">Cancels the summary request</param>
        public async Task<IEnumerable<ChatMessage>> ReduceAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            IEnumerable<ChatMessage> reducedMessages = await summarizingReducer.ReduceAsync(messages, cancellationToken);
            return reducedMessages.Select(ToUserMessageIfSummary).ToList();
        }

        /// <summary>
        /// Returns a copy of a summary written by the framework as a user message, and any other message unchanged.
        /// </summary>
        private static ChatMessage ToUserMessageIfSummary(ChatMessage message)
        {
            bool isSummary = message.Role == ChatRole.Assistant
                && message.AdditionalProperties?.ContainsKey(CompactionMessageGroup.SummaryPropertyKey) == true;
            if (!isSummary)
            {
                return message;
            }
            ChatMessage userMessage = message.Clone();
            userMessage.Role = ChatRole.User;
            return userMessage;
        }
    }
}
