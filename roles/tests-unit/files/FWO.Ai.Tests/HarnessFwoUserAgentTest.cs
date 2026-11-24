using FWO.Ai.Harness.Agent;
using FWO.Basics;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the turns of a user agent through the MCP server pipeline running on a test server: tool calls with the
/// user's JWT and roles, failed and cancelled turns, streaming, the MCP prompts, and the conversation a turn takes and
/// returns, as the harness keeps none. The AI provider is replaced by a <see cref="ScriptedChatClient"/>, strict where
/// the history matters.
/// </summary>
[TestFixture]
internal class HarnessFwoUserAgentTest
{
    private const string kQuestion = "Which roles do I have?";
    private const string kNextQuestion = "And now?";
    private const string kAnswer = "You are a reporter.";
    private const string kNextAnswer = "Still a reporter.";
    private const string kCallId = "call-1";
    private const string kOtherUserName = "other-user";
    private const string kProviderFailure = "provider unavailable";
    private const string kReasonWithMarkup = "Reads <b>customer data</b> for order checks.";
    private const string kEscapedReasonWithMarkup = "Reads &lt;b&gt;customer data&lt;/b&gt; for order checks.";
    private const string kUnknownPrompt = "unknown_prompt";
    private const string kFirstInstruction = "You support FWO users.";
    private const string kSecondInstruction = "Keep UIDs exactly as given.";

    private static readonly List<ChatRole> kToolTurnRoles = [ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant];
    private static readonly List<string> kSecondTurnRequest = [kQuestion, kAnswer, kNextQuestion];
    private static readonly List<string> kSecondTurnOnly = [kNextQuestion];
    private static readonly List<ChatMessage> kNoMessages = [];
    private static readonly List<ChatMessage> kInstructionMessages = [new(ChatRole.User, kFirstInstruction), new(ChatRole.User, kSecondInstruction)];
    private static readonly List<ChatMessage> kBlankInstructionMessages = [new(ChatRole.User, " "), new(ChatRole.User, "")];

