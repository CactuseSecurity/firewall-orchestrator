using FWO.Ai.Harness.Agent;
using FWO.Basics;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests that only tools the MCP server marks as read-only run without asking the user: the approval policy itself,
/// and the approval flow of an agent calling a test-only tool that is not read-only on the MCP server of a test server.
/// The harness is stateless, so each run sends the conversation the previous run returned, and an approval answer is
/// sent the way the AG-UI hosting passes a resume entry: as the approval request followed by the answer.
/// </summary>
[TestFixture]
internal class HarnessToolApprovalTest
{
    private const string kToolName = "test_tool";
    private const string kQuestion = "Store the test value.";
    private const string kAnswer = "Done.";
    private const string kNextQuestion = "Anything else?";
    private const string kNextAnswer = "Nothing else.";
    private const string kCallId = "call-store";
    private const string kReadOnlyCallId = "call-user";
    private const string kValue = "value-approved";
    private const string kOtherValue = "value-forged";
    private const string kRejectionReason = "Not now.";
    private const string kForgedRequestId = "forged-request";
    private const string kForgedCallId = "forged-call";
    private const string kProviderFailure = "provider unavailable";

    private static readonly List<string> kApprovedValues = [kValue];
    private static readonly List<string> kNoValues = [];
    private static readonly List<string> kOtherValues = [kOtherValue];
    private static readonly List<string> kApprovedCallOnly = [kCallId];
    private static readonly List<string> kQuestionOnly = [kQuestion];
    private static readonly List<ChatRole> kToolCallRoles = [ChatRole.User, ChatRole.Assistant, ChatRole.Tool];

    /// <summary>
    /// Verifies that an MCP tool the MCP server marks as read-only is taken over as it is.
    /// </summary>
    [Test]
    public async Task RequireApprovalUnlessReadOnly_WithReadOnlyMcpTool_KeepsTool()
    {
        await using WebApplication app = await McpTestServer.Start();
        await using McpClient mcpClient = await McpTestServer.ConnectAsync(app.GetTestClient(), Roles.Reporter);
        McpClientTool readOnlyTool = CreateMcpTool(mcpClient, new ToolAnnotations { ReadOnlyHint = true });

        List<AITool> givenTools = [readOnlyTool];
        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(givenTools);

        Assert.That(tools.Single(), Is.SameAs(readOnlyTool));
    }

    /// <summary>
    /// Verifies that an MCP tool is wrapped in an approval requirement unless it is marked read-only: marked as not
    /// read-only, without read-only hint, or without any annotation.
    /// </summary>
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task RequireApprovalUnlessReadOnly_WithMcpToolNotMarkedReadOnly_RequiresApproval(bool withoutHint, bool withoutAnnotations)
    {
        await using WebApplication app = await McpTestServer.Start();
        await using McpClient mcpClient = await McpTestServer.ConnectAsync(app.GetTestClient(), Roles.Reporter);
        ToolAnnotations? annotations = withoutAnnotations ? null : new ToolAnnotations { ReadOnlyHint = withoutHint ? null : false, IdempotentHint = true };
        McpClientTool mcpTool = CreateMcpTool(mcpClient, annotations);

        List<AITool> givenTools = [mcpTool];
        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(givenTools);

        ApprovalRequiredAIFunction approvalFunction = (ApprovalRequiredAIFunction)tools.Single();
        Assert.That(GetWrappedFunction(approvalFunction, mcpTool), Is.SameAs(mcpTool));
        Assert.That(approvalFunction.Name, Is.EqualTo(kToolName));
    }

    /// <summary>
    /// Verifies that a function that is not an MCP tool requires approval, as nothing marks it read-only.
    /// </summary>
    [Test]
    public void RequireApprovalUnlessReadOnly_WithPlainFunction_RequiresApproval()
    {
        AIFunction plainFunction = AIFunctionFactory.Create(() => kValue, kToolName);

        List<AITool> givenTools = [plainFunction];
        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(givenTools);

        Assert.That(GetWrappedFunction((ApprovalRequiredAIFunction)tools.Single(), plainFunction), Is.SameAs(plainFunction));
    }

