using FWO.Ai.Harness.Agent;
using FWO.Basics;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the filter removing the tool calls the agent loop left unanswered from the messages stored in the history,
/// directly and through an agent reaching its maximum number of iterations.
/// </summary>
[TestFixture]
internal class HarnessUnansweredToolCallFilterTest
{
    private const string kQuestion = "Which roles do I have?";
    private const string kNextQuestion = "And now?";
    private const string kAnswer = "You are a reporter.";
    private const string kNextAnswer = "Still a reporter.";
    private const string kAnsweredCallId = "call-answered";
    private const string kUnansweredCallId = "call-unanswered";
    private const string kToolName = "test_tool";
    private const string kToolResult = "result";
    private const string kAuthorName = "fwo-agent";
    private const string kMessageId = "message-1";
    private const string kApprovalRequestId = "approval-1";
    private const int kMaxIterationsPerRequest = 2;
    private const int kSingleIteration = 1;
    private const string kCallIdPrefix = "call-";

    private static readonly List<string> kAnsweredCallOnly = [kAnsweredCallId];

    /// <summary>
    /// Verifies that messages whose calls are all answered are kept as they are, in a new list.
    /// </summary>
    [Test]
    public void Apply_WithAnsweredCalls_KeepsMessages()
    {
        List<ChatMessage> messages = new() { CreateCall(kAnsweredCallId), CreateResult(kAnsweredCallId), new ChatMessage(ChatRole.Assistant, kAnswer) };

        List<ChatMessage> filtered = UnansweredToolCallFilter.Apply(messages);

        Assert.That(filtered, Is.Not.SameAs(messages));
        Assert.That(filtered, Has.Count.EqualTo(messages.Count));
        for (int index = 0; index < messages.Count; index++)
        {
            Assert.That(filtered[index], Is.SameAs(messages[index]));
        }
        Assert.That(ProviderHistoryRules.FindViolation(PrependQuestion(filtered)), Is.Null);
    }

    /// <summary>
    /// Verifies that a message holding only an unanswered call is dropped, so that a strict provider accepts the
    /// messages, which it would reject with the call.
    /// </summary>
    [Test]
    public void Apply_WithMessageOfUnansweredCallOnly_DropsMessage()
    {
        ChatMessage answeredCall = CreateCall(kAnsweredCallId);
        ChatMessage result = CreateResult(kAnsweredCallId);
        List<ChatMessage> messages = new() { answeredCall, result, CreateCall(kUnansweredCallId) };

        List<ChatMessage> filtered = UnansweredToolCallFilter.Apply(messages);

        Assert.That(filtered, Has.Count.EqualTo(2));
        Assert.That(filtered[0], Is.SameAs(answeredCall));
        Assert.That(filtered[1], Is.SameAs(result));
        Assert.That(ProviderHistoryRules.FindViolation(PrependQuestion(messages)), Is.Not.Null);
        Assert.That(ProviderHistoryRules.FindViolation(PrependQuestion(filtered)), Is.Null);
    }

