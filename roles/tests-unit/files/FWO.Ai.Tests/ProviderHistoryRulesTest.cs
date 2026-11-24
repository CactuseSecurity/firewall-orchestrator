using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the provider rules the strict mode of <see cref="ScriptedChatClient"/> applies, so that a harness test
/// passing a strict client cannot pass because the rules miss a broken history.
/// </summary>
[TestFixture]
internal class ProviderHistoryRulesTest
{
    private const string kQuestion = "question";
    private const string kAnswer = "answer";
    private const string kInstructions = "instructions";
    private const string kCallId = "call-1";
    private const string kOtherCallId = "call-2";
    private const string kToolName = "test_tool";
    private const string kResult = "result";
    private const string kRequestId = "request-1";

    private static readonly List<TestCaseData> kBrokenHistories =
    [
        new TestCaseData(new List<ChatMessage> { Assistant(kAnswer), User(kQuestion) }).SetName("StartsWithAssistant"),
        new TestCaseData(new List<ChatMessage> { User(kQuestion), Call(kCallId) }).SetName("CallWithoutResult"),
        new TestCaseData(new List<ChatMessage> { User(kQuestion), Call(kCallId), User(kQuestion), Result(kCallId) }).SetName("ResultNotDirectlyAfterCall"),
        new TestCaseData(new List<ChatMessage> { User(kQuestion), Result(kCallId) }).SetName("ResultWithoutCall"),
        new TestCaseData(new List<ChatMessage> { User(kQuestion), Call(kCallId), Result(kOtherCallId) }).SetName("ResultOfOtherCall"),
        new TestCaseData(new List<ChatMessage> { User(kQuestion), Assistant(kAnswer), Call(kCallId), Result(kCallId) }).SetName("CallAfterAssistant"),
        new TestCaseData(new List<ChatMessage> { User(kQuestion), ApprovalRequest() }).SetName("ApprovalContent")
    ];

    /// <summary>
    /// Verifies that a history meeting all rules passes: system message first, user question, tool call answered by
    /// its result, a second call after the result, and the answer.
    /// </summary>
    [Test]
    public void FindViolation_WithValidHistory_ReturnsNull()
    {
        List<ChatMessage> history =
        [
            new ChatMessage(ChatRole.System, kInstructions),
            User(kQuestion),
            Call(kCallId),
            Result(kCallId),
            Call(kOtherCallId),
            Result(kOtherCallId),
            Assistant(kAnswer),
            User(kQuestion)
        ];

        List<ChatMessage> emptyHistory = [];
        Assert.That(ProviderHistoryRules.FindViolation(history), Is.Null);
        Assert.That(ProviderHistoryRules.FindViolation(emptyHistory), Is.Null);
    }

    /// <summary>
    /// Verifies that each broken rule is found.
    /// </summary>
    [TestCaseSource(nameof(kBrokenHistories))]
    public void FindViolation_WithBrokenHistory_ReturnsViolation(List<ChatMessage> history)
    {
        Assert.That(ProviderHistoryRules.FindViolation(history), Is.Not.Null);
    }

    /// <summary>
    /// Verifies that the strict client rejects a broken request with an exception and records the broken rule.
    /// </summary>
    [Test]
    public void GetResponseAsync_StrictWithBrokenRequest_RejectsRequest()
    {
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer);
        List<ChatMessage> request = [User(kQuestion), Call(kCallId)];

        Assert.ThrowsAsync<InvalidOperationException>(async () => await chatClient.GetResponseAsync(request));

        Assert.That(chatClient.Rejections, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Creates a user message.
    /// </summary>
    private static ChatMessage User(string text)
    {
        return new ChatMessage(ChatRole.User, text);
    }

    /// <summary>
    /// Creates an assistant text message.
    /// </summary>
    private static ChatMessage Assistant(string text)
    {
        return new ChatMessage(ChatRole.Assistant, text);
    }

    /// <summary>
    /// Creates an assistant message calling the test tool.
    /// </summary>
    private static ChatMessage Call(string callId)
    {
        List<AIContent> contents = [new FunctionCallContent(callId, kToolName)];
        return new ChatMessage(ChatRole.Assistant, contents);
    }

    /// <summary>
    /// Creates a tool message with the result of a call.
    /// </summary>
    private static ChatMessage Result(string callId)
    {
        List<AIContent> contents = [new FunctionResultContent(callId, kResult)];
        return new ChatMessage(ChatRole.Tool, contents);
    }

    /// <summary>
    /// Creates an assistant message asking for the approval of a call.
    /// </summary>
    private static ChatMessage ApprovalRequest()
    {
        ToolApprovalRequestContent approvalRequest = new(kRequestId, new FunctionCallContent(kCallId, kToolName));
        List<AIContent> contents = [approvalRequest];
        return new ChatMessage(ChatRole.Assistant, contents);
    }
}
