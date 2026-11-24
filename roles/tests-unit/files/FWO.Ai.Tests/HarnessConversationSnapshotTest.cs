using AGUI.Abstractions;
using FWO.Ai.Harness.Agent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the snapshot of the conversation the stateless harness sends at the end of each run: the messages of the run
/// and its response, without tool approvals and unanswered tool calls, as AG-UI MESSAGES_SNAPSHOT that the client
/// reads back.
/// </summary>
[TestFixture]
internal class HarnessConversationSnapshotTest
{
    private const string kQuestion = "Which roles do I have?";
    private const string kAnswer = "You are a reporter.";
    private const string kToolName = "get_current_user";
    private const string kCallId = "call-1";
    private const string kUnansweredCallId = "call-2";
    private const string kRequestId = "request-1";
    private const string kUserName = "mcp-user";
    private const string kRole = "reporter";
    private const string kNameProperty = "name";
    private const string kRolesProperty = "roles";

    private static readonly List<ChatRole> kToolTurnRoles = [ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant];
    private static readonly List<string> kCallIds = [kCallId];
    private static readonly List<string> kRoles = [kRole];

    /// <summary>
    /// Verifies that the snapshot holds the messages of the run followed by the response, without the approval request
    /// and answer the run took, and that it is an update without contents of its own.
    /// </summary>
    [Test]
    public void Create_WithApprovedToolCall_HoldsConversationWithoutApprovals()
    {
        ToolApprovalRequestContent approvalRequest = new(kRequestId, new FunctionCallContent(kCallId, kToolName));
        List<AIContent> requestContents = [approvalRequest];
        List<AIContent> responseContents = [approvalRequest.CreateResponse(true)];
        List<ChatMessage> runMessages =
        [
            new ChatMessage(ChatRole.User, kQuestion),
            new ChatMessage(ChatRole.Assistant, requestContents),
            new ChatMessage(ChatRole.User, responseContents)
        ];
        List<AgentResponseUpdate> responseUpdates =
        [
            CreateUpdate(ChatRole.Assistant, "message-1", new FunctionCallContent(kCallId, kToolName)),
            CreateUpdate(ChatRole.Tool, "message-2", new FunctionResultContent(kCallId, kRole)),
            CreateUpdate(ChatRole.Assistant, "message-3", new TextContent(kAnswer))
        ];

        AgentResponseUpdate update = ConversationSnapshot.Create(runMessages, responseUpdates);

        Assert.That(update.Contents, Is.Empty);
        List<ChatMessage> conversation = ReadSnapshot(update);
        Assert.That(conversation.Select(message => message.Role), Is.EqualTo(kToolTurnRoles));
        Assert.That(conversation[0].Text, Is.EqualTo(kQuestion));
        Assert.That(HarnessTestSetup.GetCallIds(conversation), Is.EqualTo(kCallIds));
        Assert.That(HarnessTestSetup.GetResultCallIds(conversation), Is.EqualTo(kCallIds));
        Assert.That(conversation[3].Text, Is.EqualTo(kAnswer));
        Assert.That(ProviderHistoryRules.FindViolation(conversation), Is.Null);
    }

    /// <summary>
    /// Verifies that a tool call the agent loop left unanswered is not part of the snapshot, while the text of its
    /// message is.
    /// </summary>
    [Test]
    public void Create_WithUnansweredToolCall_LeavesItOut()
    {
        List<ChatMessage> runMessages = [new ChatMessage(ChatRole.User, kQuestion)];
        List<AIContent> contents = [new TextContent(kAnswer), new FunctionCallContent(kUnansweredCallId, kToolName)];
        AgentResponseUpdate textWithCall = new(new ChatResponseUpdate(ChatRole.Assistant, contents) { MessageId = "message-1" });
        List<AgentResponseUpdate> responseUpdates = [textWithCall];

        List<ChatMessage> conversation = ReadSnapshot(ConversationSnapshot.Create(runMessages, responseUpdates));

        Assert.That(conversation, Has.Count.EqualTo(2));
        Assert.That(conversation[1].Text, Is.EqualTo(kAnswer));
        Assert.That(HarnessTestSetup.GetCallIds(conversation), Is.Empty);
    }