    /// <summary>
    /// Verifies that the text of a message also holding an unanswered call is kept in a copy without the call, keeping
    /// the other properties of the message and leaving the original message unchanged.
    /// </summary>
    [Test]
    public void Apply_WithTextAndUnansweredCall_KeepsTextWithoutCall()
    {
        ChatMessage textWithCall = new(ChatRole.Assistant, new List<AIContent> { new TextContent(kAnswer), new FunctionCallContent(kUnansweredCallId, kToolName) })
        {
            AuthorName = kAuthorName,
            MessageId = kMessageId
        };
        List<ChatMessage> messages = new() { textWithCall };

        List<ChatMessage> filtered = UnansweredToolCallFilter.Apply(messages);

        ChatMessage keptMessage = filtered.Single();
        Assert.That(keptMessage, Is.Not.SameAs(textWithCall));
        Assert.That(keptMessage.Contents.Single(), Is.InstanceOf<TextContent>());
        Assert.That(keptMessage.Text, Is.EqualTo(kAnswer));
        Assert.That(keptMessage.Role, Is.EqualTo(ChatRole.Assistant));
        Assert.That(keptMessage.AuthorName, Is.EqualTo(kAuthorName));
        Assert.That(keptMessage.MessageId, Is.EqualTo(kMessageId));
        Assert.That(textWithCall.Contents, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Verifies that of the calls in one message only the unanswered one is removed.
    /// </summary>
    [Test]
    public void Apply_WithAnsweredAndUnansweredCallInOneMessage_KeepsAnsweredCall()
    {
        ChatMessage calls = new(ChatRole.Assistant, new List<AIContent>
        {
            new FunctionCallContent(kAnsweredCallId, kToolName),
            new FunctionCallContent(kUnansweredCallId, kToolName)
        });
        List<ChatMessage> messages = new() { calls, CreateResult(kAnsweredCallId) };

        List<ChatMessage> filtered = UnansweredToolCallFilter.Apply(messages);

        Assert.That(HarnessTestSetup.GetCallIds(filtered), Is.EqualTo(kAnsweredCallOnly));
        Assert.That(filtered, Has.Count.EqualTo(2));
        Assert.That(ProviderHistoryRules.FindViolation(PrependQuestion(filtered)), Is.Null);
    }

    /// <summary>
    /// Verifies that tool approval requests and responses, which carry a call without result, are kept as they are,
    /// as is a result without call.
    /// </summary>
    [Test]
    public void Apply_WithApprovalContentsAndResultWithoutCall_KeepsThem()
    {
        ToolApprovalRequestContent approvalRequest = new(kApprovalRequestId, new FunctionCallContent(kUnansweredCallId, kToolName));
        ChatMessage requestMessage = new(ChatRole.Assistant, new List<AIContent> { approvalRequest });
        ChatMessage responseMessage = new(ChatRole.User, new List<AIContent> { approvalRequest.CreateResponse(true) });
        ChatMessage resultWithoutCall = CreateResult(kAnsweredCallId);
        List<ChatMessage> messages = new() { requestMessage, responseMessage, resultWithoutCall };

        List<ChatMessage> filtered = UnansweredToolCallFilter.Apply(messages);

        Assert.That(filtered, Has.Count.EqualTo(messages.Count));
        Assert.That(filtered[0], Is.SameAs(requestMessage));
        Assert.That(filtered[1], Is.SameAs(responseMessage));
        Assert.That(filtered[2], Is.SameAs(resultWithoutCall));
    }

    /// <summary>
    /// Verifies that a message that had no contents before is kept, and that no messages give no messages.
    /// </summary>
    [Test]
    public void Apply_WithMessageWithoutContents_KeepsMessage()
    {
        ChatMessage emptyMessage = new(ChatRole.Assistant, new List<AIContent>());

        List<ChatMessage> filtered = UnansweredToolCallFilter.Apply(new List<ChatMessage> { emptyMessage });

        Assert.That(filtered.Single(), Is.SameAs(emptyMessage));
        Assert.That(UnansweredToolCallFilter.Apply(new List<ChatMessage>()), Is.Empty);
    }

    /// <summary>
    /// Verifies that missing messages are refused.
    /// </summary>
    [Test]
    public void Apply_WithoutMessages_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => UnansweredToolCallFilter.Apply(null!));
    }