    /// <summary>
    /// Verifies that a tool call of the model runs on the MCP server with the user's JWT, its result reaches the model,
    /// and the conversation the turn returns holds the whole exchange.
    /// </summary>
    [Test]
    public async Task RunStreamingAsync_WithToolCall_RunsToolWithUsersTokenAndReturnsExchange()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kCurrentUserTool).RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        List<AgentResponseUpdate> updates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);

        Assert.That(updates.ToAgentResponse().Text, Is.EqualTo(kAnswer));
        FunctionResultContent toolResult = HarnessTestSetup.GetContents<FunctionResultContent>(chatClient.ReceivedMessages[1]).Single();
        Assert.That(toolResult.CallId, Is.EqualTo(kCallId));
        string resultJson = JsonSerializer.Serialize(toolResult.Result);
        Assert.That(resultJson, Does.Contain(McpTestServer.kUserName));
        Assert.That(resultJson, Does.Contain(Roles.Reporter));
        List<ChatMessage> conversation = HarnessTestSetup.GetSnapshot(updates);
        Assert.That(conversation.Select(message => message.Role), Is.EqualTo(kToolTurnRoles));
        Assert.That(conversation[1].Contents.OfType<FunctionCallContent>().Single().Name, Is.EqualTo(HarnessTestSetup.kCurrentUserTool));
        Assert.That(conversation[2].Contents.OfType<FunctionResultContent>().Single().CallId, Is.EqualTo(kCallId));
        Assert.That(JsonSerializer.Serialize(conversation[2].Contents.OfType<FunctionResultContent>().Single().Result), Does.Contain(McpTestServer.kUserName));
        Assert.That(conversation[3].Text, Is.EqualTo(kAnswer));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that a run only offers the tools of the user's roles: the random number tool only to admins and auditors.
    /// </summary>
    [TestCase(Roles.Reporter, false)]
    [TestCase(Roles.Recertifier, false)]
    [TestCase(Roles.Admin, true)]
    [TestCase(Roles.Auditor, true)]
    public async Task RunAsync_OffersToolsOfUsersRoles(string role, bool expectRandomNumberTool)
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, role);

        await agent.RunAsync(kQuestion);

        List<string> toolNames = HarnessTestSetup.GetToolNames(chatClient.ReceivedOptions.Single());
        Assert.That(toolNames, Does.Contain(HarnessTestSetup.kCurrentUserTool));
        Assert.That(toolNames.Contains(HarnessTestSetup.kRandomNumberTool), Is.EqualTo(expectRandomNumberTool));
    }

    /// <summary>
    /// Verifies that a tool the user's roles do not offer is not run when the model calls it anyway; the model gets
    /// an error instead of a result.
    /// </summary>
    [Test]
    public async Task RunAsync_ModelCallingToolNotOffered_DoesNotRunIt()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kRandomNumberTool).RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        AgentResponse response = await agent.RunAsync(kQuestion);

        Assert.That(response.Text, Is.EqualTo(kAnswer));
        FunctionResultContent toolResult = HarnessTestSetup.GetContents<FunctionResultContent>(chatClient.ReceivedMessages[1]).Single();
        Assert.That(toolResult.Result, Is.InstanceOf<string>());
        Assert.That(int.TryParse((string)toolResult.Result!, out _), Is.False);
    }

    /// <summary>
    /// Verifies that the agents of different users created by one factory call the tools with their own user's JWT.
    /// </summary>
    [Test]
    public async Task RunAsync_AgentsOfDifferentUsers_CallToolsWithOwnToken()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kCurrentUserTool).RespondWithText(kAnswer)
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kCurrentUserTool).RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent defaultUserAgent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        string otherUserToken = McpTestServer.CreateUserToken(kOtherUserName, Roles.Admin);
        FwoUserAgent otherUserAgent = HarnessTestSetup.CreateUserAgent(factory, _ => ValueTask.FromResult(otherUserToken));

        await defaultUserAgent.RunAsync(kQuestion);
        await otherUserAgent.RunAsync(kQuestion);

        string defaultUserResult = GetToolResultJson(chatClient.ReceivedMessages[1]);
        string otherUserResult = GetToolResultJson(chatClient.ReceivedMessages[3]);
        Assert.That(defaultUserResult, Does.Contain(McpTestServer.kUserName));
        Assert.That(defaultUserResult, Does.Not.Contain(kOtherUserName));
        Assert.That(otherUserResult, Does.Contain(kOtherUserName));
        Assert.That(otherUserResult, Does.Contain(Roles.Admin));
    }

    /// <summary>
    /// Verifies that the user agent of the next request continues the conversation the previous turn returned, when it
    /// is sent with the next question: the model gets the whole conversation.
    /// </summary>
    [Test]
    public async Task RunAsync_WithConversationOfPreviousTurn_ContinuesConversation()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent firstRequestAgent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        List<AgentResponseUpdate> firstUpdates = await HarnessTestSetup.RunStreamingAsync(firstRequestAgent, kQuestion);
        FwoUserAgent nextRequestAgent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        List<ChatMessage> nextMessages = [.. HarnessTestSetup.GetSnapshot(firstUpdates), new ChatMessage(ChatRole.User, kNextQuestion)];

        AgentResponse response = await nextRequestAgent.RunAsync(nextMessages);

        Assert.That(response.Text, Is.EqualTo(kNextAnswer));
        Assert.That(nextRequestAgent.Name, Is.EqualTo(FwoAgentFactory.kAgentName));
        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kSecondTurnRequest));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that the agent keeps no history, also not in a session used for more than one turn: the next turn on
    /// the session only sends what it is given.
    /// </summary>
    [Test]
    public async Task RunAsync_OnSameSession_KeepsNoHistory()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        AgentSession session = await agent.CreateSessionAsync();

        await agent.RunAsync(kQuestion, session);
        await agent.RunAsync(kNextQuestion, session);

        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[1]), Is.EqualTo(kSecondTurnOnly));
    }

    /// <summary>
    /// Verifies that a failing request fails the turn with the error of the provider, and that the next turn with the
    /// same conversation works.
    /// </summary>
    [Test]
    public async Task RunAsync_WhenProviderFails_RunsNextTurnWithSameConversation()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithText(kAnswer)
            .Respond((_, _, _) => Task.FromException<ChatResponse>(new InvalidOperationException(kProviderFailure)))
            .RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        List<AgentResponseUpdate> firstUpdates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);
        List<ChatMessage> nextMessages = [.. HarnessTestSetup.GetSnapshot(firstUpdates), new ChatMessage(ChatRole.User, kNextQuestion)];

        InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await agent.RunAsync(nextMessages))!;

        Assert.That(exception.Message, Is.EqualTo(kProviderFailure));
        AgentResponse nextResponse = await agent.RunAsync(nextMessages);
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kSecondTurnRequest));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that a turn can be cancelled, here while a tool call is in progress, and that the next turn with the
    /// same conversation works.
    /// </summary>
    [Test]
    public async Task RunAsync_WhenCancelled_RunsNextTurnWithSameConversation()
    {
        await using WebApplication app = await McpTestServer.Start();
        BlockingAnswer blockingAnswer = new();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithText(kAnswer)
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kCurrentUserTool)
            .Respond(blockingAnswer.AnswerAsync)
            .RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        List<AgentResponseUpdate> firstUpdates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);
        List<ChatMessage> nextMessages = [.. HarnessTestSetup.GetSnapshot(firstUpdates), new ChatMessage(ChatRole.User, kNextQuestion)];
        using CancellationTokenSource cancellation = new();
        Task<AgentResponse> cancelledTurn = agent.RunAsync(nextMessages, cancellationToken: cancellation.Token);
        await blockingAnswer.Started;

        await cancellation.CancelAsync();

        Assert.CatchAsync<OperationCanceledException>(async () => await cancelledTurn);
        AgentResponse nextResponse = await agent.RunAsync(nextMessages);
        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(HarnessTestSetup.GetTexts(chatClient.ReceivedMessages[^1]), Is.EqualTo(kSecondTurnRequest));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that a blank user message is refused without asking the model.
    /// </summary>
    [TestCase("")]
    [TestCase(" ")]
    public async Task RunAsync_WithBlankMessage_ThrowsWithoutAskingModel(string userMessage)
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        Assert.CatchAsync<ArgumentException>(async () => await agent.RunAsync(userMessage));

        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that the conversation a turn with a tool call returns is accepted by the provider as history of the
    /// next turn, which sends the tool call and its result along.
    /// </summary>
    [Test]
    public async Task RunStreamingAsync_WithToolCall_ReturnsConversationNextTurnContinues()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kCurrentUserTool).RespondWithText(kAnswer).RespondWithText(kNextAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        List<AgentResponseUpdate> updates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);
        List<ChatMessage> nextMessages = [.. HarnessTestSetup.GetSnapshot(updates), new ChatMessage(ChatRole.User, kNextQuestion)];

        AgentResponse nextResponse = await agent.RunAsync(nextMessages);

        Assert.That(nextResponse.Text, Is.EqualTo(kNextAnswer));
        Assert.That(HarnessTestSetup.GetResultCallIds(chatClient.ReceivedMessages[^1]), Has.Count.EqualTo(1));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that the messages of a prompt the run names, with the arguments escaped by the MCP server, are sent
    /// as the user turn and returned as part of the conversation.
    /// </summary>
    [Test]
    public async Task RunStreamingAsync_WithPrompt_SendsAndReturnsPromptAsUserTurn()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }.RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Requester);
        Dictionary<string, string> arguments = new() { [HarnessTestSetup.kReasonArgument] = kReasonWithMarkup };

        List<AgentResponseUpdate> updates = await HarnessTestSetup.RunStreamingAsync(agent, kNoMessages,
            options: HarnessTestSetup.CreatePromptRunOptions(HarnessTestSetup.kAccessRequestPrompt, arguments));

        Assert.That(updates.ToAgentResponse().Text, Is.EqualTo(kAnswer));
        ChatMessage sentMessage = chatClient.ReceivedMessages.Single().Single();
        Assert.That(sentMessage.Role, Is.EqualTo(ChatRole.User));
        Assert.That(sentMessage.Text, Does.Contain(kEscapedReasonWithMarkup));
        Assert.That(sentMessage.Text, Does.Not.Contain(kReasonWithMarkup));
        Assert.That(HarnessTestSetup.GetSnapshot(updates)[0].Text, Is.EqualTo(sentMessage.Text));
    }

    /// <summary>
    /// Verifies that the AG-UI input of a run does not reach the AI provider: the options of the model request carry
    /// only the tools of the user, and nothing the client sent along.
    /// </summary>
    [Test]
    public async Task RunAsync_WithAguiInput_DoesNotPassClientOptionsToProvider()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        await agent.RunAsync(kQuestion, options: HarnessTestSetup.CreateAguiRunOptions(new AGUI.Abstractions.RunAgentInput()));

        Assert.That(chatClient.ReceivedOptions.Single()?.AdditionalProperties, Is.Null.Or.Empty);
    }

    /// <summary>
    /// Verifies that an unknown prompt is refused by the MCP server without asking the model.
    /// </summary>
    [Test]
    public async Task RunAsync_WithUnknownPrompt_ThrowsWithoutAskingModel()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Requester);

        Assert.CatchAsync<McpException>(async () => await agent.RunAsync(kNoMessages, options: HarnessTestSetup.CreatePromptRunOptions(kUnknownPrompt)));

        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that a prompt without messages is refused, as it cannot be sent as a turn.
    /// </summary>
    [Test]
    public async Task RunAsync_WithPromptWithoutMessages_ThrowsWithoutAskingModel()
    {
        await using WebApplication app = await McpTestServer.StartWith(mcpServer => mcpServer.WithPrompts<McpTestPrompts>());
        ScriptedChatClient chatClient = new();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Requester);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await agent.RunAsync(kNoMessages, options: HarnessTestSetup.CreatePromptRunOptions(McpTestPrompts.kEmptyPrompt)));

        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Returns the result of the single tool call in a request, as JSON.
    /// </summary>
    private static string GetToolResultJson(List<ChatMessage> request)
    {
        FunctionResultContent toolResult = HarnessTestSetup.GetContents<FunctionResultContent>(request).Single();
        return JsonSerializer.Serialize(toolResult.Result);
    }

    /// <summary>
    /// Verifies that the texts of the messages of the instructions prompt are joined line by line.
    /// </summary>
    [Test]
    public void JoinInstructions_WithSeveralMessages_JoinsTextsByLine()
    {
        string? instructions = FwoUserAgent.JoinInstructions(kInstructionMessages);

        Assert.That(instructions, Is.EqualTo(kFirstInstruction + Environment.NewLine + kSecondInstruction));
    }

    /// <summary>
    /// Verifies that an instructions prompt without text gives no instructions, so that no empty instructions are sent.
    /// </summary>
    [Test]
    public void JoinInstructions_WithoutText_ReturnsNull()
    {
        Assert.That(FwoUserAgent.JoinInstructions(kNoMessages), Is.Null);
        Assert.That(FwoUserAgent.JoinInstructions(kBlankInstructionMessages), Is.Null);
    }
}