    /// <summary>
    /// Verifies that a structured tool result, as the MCP tools return it, is serialized into the snapshot as JSON.
    /// </summary>
    [Test]
    public void Create_WithStructuredToolResult_SerializesItAsJson()
    {
        Dictionary<string, object?> result = new() { [kNameProperty] = kUserName, [kRolesProperty] = kRoles };
        List<AIContent> callContents = [new FunctionCallContent(kCallId, kToolName)];
        List<AIContent> resultContents = [new FunctionResultContent(kCallId, result)];
        List<ChatMessage> runMessages =
        [
            new ChatMessage(ChatRole.User, kQuestion),
            new ChatMessage(ChatRole.Assistant, callContents),
            new ChatMessage(ChatRole.Tool, resultContents)
        ];
        List<AgentResponseUpdate> responseUpdates = [CreateUpdate(ChatRole.Assistant, "message-1", new TextContent(kAnswer))];

        List<ChatMessage> conversation = ReadSnapshot(ConversationSnapshot.Create(runMessages, responseUpdates));

        string resultText = HarnessTestSetup.GetContents<FunctionResultContent>(conversation).Single().Result?.ToString() ?? "";
        Assert.That(resultText, Does.Contain(kUserName));
        Assert.That(resultText, Does.Contain(kRole));
    }

    /// <summary>
    /// Verifies that the approvals are removed from the messages holding them, that the other contents of such a
    /// message are kept, and that the other messages are kept as they are.
    /// </summary>
    [Test]
    public void WithoutApprovals_KeepsOtherContentsAndMessages()
    {
        ToolApprovalRequestContent approvalRequest = new(kRequestId, new FunctionCallContent(kCallId, kToolName));
        ChatMessage question = new(ChatRole.User, kQuestion);
        List<AIContent> assistantContents = [new TextContent(kAnswer), approvalRequest];
        List<AIContent> responseContents = [approvalRequest.CreateResponse(false)];
        List<ChatMessage> messages =
        [
            question,
            new ChatMessage(ChatRole.Assistant, assistantContents),
            new ChatMessage(ChatRole.User, responseContents)
        ];

        List<ChatMessage> keptMessages = ConversationSnapshot.WithoutApprovals(messages);

        Assert.That(keptMessages, Has.Count.EqualTo(2));
        Assert.That(keptMessages[0], Is.SameAs(question));
        Assert.That(keptMessages[1].Contents.Single(), Is.InstanceOf<TextContent>());
        Assert.That(messages[1].Contents, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Verifies that missing messages or updates are refused.
    /// </summary>
    [Test]
    public void Create_WithoutMessagesOrUpdates_Throws()
    {
        List<ChatMessage> noMessages = [];
        List<AgentResponseUpdate> noUpdates = [];

        Assert.Throws<ArgumentNullException>(() => ConversationSnapshot.Create(null!, noUpdates));
        Assert.Throws<ArgumentNullException>(() => ConversationSnapshot.Create(noMessages, null!));
    }

    /// <summary>
    /// Creates an update of the response with one content.
    /// </summary>
    private static AgentResponseUpdate CreateUpdate(ChatRole role, string messageId, AIContent content)
    {
        List<AIContent> contents = [content];
        return new AgentResponseUpdate(new ChatResponseUpdate(role, contents) { MessageId = messageId });
    }

    /// <summary>
    /// Returns the conversation of the snapshot update, as the client reads it.
    /// </summary>
    private static List<ChatMessage> ReadSnapshot(AgentResponseUpdate update)
    {
        MessagesSnapshotEvent snapshot = (MessagesSnapshotEvent)update.AsChatResponseUpdate().RawRepresentation!;
        return snapshot.Messages.AsChatMessages().ToList();
    }
}