    /// <summary>
    /// Verifies the bound of the agent loop: with at most 2 iterations the model is asked 3 times, as each iteration
    /// is a round trip of tool calls, and the last request offers no tools, so the model has to answer. A call the model
    /// still makes there is streamed, but not part of the conversation the turn returns, so that a strict provider
    /// accepts the next turn.
    /// </summary>
    [Test]
    public async Task RunStreamingAsync_ToolCallAfterMaxIterations_IsStreamedButNotReturnedInConversation()
    {
        await using WebApplication app = await McpTestServer.Start();
        int callNumber = 0;
        ScriptedChatClient chatClient = new()
        {
            Strict = true,
            Fallback = (_, _, _) => Task.FromResult(ScriptedChatClient.CreateFunctionCallResponse($"{kCallIdPrefix}{++callNumber}", HarnessTestSetup.kCurrentUserTool))
        };
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient, new AgentSettings { MaxIterationsPerRequest = kMaxIterationsPerRequest, ChatEnabled = true });
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        List<AgentResponseUpdate> updates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);

        List<ChatOptions?> requestOptions = chatClient.ReceivedOptions;
        Assert.That(requestOptions, Has.Count.EqualTo(kMaxIterationsPerRequest + 1));
        Assert.That(requestOptions.Take(kMaxIterationsPerRequest).Select(HarnessTestSetup.OffersTools), Has.All.True);
        Assert.That(HarnessTestSetup.OffersTools(requestOptions[^1]), Is.False);
        string unansweredCallId = $"{kCallIdPrefix}{kMaxIterationsPerRequest + 1}";
        Assert.That(HarnessTestSetup.GetCallIds(updates.ToAgentResponse().Messages), Does.Contain(unansweredCallId));
        List<ChatMessage> conversation = HarnessTestSetup.GetSnapshot(updates);
        List<string> conversationCallIds = HarnessTestSetup.GetCallIds(conversation);
        Assert.That(conversationCallIds, Has.Count.EqualTo(kMaxIterationsPerRequest));
        Assert.That(conversationCallIds, Does.Not.Contain(unansweredCallId));
        Assert.That(conversationCallIds, Is.EquivalentTo(HarnessTestSetup.GetResultCallIds(conversation)));
        await AssertNextTurnAcceptedAsync(agent, conversation, chatClient);
    }

    /// <summary>
    /// Verifies that the text of a last answer that also holds an unanswered call is part of the conversation the turn
    /// returns without the call, while the streamed response is left unchanged.
    /// </summary>
    [Test]
    public async Task RunStreamingAsync_TextWithToolCallAfterMaxIterations_ReturnsTextWithoutCall()
    {
        await using WebApplication app = await McpTestServer.Start();
        ChatMessage textWithCall = new(ChatRole.Assistant, new List<AIContent>
        {
            new TextContent(kAnswer),
            new FunctionCallContent(kUnansweredCallId, HarnessTestSetup.kCurrentUserTool)
        });
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kAnsweredCallId, HarnessTestSetup.kCurrentUserTool)
            .Respond((_, _, _) => Task.FromResult(new ChatResponse(textWithCall)));
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient, new AgentSettings { MaxIterationsPerRequest = kSingleIteration, ChatEnabled = true });
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        List<AgentResponseUpdate> updates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);

        Assert.That(HarnessTestSetup.GetCallIds(updates.ToAgentResponse().Messages), Does.Contain(kUnansweredCallId));
        List<ChatMessage> conversation = HarnessTestSetup.GetSnapshot(updates);
        Assert.That(conversation[^1].Text, Is.EqualTo(kAnswer));
        Assert.That(conversation[^1].Contents.OfType<FunctionCallContent>(), Is.Empty);
        Assert.That(HarnessTestSetup.GetCallIds(conversation), Is.EqualTo(kAnsweredCallOnly));
        await AssertNextTurnAcceptedAsync(agent, conversation, chatClient);
    }

    /// <summary>
    /// Runs one more turn with the given conversation and checks that the strict provider accepted all requests.
    /// </summary>
    private static async Task AssertNextTurnAcceptedAsync(FwoUserAgent agent, List<ChatMessage> conversation, ScriptedChatClient chatClient)
    {
        chatClient.Fallback = null;
        chatClient.RespondWithText(kNextAnswer);
        List<ChatMessage> nextMessages = [.. conversation, new ChatMessage(ChatRole.User, kNextQuestion)];
        AgentResponse nextResponse = await agent.RunAsync(nextMessages);
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Returns the messages after a user question, as they would be sent to a provider.
    /// </summary>
    private static List<ChatMessage> PrependQuestion(List<ChatMessage> messages)
    {
        List<ChatMessage> request = new() { new ChatMessage(ChatRole.User, kQuestion) };
        request.AddRange(messages);
        return request;
    }

    /// <summary>
    /// Creates an assistant message calling the test tool.
    /// </summary>
    private static ChatMessage CreateCall(string callId)
    {
        return new ChatMessage(ChatRole.Assistant, new List<AIContent> { new FunctionCallContent(callId, kToolName) });
    }

    /// <summary>
    /// Creates a tool message with the result of a call.
    /// </summary>
    private static ChatMessage CreateResult(string callId)
    {
        return new ChatMessage(ChatRole.Tool, new List<AIContent> { new FunctionResultContent(callId, kToolResult) });
    }
}
