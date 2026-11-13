using FWO.Ai.Harness.Agent;
using FWO.Basics;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the compaction of the conversation of a run, directly on the reducer and through an agent. The token count is
/// estimated as UTF-8 bytes / 4 per message group, so every message of the tests is built from
/// <see cref="kMessageCharacters"/> ASCII characters, i.e. <see cref="kMessageTokens"/> tokens.
/// </summary>
[TestFixture]
internal class HarnessHistoryCompactionTest
{
    private const int kMessageCharacters = 400;
    private const int kMessageTokens = 100;
    private const int kContextWindowTokens = 1000;
    private const int kReserveTokens = 250;
    private const int kLargeReserveTokens = 500;
    private const char kFillCharacter = '.';
    private const string kSummaryPrefix = "[Summary]";
    private const string kSummary = "The user asked about firewall 10.1.1.1 port 443.";
    private const string kNextSummary = "Second summary of the conversation.";
    private const string kSummarizerFailure = "summarizer unavailable";
    private const string kIdentifierRule = "Keep names, UIDs, IP addresses, networks, ports and other identifiers when relevant, exactly as given.";
    private const string kCallId = "call-1";
    private const string kToolName = "test_tool";
    private const string kToolResult = "result";
    private const string kUserLabel = "u";
    private const string kAssistantLabel = "a";
    private const int kPreservedRecentGroups = 4;
    private const int kTurnsBelowThreshold = 3;
    private const int kTurnsAboveThreshold = 5;
    private const int kTurnsOfSecondCompaction = 3;
    private const int kAgentTurns = 5;
    private const int kTurnsBelowLargeReserve = 3;
    private const int kAtThresholdLastMessageCharacters = 200;
    private const int kAboveThresholdLastMessageCharacters = 204;
    private const int kTurnsAtThreshold = 4;
    private const int kMessagesBeforeLastAtThreshold = 7;

    /// <summary>
    /// Verifies the compaction constants: the 4 most recent groups are kept, and the summary keeps identifiers.
    /// </summary>
    [Test]
    public void Constants_PreserveRecentGroupsAndIdentifiers()
    {
        Assert.That(HistoryCompaction.kPreservedRecentGroups, Is.EqualTo(kPreservedRecentGroups));
        Assert.That(HistoryCompaction.kSummarizationPrompt, Does.Contain(kIdentifierRule));
    }