    /// <summary>
    /// Verifies that a function wrapping a read-only MCP tool requires approval, as the wrapper may do more than the tool.
    /// </summary>
    [Test]
    public async Task RequireApprovalUnlessReadOnly_WithFunctionWrappingReadOnlyMcpTool_RequiresApproval()
    {
        await using WebApplication app = await McpTestServer.Start();
        await using McpClient mcpClient = await McpTestServer.ConnectAsync(app.GetTestClient(), Roles.Reporter);
        WrappingFunction wrapper = new(CreateMcpTool(mcpClient, new ToolAnnotations { ReadOnlyHint = true }));

        List<AITool> givenTools = [wrapper];
        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(givenTools);

        Assert.That(GetWrappedFunction((ApprovalRequiredAIFunction)tools.Single(), wrapper), Is.SameAs(wrapper));
    }

    /// <summary>
    /// Verifies that a function already requiring approval, directly or below a wrapper, is not wrapped a second time.
    /// </summary>
    [Test]
    public void RequireApprovalUnlessReadOnly_WithFunctionRequiringApproval_KeepsFunction()
    {
        ApprovalRequiredAIFunction approvalFunction = new(AIFunctionFactory.Create(() => kValue, kToolName));
        WrappingFunction wrappedApprovalFunction = new(approvalFunction);

        List<AITool> givenTools = [approvalFunction, wrappedApprovalFunction];
        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(givenTools);

        Assert.That(tools[0], Is.SameAs(approvalFunction));
        Assert.That(tools[1], Is.SameAs(wrappedApprovalFunction));
    }

    /// <summary>
    /// Verifies that tools that are not invocable functions (a hosted tool, a function declaration) are taken over as they are.
    /// </summary>
    [Test]
    public void RequireApprovalUnlessReadOnly_WithToolThatIsNoFunction_KeepsTool()
    {
        HostedWebSearchTool hostedTool = new();
        AIFunctionDeclaration declaration = AIFunctionFactory.CreateDeclaration(kToolName, null, JsonDocument.Parse("{\"type\":\"object\"}").RootElement);

        List<AITool> givenTools = [hostedTool, declaration];
        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(givenTools);

        Assert.That(tools[0], Is.SameAs(hostedTool));
        Assert.That(tools[1], Is.SameAs(declaration));
    }

    /// <summary>
    /// Verifies that a new list is returned in the order of the given tools, leaving the given list unchanged.
    /// </summary>
    [Test]
    public void RequireApprovalUnlessReadOnly_ReturnsNewListInSameOrder()
    {
        AIFunction firstFunction = AIFunctionFactory.Create(() => kValue, kToolName);
        HostedWebSearchTool hostedTool = new();
        List<AITool> givenTools = [firstFunction, hostedTool];

        List<AITool> tools = ToolApproval.RequireApprovalUnlessReadOnly(givenTools);

        Assert.That(tools, Is.Not.SameAs(givenTools));
        Assert.That(tools, Has.Count.EqualTo(givenTools.Count));
        Assert.That(givenTools[0], Is.SameAs(firstFunction));
        Assert.That(tools[0], Is.InstanceOf<ApprovalRequiredAIFunction>());
        Assert.That(tools[1], Is.SameAs(hostedTool));
    }

    /// <summary>
    /// Verifies that a missing list, or a list containing null, is refused.
    /// </summary>
    [Test]
    public void RequireApprovalUnlessReadOnly_WithNull_Throws()
    {
        List<AITool> toolsWithNull = [AIFunctionFactory.Create(() => kValue, kToolName), null!];

        Assert.Throws<ArgumentNullException>(() => ToolApproval.RequireApprovalUnlessReadOnly(null!));
        Assert.Throws<ArgumentNullException>(() => ToolApproval.RequireApprovalUnlessReadOnly(toolsWithNull));
    }

    /// <summary>
    /// Verifies that a run offers the read-only production tool as it is and the test tool, which is not read-only,
    /// as a function requiring approval.
    /// </summary>
    [Test]
    public async Task RunAsync_WrapsOnlyToolsNotMarkedReadOnly()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        await agent.RunAsync(kQuestion);

