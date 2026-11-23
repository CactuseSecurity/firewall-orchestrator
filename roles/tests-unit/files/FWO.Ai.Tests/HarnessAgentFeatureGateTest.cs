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
/// Tests the switches and role filters of the AI functions in the harness: which function a turn belongs to (the reason
/// check prompt to the reason check, everything else to the chat), and that a turn of a function switched off or not
/// allowed for the roles of the user is refused before it reaches the MCP server or the AI provider, by the user agent
/// and through the AG-UI endpoint, which takes the roles from the JWT.
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
    private static readonly List<string> kNoRoles = [];
    private static readonly List<string> kAdminRoles = [Roles.Admin];
    private static readonly List<string> kModellerRoles = [Roles.Modeller];
    private static readonly List<string> kReporterRoles = [Roles.Reporter];
    private static readonly List<string> kReporterAndAdminRoles = [Roles.Reporter, Roles.Admin];
    private static readonly List<string> kUpperCaseModellerRoles = ["MODELLER"];

    private static readonly List<TestCaseData> kRoleFilterCases =
    [
        new TestCaseData(AgentFeature.Chat, kNoRoles, kAdminRoles, kNoRoles, true).SetName("ChatWithoutRolesAllowsUserWithoutRoles"),
        new TestCaseData(AgentFeature.Chat, kAdminRoles, kNoRoles, kReporterAndAdminRoles, true).SetName("ChatAllowsUserHoldingOneRole"),
        new TestCaseData(AgentFeature.Chat, kAdminRoles, kNoRoles, kReporterRoles, false).SetName("ChatRefusesUserWithOtherRole"),
        new TestCaseData(AgentFeature.Chat, kAdminRoles, kNoRoles, kNoRoles, false).SetName("ChatRefusesUserWithoutRoles"),
        new TestCaseData(AgentFeature.ReasonCheck, kAdminRoles, kNoRoles, kReporterRoles, true).SetName("ReasonCheckFollowsItsOwnRoles"),
        new TestCaseData(AgentFeature.ReasonCheck, kNoRoles, kModellerRoles, kUpperCaseModellerRoles, true).SetName("ReasonCheckIgnoresCaseOfRoles"),
        new TestCaseData(AgentFeature.ReasonCheck, kNoRoles, kModellerRoles, kReporterRoles, false).SetName("ReasonCheckRefusesUserWithOtherRole"),
        new TestCaseData(kUnknownFeature, kNoRoles, kNoRoles, kAdminRoles, false).SetName("UnknownFeatureIsNotAllowed")
    ];
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
    /// Verifies that the AI functions are not limited to roles unless roles are set, so that switching a function on is
    /// enough to offer it to every user.
    /// </summary>
    [Test]
    public void AgentSettings_LimitNoFeatureToRolesByDefault()
    {
        AgentSettings agentSettings = new();

        Assert.Multiple(() =>
        {
            Assert.That(agentSettings.ChatRoles, Is.Empty);
            Assert.That(agentSettings.ReasonCheckRoles, Is.Empty);
            Assert.That(AgentFeatureGate.IsAllowedForRoles(AgentFeature.Chat, agentSettings, kNoRoles), Is.True);
            Assert.That(AgentFeatureGate.IsAllowedForRoles(AgentFeature.ReasonCheck, agentSettings, kNoRoles), Is.True);
        });
    }

    /// <summary>
    /// Verifies that the snapshot of the settings keeps the roles they had, so that changing the lists afterwards does
    /// not change the roles of a factory.
    /// </summary>
    [Test]
    public void CreateSnapshot_CopiesRoles()
    {
        List<string> chatRoles = [Roles.Admin];
        List<string> reasonCheckRoles = [Roles.Modeller];
        AgentSettings agentSettings = new() { ChatRoles = chatRoles, ReasonCheckRoles = reasonCheckRoles };

        AgentSettings snapshot = agentSettings.CreateSnapshot();
        chatRoles.Add(Roles.Reporter);
        reasonCheckRoles.Clear();

        Assert.That(snapshot.ChatRoles, Is.EqualTo(kAdminRoles));
        Assert.That(snapshot.ReasonCheckRoles, Is.EqualTo(kModellerRoles));
    }

    /// <summary>
    /// Verifies that each AI function follows its own roles only, that a user needs one of them (ignoring case), that
    /// no roles allow every user, and that an unknown function is not allowed (fail closed).
    /// </summary>
    [TestCaseSource(nameof(kRoleFilterCases))]
    public void IsAllowedForRoles_FollowsTheRolesOfTheFeature(AgentFeature feature, List<string> chatRoles, List<string> reasonCheckRoles,
        List<string> userRoles, bool expected)
    {
        AgentSettings agentSettings = new() { ChatRoles = chatRoles, ReasonCheckRoles = reasonCheckRoles };

        Assert.That(AgentFeatureGate.IsAllowedForRoles(feature, agentSettings, userRoles), Is.EqualTo(expected));
    }

    /// <summary>
    /// Verifies that a turn of a user holding none of the roles of its function is refused with the function, and a
    /// turn of a user holding one of them is not.
    /// </summary>
    [Test]
    public void EnsureAllowed_RefusesTurnOfUserWithoutAllowedRole()
    {
        TurnInput turnInput = TurnInput.Read(kQuestionMessages, null);
        AgentSettings agentSettings = new() { ChatEnabled = true, ChatRoles = kAdminRoles };

        AgentFeatureNotPermittedException? exception = Assert.Throws<AgentFeatureNotPermittedException>(
            () => AgentFeatureGate.EnsureAllowed(turnInput, agentSettings, kReporterRoles));
        Assert.That(exception!.Feature, Is.EqualTo(AgentFeature.Chat));
        Assert.That(exception.Message, Does.Contain(nameof(AgentFeature.Chat)));
        Assert.DoesNotThrow(() => AgentFeatureGate.EnsureAllowed(turnInput, agentSettings, kReporterAndAdminRoles));
    }

    /// <summary>
    /// Verifies that a function switched off is refused as switched off, whatever the roles of the user.
    /// </summary>
    [Test]
    public void EnsureAllowed_ReportsSwitchedOffBeforeRoles()
    {
        TurnInput turnInput = TurnInput.Read(kQuestionMessages, null);
        AgentSettings agentSettings = new() { ChatRoles = kAdminRoles };

        Assert.Throws<AgentFeatureDisabledException>(() => AgentFeatureGate.EnsureAllowed(turnInput, agentSettings, kReporterRoles));
        Assert.Throws<AgentFeatureDisabledException>(() => AgentFeatureGate.EnsureAllowed(turnInput, agentSettings, kAdminRoles));
    }

    /// <summary>
    /// Verifies that missing user roles are refused.
    /// </summary>
    [Test]
    public void IsAllowedForRoles_WithoutUserRoles_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AgentFeatureGate.IsAllowedForRoles(AgentFeature.Chat, new AgentSettings(), null!));
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
    public void EnsureAllowed_RefusesTurnOfFeatureSwitchedOff()
    {
        TurnInput turnInput = TurnInput.Read(kQuestionMessages, null);

        AgentFeatureDisabledException? exception = Assert.Throws<AgentFeatureDisabledException>(
            () => AgentFeatureGate.EnsureAllowed(turnInput, new AgentSettings { ReasonCheckEnabled = true }, kNoRoles));
        Assert.That(exception!.Feature, Is.EqualTo(AgentFeature.Chat));
        Assert.That(exception.Message, Does.Contain(nameof(AgentFeature.Chat)));
        Assert.DoesNotThrow(() => AgentFeatureGate.EnsureAllowed(turnInput, new AgentSettings { ChatEnabled = true }, kNoRoles));
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
    /// Verifies that the agent refuses a text of a user holding none of the roles of the chat, without asking the AI
    /// provider, and runs it for a user holding one of them.
    /// </summary>
    [Test]
    public async Task RunStreamingAsync_WithChatNotAllowedForRoles_RefusesTextWithoutAskingProvider()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient, new AgentSettings { ChatEnabled = true, ChatRoles = kAdminRoles });

        AgentFeatureNotPermittedException? exception = Assert.ThrowsAsync<AgentFeatureNotPermittedException>(
            async () => await HarnessTestSetup.RunStreamingAsync(HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter), kQuestion));
        Assert.That(exception!.Feature, Is.EqualTo(AgentFeature.Chat));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);

        AgentResponse response = await HarnessTestSetup.CreateUserAgent(factory, Roles.Admin).RunAsync(kQuestion);
        Assert.That(response.Text, Is.EqualTo(kAnswer));
    }

    /// <summary>
    /// Verifies that the reason check limited to modellers runs for a modeller and is refused for a reporter, while the
    /// chat without roles stays open to the reporter.
    /// </summary>
    [Test]
    public async Task RunAsync_WithReasonCheckLimitedToModellers_RefusesOtherRoles()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer).RespondWithText(kAnswer);
        AgentSettings agentSettings = new() { ChatEnabled = true, ReasonCheckEnabled = true, ReasonCheckRoles = kModellerRoles };
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient, agentSettings);
        FwoUserAgent reporterAgent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        AgentFeatureNotPermittedException? exception = Assert.ThrowsAsync<AgentFeatureNotPermittedException>(async () =>
            await reporterAgent.RunAsync(kNoMessages, options: HarnessTestSetup.CreatePromptRunOptions(HarnessTestSetup.kConnectionPrompt, kReasonArguments)));
        Assert.That(exception!.Feature, Is.EqualTo(AgentFeature.ReasonCheck));
        Assert.That(chatClient.ReceivedMessages, Is.Empty);

        AgentResponse reasonCheck = await HarnessTestSetup.CreateUserAgent(factory, Roles.Modeller).RunAsync(kNoMessages,
            options: HarnessTestSetup.CreatePromptRunOptions(HarnessTestSetup.kConnectionPrompt, kReasonArguments));
        AgentResponse chat = await reporterAgent.RunAsync(kQuestion);
        Assert.That(reasonCheck.Text, Is.EqualTo(kAnswer));
        Assert.That(chat.Text, Is.EqualTo(kAnswer));
        Assert.That(chatClient.ReceivedMessages, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Verifies that the AG-UI endpoint takes the roles from the JWT of the request: a run of a user whose token holds
    /// none of the roles of the chat ends with a run error without asking the AI provider, a run of a user whose token
    /// holds one of them is answered.
    /// </summary>
    [Test]
    public async Task Run_WithChatLimitedToRoles_ChecksRolesOfToken()
    {
        await using WebApplication mcpApp = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        HarnessSettings settings = new() { McpEndpoint = HarnessTestSetup.kTestMcpEndpoint };
        AgentSettings agentSettings = new() { ChatEnabled = true, ChatRoles = kAdminRoles };
        AgentFactoryHolder agentFactories = new(HarnessTestSetup.CreateFactory(mcpApp, chatClient, agentSettings));
        await using WebApplication app = await HarnessTestServer.StartOnLoopback(settings, agentFactories);
        using HttpClient httpClient = new() { BaseAddress = McpTestServer.GetBaseAddress(app) };

        using HttpResponseMessage refused = await HarnessTestServer.PostRun(httpClient, McpTestServer.CreateMiddlewareToken(Roles.Reporter),
            HarnessTestServer.CreateRunBody(kThreadId, kQuestion));
        List<JsonElement> refusedEvents = await HarnessTestServer.ReadEventsAsync(refused);
        Assert.Multiple(() =>
        {
            Assert.That(HarnessTestServer.GetEventTypes(refusedEvents), Does.Contain(HarnessTestServer.kRunError));
            Assert.That(chatClient.ReceivedMessages, Is.Empty);
        });

        using HttpResponseMessage answered = await HarnessTestServer.PostRun(httpClient, McpTestServer.CreateMiddlewareToken(Roles.Reporter, Roles.Admin),
            HarnessTestServer.CreateRunBody(kThreadId, kQuestion));
        List<JsonElement> answeredEvents = await HarnessTestServer.ReadEventsAsync(answered);
        Assert.Multiple(() =>
        {
            Assert.That(HarnessTestServer.GetEventTypes(answeredEvents), Does.Not.Contain(HarnessTestServer.kRunError));
            Assert.That(HarnessTestServer.GetEventTypes(answeredEvents), Does.Contain(HarnessTestServer.kMessagesSnapshot));
            Assert.That(chatClient.ReceivedMessages, Has.Count.EqualTo(1));
        });
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
