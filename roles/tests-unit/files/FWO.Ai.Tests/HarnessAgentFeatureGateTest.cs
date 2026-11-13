using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Hosting;
using FWO.Basics;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using NUnit.Framework;
using System.Net;
using System.Text.Json;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the switches of the AI functions in the harness: which function a turn belongs to (the reason check prompt
/// to the reason check, everything else to the chat), and that a turn of a function switched off is refused before it
/// reaches the MCP server or the AI provider, by the user agent and through the AG-UI endpoint.
/// </summary>
[TestFixture]
internal class HarnessAgentFeatureGateTest
{
    private const string kQuestion = "Which roles do I have?";
    private const string kAnswer = "Verdict: GENUINE";
    private const string kReason = "Nightly backup of the order database to the backup site.";
    private const string kThreadId = "thread-1";
    private const string kRequestId = "request-1";
    private const string kToolName = "store_value";
    private const AgentFeature kUnknownFeature = (AgentFeature)99;

    private static readonly List<ChatMessage> kNoMessages = [];
    private static readonly List<ChatMessage> kQuestionMessages = [new(ChatRole.User, kQuestion)];
    private static readonly Dictionary<string, string> kReasonArguments = new() { [HarnessTestSetup.kReasonArgument] = kReason };

    /// <summary>
    /// Verifies that both AI functions are off unless switched on, so that a harness without settings refuses all runs.
    /// </summary>
    [Test]
    public void AgentSettings_SwitchOffAllFeaturesByDefault()
    {
        AgentSettings agentSettings = new();

        Assert.Multiple(() =>
        {
            Assert.That(agentSettings.ChatEnabled, Is.False);
            Assert.That(agentSettings.ReasonCheckEnabled, Is.False);
            Assert.That(AgentFeatureGate.IsSwitchedOn(AgentFeature.Chat, agentSettings), Is.False);
            Assert.That(AgentFeatureGate.IsSwitchedOn(AgentFeature.ReasonCheck, agentSettings), Is.False);
        });
    }

    /// <summary>
    /// Verifies that a text of the user belongs to the chat.
    /// </summary>
    [Test]
    public void GetFeature_OfUserText_IsChat()
    {
        TurnInput turnInput = TurnInput.Read(kQuestionMessages, null);

        Assert.That(AgentFeatureGate.GetFeature(turnInput), Is.EqualTo(AgentFeature.Chat));
    }

    /// <summary>
    /// Verifies that the answers to tool approval requests belong to the chat, as the reason check never asks for any.
    /// </summary>
    [Test]
    public void GetFeature_OfApprovalAnswers_IsChat()
    {
        TurnInput turnInput = TurnInput.Read(CreateApprovalAnswerMessages(), null);

        Assert.That(AgentFeatureGate.GetFeature(turnInput), Is.EqualTo(AgentFeature.Chat));
    }

    /// <summary>
    /// Verifies that the prompt of the reason check belongs to the reason check, and any other prompt to the chat.
    /// </summary>
    [TestCase(HarnessTestSetup.kConnectionPrompt, AgentFeature.ReasonCheck)]
    [TestCase(HarnessTestSetup.kAccessRequestPrompt, AgentFeature.Chat)]
    [TestCase("unknown_prompt", AgentFeature.Chat)]
    public void GetFeature_OfPrompt_IsReasonCheckOnlyForItsPrompt(string promptName, AgentFeature expectedFeature)
    {
        TurnInput turnInput = TurnInput.Read(kNoMessages, HarnessTestSetup.CreatePromptRunOptions(promptName, kReasonArguments));

        Assert.That(AgentFeatureGate.GetFeature(turnInput), Is.EqualTo(expectedFeature));
    }

    /// <summary>
    /// Verifies that each AI function follows its own switch only, and that an unknown function is off (fail closed).
    /// </summary>
    [TestCase(AgentFeature.Chat, true, false, true)]
    [TestCase(AgentFeature.Chat, false, true, false)]
    [TestCase(AgentFeature.ReasonCheck, false, true, true)]
    [TestCase(AgentFeature.ReasonCheck, true, false, false)]
    [TestCase(kUnknownFeature, true, true, false)]
    public void IsSwitchedOn_FollowsTheSwitchOfTheFeature(AgentFeature feature, bool chatEnabled, bool reasonCheckEnabled, bool expected)
    {
        AgentSettings agentSettings = new() { ChatEnabled = chatEnabled, ReasonCheckEnabled = reasonCheckEnabled };

        Assert.That(AgentFeatureGate.IsSwitchedOn(feature, agentSettings), Is.EqualTo(expected));
    }

    /// <summary>
    /// Verifies that a turn of a function switched off is refused with the function it belongs to, and one switched on is not.
    /// </summary>
    [Test]
    public void EnsureSwitchedOn_RefusesTurnOfFeatureSwitchedOff()
    {
        TurnInput turnInput = TurnInput.Read(kQuestionMessages, null);

        AgentFeatureDisabledException? exception = Assert.Throws<AgentFeatureDisabledException>(
            () => AgentFeatureGate.EnsureSwitchedOn(turnInput, new AgentSettings { ReasonCheckEnabled = true }));
        Assert.That(exception!.Feature, Is.EqualTo(AgentFeature.Chat));
        Assert.That(exception.Message, Does.Contain(nameof(AgentFeature.Chat)));
        Assert.DoesNotThrow(() => AgentFeatureGate.EnsureSwitchedOn(turnInput, new AgentSettings { ChatEnabled = true }));
    }

    /// <summary>
    /// Verifies that the agent refuses a text of the user while the chat is off, without asking the AI provider.
    /// </summary>
    [Test]
    public async Task RunStreamingAsync_WithChatSwitchedOff_RefusesTextWithoutAskingProvider()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient, new AgentSettings { ReasonCheckEnabled = true });
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Modeller);

        AgentFeatureDisabledException? exception = Assert.ThrowsAsync<AgentFeatureDisabledException>(
            async () => await HarnessTestSetup.RunStreamingAsync(agent, kQuestion));

        Assert.That(exception!.Feature, Is.EqualTo(AgentFeature.Chat));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that the agent refuses the answers to tool approval requests while the chat is off, so that no tool runs.
    /// </summary>
    [Test]
    public async Task RunAsync_WithChatSwitchedOff_RefusesApprovalAnswers()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient, new AgentSettings { ReasonCheckEnabled = true });
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Modeller);

        Assert.ThrowsAsync<AgentFeatureDisabledException>(async () => await agent.RunAsync(CreateApprovalAnswerMessages()));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that the agent refuses the reason check prompt while the reason check is off, even with the chat on.
    /// </summary>
    [Test]
    public async Task RunAsync_WithReasonCheckSwitchedOff_RefusesReasonCheckPrompt()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient, new AgentSettings { ChatEnabled = true });
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Modeller);

        AgentFeatureDisabledException? exception = Assert.ThrowsAsync<AgentFeatureDisabledException>(async () =>
            await agent.RunAsync(kNoMessages, options: HarnessTestSetup.CreatePromptRunOptions(HarnessTestSetup.kConnectionPrompt, kReasonArguments)));

        Assert.That(exception!.Feature, Is.EqualTo(AgentFeature.ReasonCheck));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that with only the reason check on, its prompt runs, while any other prompt is refused as part of the chat.
    /// </summary>
    [Test]
    public async Task RunAsync_WithOnlyReasonCheckSwitchedOn_RunsReasonCheckAndRefusesOtherPrompts()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient, new AgentSettings { ReasonCheckEnabled = true });
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Modeller);

        AgentResponse response = await agent.RunAsync(kNoMessages,
            options: HarnessTestSetup.CreatePromptRunOptions(HarnessTestSetup.kConnectionPrompt, kReasonArguments));

        Assert.That(response.Text, Is.EqualTo(kAnswer));
        Assert.That(chatClient.ReceivedMessages.Single().Single().Text, Does.Contain(kReason));
        Assert.ThrowsAsync<AgentFeatureDisabledException>(async () =>
            await agent.RunAsync(kNoMessages, options: HarnessTestSetup.CreatePromptRunOptions(HarnessTestSetup.kAccessRequestPrompt, kReasonArguments)));
        Assert.That(chatClient.ReceivedMessages, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Verifies that the AG-UI endpoint answers a run of a function switched off with a run error and no snapshot,
    /// without asking the AI provider.
    /// </summary>
    [Test]
    public async Task Run_WithFeaturesSwitchedOff_EndsWithRunErrorWithoutAskingModel()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        HarnessSettings settings = new() { McpEndpoint = HarnessTestSetup.kTestMcpEndpoint };
        AgentFactoryHolder agentFactories = new(HarnessTestSetup.CreateFactory(mcpApp, chatClient, new AgentSettings()));
        await using WebApplication app = await HarnessTestServer.StartOnLoopback(settings, agentFactories);
        using HttpClient httpClient = new() { BaseAddress = McpTestServer.GetBaseAddress(app) };

        using HttpResponseMessage response = await HarnessTestServer.PostRun(httpClient, McpTestServer.CreateMiddlewareToken(Roles.Reporter),
            HarnessTestServer.CreateRunBody(kThreadId, kQuestion));
        List<JsonElement> events = await HarnessTestServer.ReadEventsAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(HarnessTestServer.GetEventTypes(events), Does.Contain(HarnessTestServer.kRunError));
            Assert.That(HarnessTestServer.GetEventTypes(events), Does.Not.Contain(HarnessTestServer.kMessagesSnapshot));
            Assert.That(chatClient.ReceivedMessages, Is.Empty);
        });
    }

    /// <summary>
    /// Creates a conversation answering a tool approval request, as the AG-UI hosting passes the resume entry of a run.
    /// </summary>
    private static List<ChatMessage> CreateApprovalAnswerMessages()
    {
        FunctionCallContent call = new(kRequestId, kToolName);
        ToolApprovalRequestContent request = new(kRequestId, call);
        return
        [
            new ChatMessage(ChatRole.User, kQuestion),
            new ChatMessage(ChatRole.Assistant, new List<AIContent> { request }),
            new ChatMessage(ChatRole.User, new List<AIContent> { request.CreateResponse(true) })
        ];
    }
}