    /// <summary>
    /// Verifies that a missing summarizer is refused.
    /// </summary>
    [Test]
    public void CreateReducer_WithoutSummarizer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => HistoryCompaction.CreateReducer(null!, kContextWindowTokens, kReserveTokens));
    }

    /// <summary>
    /// Verifies that a history below the threshold (context window minus reserve) is returned as it is, without asking
    /// the summarizer.
    /// </summary>
    [Test]
    public async Task ReduceAsync_BelowThreshold_KeepsHistoryWithoutSummarizerCall()
    {
        ScriptedChatClient summarizer = new();
        IChatReducer reducer = HistoryCompaction.CreateReducer(summarizer, kContextWindowTokens, kReserveTokens);
        List<ChatMessage> history = CreateTurns(1, kTurnsBelowThreshold);

        List<ChatMessage> reduced = (await reducer.ReduceAsync(history, CancellationToken.None)).ToList();

        AssertSameMessages(reduced, history);
        Assert.That(summarizer.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that the history is compacted only once its tokens exceed the threshold, not when they reach it.
    /// </summary>
    [TestCase(kAtThresholdLastMessageCharacters, false)]
    [TestCase(kAboveThresholdLastMessageCharacters, true)]
    public async Task ReduceAsync_AtAndAboveThreshold_CompactsOnlyAbove(int lastMessageCharacters, bool expectCompaction)
    {
        ScriptedChatClient summarizer = new ScriptedChatClient().RespondWithText(kSummary);
        IChatReducer reducer = HistoryCompaction.CreateReducer(summarizer, kContextWindowTokens, kReserveTokens);
        List<ChatMessage> history = CreateTurns(1, kTurnsAtThreshold).Take(kMessagesBeforeLastAtThreshold).ToList();
        history.Add(new ChatMessage(ChatRole.Assistant, new string(kFillCharacter, lastMessageCharacters)));

        List<ChatMessage> reduced = (await reducer.ReduceAsync(history, CancellationToken.None)).ToList();

        Assert.That(summarizer.ReceivedMessages, Has.Count.EqualTo(expectCompaction ? 1 : 0));
        Assert.That(reduced, Has.Count.EqualTo(expectCompaction ? kPreservedRecentGroups + 1 : history.Count));
    }

    /// <summary>
    /// Verifies that a history above the threshold is compacted in one summarizer call without tools, with the
    /// summarization prompt as system message and all groups but the 4 most recent ones; the summary replaces them as a
    /// user message starting with [Summary], and the 4 most recent groups are kept as they are.
    /// </summary>
    [Test]
    public async Task ReduceAsync_AboveThreshold_SummarizesAllButMostRecentGroupsInOneCall()
    {
        ScriptedChatClient summarizer = new ScriptedChatClient { Strict = true }.RespondWithText(kSummary);
        IChatReducer reducer = HistoryCompaction.CreateReducer(summarizer, kContextWindowTokens, kReserveTokens);
        List<ChatMessage> history = CreateTurns(1, kTurnsAboveThreshold);
        int summarizedCount = history.Count - kPreservedRecentGroups;

        List<ChatMessage> reduced = (await reducer.ReduceAsync(history, CancellationToken.None)).ToList();

        List<ChatMessage> summarizationRequest = summarizer.ReceivedMessages.Single();
        Assert.That(summarizationRequest[0].Role, Is.EqualTo(ChatRole.System));
        Assert.That(summarizationRequest[0].Text, Is.EqualTo(HistoryCompaction.kSummarizationPrompt));
        AssertSameMessages(summarizationRequest.Skip(1).ToList(), history.Take(summarizedCount).ToList());
        Assert.That(HarnessTestSetup.OffersTools(summarizer.ReceivedOptions.Single()), Is.False);
        AssertSummary(reduced[0], kSummary);
        AssertSameMessages(reduced.Skip(1).ToList(), history.Skip(summarizedCount).ToList());
        Assert.That(summarizer.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that a tool call among the most recent groups is kept together with its result, and that the stored
    /// summary, being a user message, lets the preserved tool call follow it: a strict provider accepts the history,
    /// while it would reject a summary stored as assistant message right before the tool call.
    /// </summary>
    [Test]
    public async Task ReduceAsync_WithRecentToolCall_KeepsCallWithResultAfterUserSummary()
    {
        ScriptedChatClient summarizer = new ScriptedChatClient().RespondWithText(kSummary);
        IChatReducer reducer = HistoryCompaction.CreateReducer(summarizer, kContextWindowTokens, kReserveTokens);
        // groups: u1 a1 u2 a2 u3 (summarized), tool call with result, a3 u4 a4 (the 4 most recent)
        List<ChatMessage> history = CreateTurns(1, 2);
        history.Add(CreateMessage(ChatRole.User, kUserLabel + kTurnsBelowThreshold));
        history.AddRange(CreateToolCallGroup());
        history.Add(CreateMessage(ChatRole.Assistant, kAssistantLabel + kTurnsBelowThreshold));
        history.AddRange(CreateTurns(kTurnsBelowThreshold + 1, kTurnsBelowThreshold + 1));

        List<ChatMessage> reduced = (await reducer.ReduceAsync(history, CancellationToken.None)).ToList();

        List<ChatMessage> summarizationRequest = summarizer.ReceivedMessages.Single();
        Assert.That(HarnessTestSetup.GetCallIds(summarizationRequest), Is.Empty);
        AssertSummary(reduced[0], kSummary);
        Assert.That(HarnessTestSetup.GetCallIds(reduced.Take(2)), Has.Count.EqualTo(1));
        Assert.That(HarnessTestSetup.GetResultCallIds(reduced.Take(3)), Has.Count.EqualTo(1));
        Assert.That(ProviderHistoryRules.FindViolation(reduced), Is.Null);
        List<ChatMessage> withAssistantSummary = new(reduced);
        withAssistantSummary[0] = new ChatMessage(ChatRole.Assistant, reduced[0].Text);
        Assert.That(ProviderHistoryRules.FindViolation(withAssistantSummary), Is.Not.Null);
    }

    /// <summary>
    /// Verifies that a tool call outside the most recent groups is summarized together with its result.
    /// </summary>
    [Test]
    public async Task ReduceAsync_WithOlderToolCall_SummarizesCallWithResult()
    {
        ScriptedChatClient summarizer = new ScriptedChatClient().RespondWithText(kSummary);
        IChatReducer reducer = HistoryCompaction.CreateReducer(summarizer, kContextWindowTokens, kReserveTokens);
        // groups: u1, tool call with result, a1 u2 a2 u3 a3 (summarized), u4 a4 u5 a5 (the 4 most recent)
        List<ChatMessage> history = new() { CreateMessage(ChatRole.User, kUserLabel + 1) };
        history.AddRange(CreateToolCallGroup());
        history.Add(CreateMessage(ChatRole.Assistant, kAssistantLabel + 1));
        history.AddRange(CreateTurns(2, kTurnsAboveThreshold));

        List<ChatMessage> reduced = (await reducer.ReduceAsync(history, CancellationToken.None)).ToList();

        List<ChatMessage> summarizationRequest = summarizer.ReceivedMessages.Single();
        Assert.That(HarnessTestSetup.GetCallIds(summarizationRequest), Is.EqualTo(HarnessTestSetup.GetResultCallIds(summarizationRequest)));
        Assert.That(HarnessTestSetup.GetCallIds(summarizationRequest), Has.Count.EqualTo(1));
        Assert.That(HarnessTestSetup.GetCallIds(reduced), Is.Empty);
        Assert.That(HarnessTestSetup.GetResultCallIds(reduced), Is.Empty);
        Assert.That(reduced, Has.Count.EqualTo(kPreservedRecentGroups + 1));
    }

    /// <summary>
    /// Verifies that the next compaction summarizes the previous summary together with the older messages, so that
    /// only the new summary is kept.
    /// </summary>
    [Test]
    public async Task ReduceAsync_SecondCompaction_SummarizesPreviousSummary()
    {
        ScriptedChatClient summarizer = new ScriptedChatClient().RespondWithText(kSummary).RespondWithText(kNextSummary);
        IChatReducer reducer = HistoryCompaction.CreateReducer(summarizer, kContextWindowTokens, kReserveTokens);
        List<ChatMessage> history = (await reducer.ReduceAsync(CreateTurns(1, kTurnsAboveThreshold), CancellationToken.None)).ToList();
        ChatMessage firstSummary = history[0];
        history.AddRange(CreateTurns(kTurnsAboveThreshold + 1, kTurnsAboveThreshold + kTurnsOfSecondCompaction));

        List<ChatMessage> reduced = (await reducer.ReduceAsync(history, CancellationToken.None)).ToList();

        List<ChatMessage> secondRequest = summarizer.ReceivedMessages[1];
        Assert.That(secondRequest[1], Is.SameAs(firstSummary));
        AssertSummary(reduced[0], kNextSummary);
        Assert.That(reduced, Does.Not.Contain(firstSummary));
        Assert.That(HarnessTestSetup.GetTexts(reduced).Count(text => text.StartsWith(kSummaryPrefix)), Is.EqualTo(1));
        Assert.That(reduced, Has.Count.EqualTo(kPreservedRecentGroups + 1));
    }

    /// <summary>
    /// Verifies that a failing summarizer call leaves the history unchanged, without failing the compaction.
    /// </summary>
    [Test]
    public async Task ReduceAsync_WhenSummarizerFails_KeepsHistory()
    {
        ScriptedChatClient summarizer = new ScriptedChatClient()
            .Respond((_, _, _) => Task.FromException<ChatResponse>(new InvalidOperationException(kSummarizerFailure)));
        IChatReducer reducer = HistoryCompaction.CreateReducer(summarizer, kContextWindowTokens, kReserveTokens);
        List<ChatMessage> history = CreateTurns(1, kTurnsAboveThreshold);

        List<ChatMessage> reduced = (await reducer.ReduceAsync(history, CancellationToken.None)).ToList();

        Assert.That(summarizer.ReceivedMessages, Has.Count.EqualTo(1));
        AssertSameMessages(reduced, history);
    }

    /// <summary>
    /// Verifies that cancelling the summarizer call cancels the compaction.
    /// </summary>
    [Test]
    public async Task ReduceAsync_WhenCancelled_Throws()
    {
        BlockingAnswer blockingAnswer = new();
        ScriptedChatClient summarizer = new ScriptedChatClient().Respond(blockingAnswer.AnswerAsync);
        IChatReducer reducer = HistoryCompaction.CreateReducer(summarizer, kContextWindowTokens, kReserveTokens);
        using CancellationTokenSource cancellation = new();
        Task<IEnumerable<ChatMessage>> reduction = reducer.ReduceAsync(CreateTurns(1, kTurnsAboveThreshold), cancellation.Token);
        await blockingAnswer.Started;

        await cancellation.CancelAsync();

        Assert.CatchAsync<OperationCanceledException>(async () => await reduction);
    }

    /// <summary>
    /// Verifies through an agent that a conversation growing beyond the threshold is compacted before the next turn:
    /// the summarizer is the plain provider client (no tools, no instructions), the turn starts with the summary as user
    /// message followed by the 4 most recent messages and the new one, the conversation the turn returns is the
    /// compacted one, and a strict provider accepts all requests.
    /// </summary>
    [Test]
    public async Task RunAsync_WithLongHistory_CompactsBeforeNextTurn()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = CreateAnsweringClient();
        AgentSettings agentSettings = new() { ChatEnabled = true };
        using FwoAgentFactory factory = CreateFactory(app, chatClient, agentSettings);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        List<ChatMessage> conversation = await RunTurnsAsync(agent, kAgentTurns);

        List<int> summarizationIndexes = GetSummarizationRequestIndexes(chatClient);
        int summarizationIndex = summarizationIndexes.Single();
        Assert.That(chatClient.ReceivedOptions[summarizationIndex]?.Instructions, Is.Null);
        Assert.That(HarnessTestSetup.OffersTools(chatClient.ReceivedOptions[summarizationIndex]), Is.False);
        List<ChatMessage> lastTurnRequest = chatClient.ReceivedMessages[^1];
        AssertSummary(lastTurnRequest[0], kSummary);
        Assert.That(lastTurnRequest, Has.Count.EqualTo(kPreservedRecentGroups + 2));
        Assert.That(lastTurnRequest[^1].Text, Is.EqualTo(CreateText(kUserLabel + kAgentTurns)));
        AssertSummary(conversation[0], kSummary);
        Assert.That(conversation, Has.Count.EqualTo(kPreservedRecentGroups + 3));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies through an agent that the configured compaction reserve sets the threshold: with half of the context
    /// window reserved the history is compacted earlier than with the default quarter.
    /// </summary>
    [Test]
    public async Task RunAsync_WithLargerReserve_CompactsEarlier()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = CreateAnsweringClient();
        using FwoAgentFactory factory = CreateFactory(app, chatClient, new AgentSettings { CompactionReserveTokens = kLargeReserveTokens, ChatEnabled = true });
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        await RunTurnsAsync(agent, kTurnsBelowLargeReserve + 1);

        Assert.That(GetSummarizationRequestIndexes(chatClient), Has.Count.EqualTo(1));
        AssertSummary(chatClient.ReceivedMessages[^1][0], kSummary);
    }

    /// <summary>
    /// Verifies through an agent that a failing summarizer leaves the conversation uncompacted and the turn runs with
    /// the full conversation.
    /// </summary>
    [Test]
    public async Task RunAsync_WhenSummarizerFails_RunsTurnWithFullHistory()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = CreateAnsweringClient(summarizerFails: true);
        using FwoAgentFactory factory = CreateFactory(app, chatClient, new AgentSettings { ChatEnabled = true });
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        List<ChatMessage> conversation = await RunTurnsAsync(agent, kAgentTurns);

        Assert.That(GetSummarizationRequestIndexes(chatClient), Has.Count.EqualTo(1));
        Assert.That(conversation, Has.Count.EqualTo(kAgentTurns * 2));
        Assert.That(chatClient.ReceivedMessages[^1], Has.Count.EqualTo(kAgentTurns * 2 - 1));
    }

    /// <summary>
    /// Creates a factory with the context window of these tests, whose user agents call the MCP server of the test server.
    /// </summary>
    private static FwoAgentFactory CreateFactory(WebApplication app, ScriptedChatClient chatClient, AgentSettings agentSettings)
    {
        return new FwoAgentFactory(chatClient, kContextWindowTokens, agentSettings, mcpHandlerFactory: () => app.GetTestServer().CreateHandler());
    }

    /// <summary>
    /// Runs the given number of turns with user messages of the test size, each sending the conversation the turn
    /// before returned, and returns the conversation of the last turn.
    /// </summary>
    private static async Task<List<ChatMessage>> RunTurnsAsync(FwoUserAgent agent, int turnCount)
    {
        List<ChatMessage> conversation = new();
        for (int turn = 1; turn <= turnCount; turn++)
        {
            List<ChatMessage> messages = [.. conversation, new ChatMessage(ChatRole.User, CreateText(kUserLabel + turn))];
            List<AgentResponseUpdate> updates = await HarnessTestSetup.RunStreamingAsync(agent, messages);
            conversation = HarnessTestSetup.GetSnapshot(updates);
        }
        return conversation;
    }

    /// <summary>
    /// Creates a strict client answering summarization requests with the summary (or failing them) and other requests
    /// with numbered answers of the test size.
    /// </summary>
    private static ScriptedChatClient CreateAnsweringClient(bool summarizerFails = false)
    {
        int answerNumber = 0;
        ScriptedChatClient chatClient = new()
        {
            Strict = true,
            Fallback = (messages, _, _) =>
            {
                if (IsSummarizationRequest(messages))
                {
                    return summarizerFails
                        ? Task.FromException<ChatResponse>(new InvalidOperationException(kSummarizerFailure))
                        : Task.FromResult(ScriptedChatClient.CreateTextResponse(kSummary));
                }
                return Task.FromResult(ScriptedChatClient.CreateTextResponse(CreateText(kAssistantLabel + Interlocked.Increment(ref answerNumber))));
            }
        };
        return chatClient;
    }

    /// <summary>
    /// Returns the indexes of the summarization requests among the requests the client received.
    /// </summary>
    private static List<int> GetSummarizationRequestIndexes(ScriptedChatClient chatClient)
    {
        List<List<ChatMessage>> requests = chatClient.ReceivedMessages;
        return Enumerable.Range(0, requests.Count).Where(index => IsSummarizationRequest(requests[index])).ToList();
    }

    /// <summary>
    /// Tells whether a request is a summarization request, identified by its system message.
    /// </summary>
    private static bool IsSummarizationRequest(List<ChatMessage> messages)
    {
        return messages.Count > 0 && messages[0].Role == ChatRole.System && messages[0].Text == HistoryCompaction.kSummarizationPrompt;
    }

    /// <summary>
    /// Checks that a message is a stored summary: a user message starting with [Summary] and holding the summary text.
    /// </summary>
    private static void AssertSummary(ChatMessage message, string summary)
    {
        Assert.That(message.Role, Is.EqualTo(ChatRole.User));
        Assert.That(message.Text, Does.StartWith(kSummaryPrefix));
        Assert.That(message.Text, Does.Contain(summary));
    }

    /// <summary>
    /// Checks that two lists hold the same message instances in the same order.
    /// </summary>
    private static void AssertSameMessages(List<ChatMessage> actual, List<ChatMessage> expected)
    {
        Assert.That(actual, Has.Count.EqualTo(expected.Count));
        for (int index = 0; index < expected.Count; index++)
        {
            Assert.That(actual[index], Is.SameAs(expected[index]));
        }
    }

    /// <summary>
    /// Creates the user and assistant messages of the given turns, each of the test size.
    /// </summary>
    private static List<ChatMessage> CreateTurns(int firstTurn, int lastTurn)
    {
        List<ChatMessage> messages = new();
        for (int turn = firstTurn; turn <= lastTurn; turn++)
        {
            messages.Add(CreateMessage(ChatRole.User, kUserLabel + turn));
            messages.Add(CreateMessage(ChatRole.Assistant, kAssistantLabel + turn));
        }
        return messages;
    }

    /// <summary>
    /// Creates an assistant message calling a tool and the tool message with its result, a few tokens together.
    /// </summary>
    private static List<ChatMessage> CreateToolCallGroup()
    {
        ChatMessage call = new(ChatRole.Assistant, new List<AIContent> { new FunctionCallContent(kCallId, kToolName) });
        ChatMessage result = new(ChatRole.Tool, new List<AIContent> { new FunctionResultContent(kCallId, kToolResult) });
        return new List<ChatMessage> { call, result };
    }

    /// <summary>
    /// Creates a message of the test size with the given label.
    /// </summary>
    private static ChatMessage CreateMessage(ChatRole role, string label)
    {
        return new ChatMessage(role, CreateText(label));
    }

    /// <summary>
    /// Creates a text of the test size starting with the given label.
    /// </summary>
    private static string CreateText(string label)
    {
        return label.PadRight(kMessageCharacters, kFillCharacter);
    }
}