        IList<AITool> offeredTools = chatClient.ReceivedOptions.Single()!.Tools!;
        Assert.That(offeredTools.Single(tool => tool.Name == HarnessTestSetup.kCurrentUserTool), Is.InstanceOf<McpClientTool>());
        Assert.That(offeredTools.Single(tool => tool.Name == McpTestWriteTools.kStoreValueTool), Is.InstanceOf<ApprovalRequiredAIFunction>());
    }

    /// <summary>
    /// Verifies that a call of a tool not marked read-only is returned to the host as an approval request naming the
    /// tool and its arguments, without running the tool and without asking the model again, and that the conversation
    /// the run returns holds neither the request nor the call.
    /// </summary>
    [Test]
    public async Task RunStreamingAsync_CallingToolNotReadOnly_ReturnsApprovalRequestWithoutRunningTool()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue));
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        List<AgentResponseUpdate> updates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);

        ToolApprovalRequestContent approvalRequest = HarnessTestSetup.GetContents<ToolApprovalRequestContent>(updates.ToAgentResponse().Messages).Single();
        FunctionCallContent requestedCall = (FunctionCallContent)approvalRequest.ToolCall;
        Assert.That(requestedCall.Name, Is.EqualTo(McpTestWriteTools.kStoreValueTool));
        Assert.That(requestedCall.Arguments![McpTestWriteTools.kValueArgument]?.ToString(), Is.EqualTo(kValue));
        Assert.That(toolCalls.StoredValues, Is.Empty);
        Assert.That(chatClient.ReceivedMessages, Has.Count.EqualTo(1));
        Assert.That(HarnessTestSetup.GetTexts(HarnessTestSetup.GetSnapshot(updates)), Is.EqualTo(kQuestionOnly));
    }

    /// <summary>
    /// Verifies that approving the request in the next run runs the tool and passes its result to the model, and that
    /// rejecting it passes the rejection and its reason to the model without running the tool. In both cases the
    /// conversation the run returns is accepted by a strict provider in the following turn.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task RunAsync_AnsweringApprovalRequest_RunsToolOnlyIfApproved(bool approved)
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue))
            .RespondWithText(kAnswer)
            .RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        List<AgentResponseUpdate> requestUpdates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);
        ToolApprovalRequestContent approvalRequest = HarnessTestSetup.GetContents<ToolApprovalRequestContent>(requestUpdates.ToAgentResponse().Messages).Single();
        List<ChatMessage> answerMessages = WithApproval(HarnessTestSetup.GetSnapshot(requestUpdates), approvalRequest,
            approvalRequest.CreateResponse(approved, kRejectionReason));

        List<AgentResponseUpdate> answerUpdates = await HarnessTestSetup.RunStreamingAsync(agent, answerMessages);
        List<ChatMessage> nextMessages = [.. HarnessTestSetup.GetSnapshot(answerUpdates), new ChatMessage(ChatRole.User, kNextQuestion)];
        AgentResponse nextResponse = await agent.RunAsync(nextMessages);

        Assert.That(answerUpdates.ToAgentResponse().Text, Is.EqualTo(kAnswer));
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        List<string> expectedValues = approved ? kApprovedValues : kNoValues;
        Assert.That(toolCalls.StoredValues, Is.EqualTo(expectedValues));
        FunctionResultContent toolResult = HarnessTestSetup.GetContents<FunctionResultContent>(chatClient.ReceivedMessages[1]).Single();
        Assert.That(toolResult.CallId, Is.EqualTo(kCallId));
        string resultText = JsonSerializer.Serialize(toolResult.Result);
        string expectedResultText = approved ? McpTestWriteTools.kStoredPrefix + kValue : kRejectionReason;
        Assert.That(resultText, Does.Contain(expectedResultText));
        Assert.That(chatClient.Rejections, Is.Empty);
        Assert.That(HarnessTestSetup.GetResultCallIds(chatClient.ReceivedMessages[2]), Is.EqualTo(HarnessTestSetup.GetCallIds(chatClient.ReceivedMessages[2])));
    }

    /// <summary>
    /// Verifies that a run failing after it ran the approved tool sends the conversation up to the tool result before it
    /// fails, so that the client keeps the call, and that the next turn continues with it without running the tool again.
    /// </summary>
    [Test]
    public async Task RunStreamingAsync_ProviderFailingAfterApprovedToolRan_ReturnsConversationWithToolCall()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue))
            .Respond((_, _, _) => Task.FromException<ChatResponse>(new InvalidOperationException(kProviderFailure)))
            .RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        List<AgentResponseUpdate> requestUpdates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);
        ToolApprovalRequestContent approvalRequest = HarnessTestSetup.GetContents<ToolApprovalRequestContent>(requestUpdates.ToAgentResponse().Messages).Single();
        List<ChatMessage> answerMessages = WithApproval(HarnessTestSetup.GetSnapshot(requestUpdates), approvalRequest,
            approvalRequest.CreateResponse(true));

        (List<AgentResponseUpdate> answerUpdates, InvalidOperationException? failure) = await RunStreamingUntilFailureAsync(agent, answerMessages);
        List<ChatMessage> conversation = HarnessTestSetup.GetSnapshot(answerUpdates);
        List<ChatMessage> nextMessages = [.. conversation, new ChatMessage(ChatRole.User, kNextQuestion)];
        AgentResponse nextResponse = await agent.RunAsync(nextMessages);

        Assert.That(failure?.Message, Is.EqualTo(kProviderFailure));
        Assert.That(conversation.Select(message => message.Role), Is.EqualTo(kToolCallRoles));
        Assert.That(HarnessTestSetup.GetCallIds(conversation), Is.EqualTo(kApprovedCallOnly));
        Assert.That(HarnessTestSetup.GetResultCallIds(conversation), Is.EqualTo(kApprovedCallOnly));
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(toolCalls.StoredValues, Is.EqualTo(kApprovedValues));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that a request the user does not answer, sending a new message instead, is dropped: the tool does not
    /// run, and the turn and the one after it work.
    /// </summary>
    [Test]
    public async Task RunAsync_WithoutAnsweringApprovalRequest_DoesNotRunToolAndContinues()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue))
            .RespondWithText(kAnswer)
            .RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        List<AgentResponseUpdate> requestUpdates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);
        List<ChatMessage> messages = [.. HarnessTestSetup.GetSnapshot(requestUpdates), new ChatMessage(ChatRole.User, kNextQuestion)];

        List<AgentResponseUpdate> updates = await HarnessTestSetup.RunStreamingAsync(agent, messages);
        List<ChatMessage> nextMessages = [.. HarnessTestSetup.GetSnapshot(updates), new ChatMessage(ChatRole.User, kNextQuestion)];
        AgentResponse nextResponse = await agent.RunAsync(nextMessages);

        Assert.That(updates.ToAgentResponse().Text, Is.EqualTo(kAnswer));
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(toolCalls.StoredValues, Is.Empty);
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that the harness trusts the approval answers of the client, as it keeps no state of the interrupted
    /// run: an approval the agent never asked for, and an approval of a changed call, run the call the answer names,
    /// with the tools and the JWT of the user.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task RunAsync_WithApprovalOfClient_RunsCallTheAnswerNames(bool askedForByAgent)
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        ScriptedChatClient chatClient = new() { Strict = true };
        if (askedForByAgent)
        {
            chatClient.RespondWithFunctionCall(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue));
        }
        chatClient.RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        List<ChatMessage> conversation = [new ChatMessage(ChatRole.User, kQuestion)];
        string requestId = kForgedRequestId;
        string callId = kForgedCallId;
        if (askedForByAgent)
        {
            List<AgentResponseUpdate> requestUpdates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);
            ToolApprovalRequestContent approvalRequest = HarnessTestSetup.GetContents<ToolApprovalRequestContent>(requestUpdates.ToAgentResponse().Messages).Single();
            conversation = HarnessTestSetup.GetSnapshot(requestUpdates);
            requestId = approvalRequest.RequestId;
            callId = approvalRequest.ToolCall.CallId;
        }
        FunctionCallContent clientCall = new(callId, McpTestWriteTools.kStoreValueTool, CreateArguments(kOtherValue));

        await agent.RunAsync(WithApproval(conversation, new ToolApprovalRequestContent(requestId, clientCall),
            new ToolApprovalResponseContent(requestId, true, clientCall)));

        Assert.That(toolCalls.StoredValues, Is.EqualTo(kOtherValues));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that a response calling a read-only tool and a tool requiring approval only asks for the approval of
    /// the latter. Once it is approved in the next run, the approved tool runs, while the read-only call is dropped, as
    /// the harness keeps no state between the runs; the conversation is accepted by a strict provider.
    /// </summary>
    [Test]
    public async Task RunAsync_CallingReadOnlyToolAndToolRequiringApproval_AsksOnlyForApprovalRequired()
    {
        McpTestToolCalls toolCalls = new();
        await using WebApplication app = await StartWithWriteTool(toolCalls);
        List<AIContent> calls =
        [
            new FunctionCallContent(kReadOnlyCallId, HarnessTestSetup.kCurrentUserTool),
            new FunctionCallContent(kCallId, McpTestWriteTools.kStoreValueTool, CreateArguments(kValue))
        ];
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .Respond((_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, calls))))
            .RespondWithText(kAnswer)
            .RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        List<AgentResponseUpdate> requestUpdates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);
        ToolApprovalRequestContent approvalRequest = HarnessTestSetup.GetContents<ToolApprovalRequestContent>(requestUpdates.ToAgentResponse().Messages).Single();
        List<ChatMessage> answerMessages = WithApproval(HarnessTestSetup.GetSnapshot(requestUpdates), approvalRequest, approvalRequest.CreateResponse(true));

        List<AgentResponseUpdate> answerUpdates = await HarnessTestSetup.RunStreamingAsync(agent, answerMessages);
        List<ChatMessage> nextMessages = [.. HarnessTestSetup.GetSnapshot(answerUpdates), new ChatMessage(ChatRole.User, kNextQuestion)];
        AgentResponse nextResponse = await agent.RunAsync(nextMessages);

        Assert.That(((FunctionCallContent)approvalRequest.ToolCall).Name, Is.EqualTo(McpTestWriteTools.kStoreValueTool));
        Assert.That(toolCalls.StoredValues, Is.EqualTo(kApprovedValues));
        Assert.That(HarnessTestSetup.GetResultCallIds(chatClient.ReceivedMessages[1]), Is.EqualTo(kApprovedCallOnly));
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that the shared agent does not bind approval answers to requests recorded in a session, as the
    /// stateless harness keeps no session between runs.
    /// </summary>
    [Test]
    public void CreateAgentOptions_DisablesApprovalResponseBinding()
    {
        ChatClientAgentOptions options = FwoAgentFactory.CreateAgentOptions();

        Assert.That(options.DisableApprovalResponseBinding, Is.True);
        Assert.That(options.DisableApprovalNotRequiredFunctionBypassing, Is.False);
    }

    /// <summary>
    /// Runs a streaming turn expected to fail and returns the updates sent before the failure, and the failure.
    /// </summary>
    private static async Task<(List<AgentResponseUpdate> Updates, InvalidOperationException? Failure)> RunStreamingUntilFailureAsync(AIAgent agent,
        List<ChatMessage> messages)
    {
        List<AgentResponseUpdate> updates = [];
        try
        {
            await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages))
            {
                updates.Add(update);
            }
        }
        catch (InvalidOperationException exception)
        {
            return (updates, exception);
        }
        return (updates, null);
    }

    /// <summary>
    /// Starts the MCP server pipeline on a test server with the test-only tool that is not read-only.
    /// </summary>
    private static Task<WebApplication> StartWithWriteTool(McpTestToolCalls toolCalls)
    {
        return McpTestServer.StartWith(mcpServer => McpTestWriteTools.Register(mcpServer, toolCalls));
    }

    /// <summary>
    /// Creates an MCP tool of the given client with the given annotations, as the MCP server would describe it.
    /// </summary>
    private static McpClientTool CreateMcpTool(McpClient mcpClient, ToolAnnotations? annotations)
    {
        return new McpClientTool(mcpClient, new Tool { Name = kToolName, Annotations = annotations });
    }

    /// <summary>
    /// Returns the function of the type of the expected one that an approval requirement wraps.
    /// </summary>
    private static object? GetWrappedFunction(ApprovalRequiredAIFunction approvalFunction, AIFunction expectedFunction)
    {
        return approvalFunction.GetService(expectedFunction.GetType());
    }

    /// <summary>
    /// Creates the arguments of a call of the test tool storing the given value.
    /// </summary>
    private static Dictionary<string, object?> CreateArguments(string value)
    {
        return new Dictionary<string, object?> { [McpTestWriteTools.kValueArgument] = value };
    }

    /// <summary>
    /// Returns the conversation followed by the approval request and its answer, the way the AG-UI hosting passes a
    /// resume entry to the agent.
    /// </summary>
    private static List<ChatMessage> WithApproval(List<ChatMessage> conversation, ToolApprovalRequestContent approvalRequest,
        ToolApprovalResponseContent approvalResponse)
    {
        List<AIContent> requestContents = [approvalRequest];
        List<AIContent> responseContents = [approvalResponse];
        List<ChatMessage> messages =
        [
            .. conversation,
            new ChatMessage(ChatRole.Assistant, requestContents),
            new ChatMessage(ChatRole.User, responseContents)
        ];
        return messages;
    }

    /// <summary>
    /// Function wrapping another function without changing it.
    /// </summary>
    private sealed class WrappingFunction(AIFunction innerFunction) : DelegatingAIFunction(innerFunction)
    {
    }
}
