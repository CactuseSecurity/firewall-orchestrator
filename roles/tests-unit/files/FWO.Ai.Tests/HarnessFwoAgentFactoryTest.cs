using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Mcp;
using FWO.Ai.Harness.Providers;
using FWO.Ai.Mcp;
using FWO.Data.Ai;
using FWO.Basics;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the agent factory: settings validation, ownership of the provider client, the agent and agent loop shared
/// by its user agents, the MCP connection of a run, and what a run sends to the model (instructions, tools).
/// </summary>
[TestFixture]
internal class HarnessFwoAgentFactoryTest
{
    private const string kModel = "test-model";
    private const string kApiKey = "sk-harness-test-key-4711";
    private const string kQuestion = "Which roles do I have?";
    private const string kAnswer = "You are a reporter.";
    private const string kCallId = "call-1";
    private const string kTokenFailureDetail = "token store at 10.9.9.9 unreachable";
    private const string kRemoteHttpMcpEndpoint = "http://middleware.example.com/mcp";
    private const string kToken = "token";
    private const int kContextWindowTokens = 1000;
    private const int kTinyContextWindowTokens = 3;
    private const int kMaxOutputTokensBelowDefaultReserve = 249;
    private const int kMaxOutputTokensAtDefaultReserve = 250;
    private const int kExplicitReserveTokens = 600;
    private const int kMaxOutputTokensBelowExplicitReserve = 500;
    private const int kConfiguredIterations = 7;
    private const int kConfiguredErrors = 2;
    private const int kChangedIterations = 1;
    private const int kNoErrorsAllowed = 0;

    private static readonly List<string> kReporterTools = [HarnessTestSetup.kCurrentUserTool];
    private static readonly List<string> kAdminTools = [HarnessTestSetup.kCurrentUserTool, HarnessTestSetup.kRandomNumberTool];
    private static readonly List<string> kReporterRoles = [Roles.Reporter];
    private static readonly List<string> kNoRoles = [];

    private static readonly List<TestCaseData> kInvalidAgentSettings =
    [
        new TestCaseData(new AgentSettings { MaxIterationsPerRequest = 0 }, kContextWindowTokens, typeof(ArgumentOutOfRangeException)).SetName("NoIteration"),
        new TestCaseData(new AgentSettings { MaxConsecutiveErrorsPerRequest = -1 }, kContextWindowTokens, typeof(ArgumentOutOfRangeException)).SetName("NegativeErrors"),
        new TestCaseData(new AgentSettings(), 0, typeof(ArgumentOutOfRangeException)).SetName("NoContextWindow"),
        new TestCaseData(new AgentSettings(), -1, typeof(ArgumentOutOfRangeException)).SetName("NegativeContextWindow"),
        new TestCaseData(new AgentSettings { CompactionReserveTokens = 0 }, kContextWindowTokens, typeof(ArgumentOutOfRangeException)).SetName("NoReserve"),
        new TestCaseData(new AgentSettings { CompactionReserveTokens = -1 }, kContextWindowTokens, typeof(ArgumentOutOfRangeException)).SetName("NegativeReserve"),
        new TestCaseData(new AgentSettings { CompactionReserveTokens = kContextWindowTokens }, kContextWindowTokens, typeof(ArgumentOutOfRangeException))
            .SetName("ReserveEqualToContextWindow"),
        new TestCaseData(new AgentSettings { CompactionReserveTokens = kContextWindowTokens + 1 }, kContextWindowTokens, typeof(ArgumentOutOfRangeException))
            .SetName("ReserveAboveContextWindow"),
        new TestCaseData(new AgentSettings(), kTinyContextWindowTokens, typeof(ArgumentOutOfRangeException)).SetName("DefaultReserveOfTinyContextWindowIsZero"),
        new TestCaseData(null, kContextWindowTokens, typeof(ArgumentNullException)).SetName("NoSettings")
    ];

    private static readonly List<TestCaseData> kBoundaryAgentSettings =
    [
        new TestCaseData(new AgentSettings { MaxIterationsPerRequest = 1, MaxConsecutiveErrorsPerRequest = 0 }).SetName("SingleIterationWithoutErrors"),
        new TestCaseData(new AgentSettings { CompactionReserveTokens = 1 }).SetName("SmallestReserve"),
        new TestCaseData(new AgentSettings { CompactionReserveTokens = kContextWindowTokens - 1 }).SetName("LargestReserve")
    ];

    private static readonly List<TestCaseData> kInvalidProviderSettings =
    [
        new TestCaseData(null, new AgentSettings(), typeof(ArgumentNullException)).SetName("NoProviderSettings"),
        new TestCaseData(CreateOllamaSettings(), null, typeof(ArgumentNullException)).SetName("NoAgentSettings"),
        new TestCaseData(new AiProviderSettings { Provider = AiProvider.Ollama, ContextWindowTokens = kContextWindowTokens }, new AgentSettings(), typeof(ArgumentException))
            .SetName("NoModel"),
        new TestCaseData(new AiProviderSettings { Provider = AiProvider.OpenAi, Model = kModel, ContextWindowTokens = kContextWindowTokens }, new AgentSettings(),
            typeof(ArgumentException)).SetName("NoApiKey"),
        new TestCaseData(new AiProviderSettings { Provider = AiProvider.Ollama, Model = kModel }, new AgentSettings(), typeof(ArgumentOutOfRangeException))
            .SetName("ProviderWithoutContextWindow"),
        new TestCaseData(CreateOllamaSettings(kContextWindowTokens), new AgentSettings(), typeof(ArgumentOutOfRangeException)).SetName("OutputFillsContextWindow"),
        new TestCaseData(CreateOllamaSettings(kMaxOutputTokensAtDefaultReserve), new AgentSettings(), typeof(ArgumentOutOfRangeException))
            .SetName("DefaultReserveNotAboveOutput"),
        new TestCaseData(CreateOllamaSettings(kMaxOutputTokensBelowExplicitReserve), new AgentSettings { CompactionReserveTokens = kMaxOutputTokensBelowExplicitReserve },
            typeof(ArgumentOutOfRangeException)).SetName("ExplicitReserveNotAboveOutput"),
        new TestCaseData(CreateOllamaSettings(), new AgentSettings { MaxIterationsPerRequest = 0 }, typeof(ArgumentOutOfRangeException)).SetName("InvalidAgentSettings")
    ];

    /// <summary>
    /// Verifies that invalid agent settings or context windows are refused with the documented exception, and that
    /// the factory disposes the provider client it was given.
    /// </summary>
    [TestCaseSource(nameof(kInvalidAgentSettings))]
    public void Ctor_WithInvalidAgentSettings_ThrowsAndDisposesClient(AgentSettings? agentSettings, int contextWindowTokens, Type expectedException)
    {
        ScriptedChatClient providerClient = new();

        Assert.Throws(expectedException, () => _ = new FwoAgentFactory(providerClient, contextWindowTokens, agentSettings!));

        Assert.That(providerClient.DisposeCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that the limits of the valid settings are accepted, and that the provider client stays open until the
    /// factory is disposed.
    /// </summary>
    [TestCaseSource(nameof(kBoundaryAgentSettings))]
    public void Ctor_WithBoundaryAgentSettings_CreatesFactory(AgentSettings agentSettings)
    {
        ScriptedChatClient providerClient = new();

        FwoAgentFactory factory = new(providerClient, kContextWindowTokens, agentSettings);

        Assert.That(providerClient.DisposeCount, Is.EqualTo(0));
        factory.Dispose();
        Assert.That(providerClient.DisposeCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that a missing provider client is refused.
    /// </summary>
    [Test]
    public void Ctor_WithoutProviderClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new FwoAgentFactory(null!, kContextWindowTokens, new AgentSettings()));
    }

    /// <summary>
    /// Verifies that invalid provider or agent settings are refused before a provider client is created, including a
    /// compaction reserve that leaves no room for a full answer.
    /// </summary>
    [TestCaseSource(nameof(kInvalidProviderSettings))]
    public void Ctor_FromSettingsWithInvalidSettings_Throws(AiProviderSettings? providerSettings, AgentSettings? agentSettings, Type expectedException)
    {
        Assert.Throws(expectedException, () => _ = new FwoAgentFactory(providerSettings!, agentSettings!));
    }

    /// <summary>
    /// Verifies that a compaction reserve larger than the maximum number of output tokens is accepted, by default
    /// (a quarter of the context window) and explicitly set.
    /// </summary>
    [Test]
    public void Ctor_FromSettingsWithReserveAboveMaxOutputTokens_CreatesFactory()
    {
        AgentSettings explicitReserve = new() { CompactionReserveTokens = kExplicitReserveTokens };

        Assert.DoesNotThrow(() => new FwoAgentFactory(CreateOllamaSettings(kMaxOutputTokensBelowDefaultReserve), new AgentSettings()).Dispose());
        Assert.DoesNotThrow(() => new FwoAgentFactory(CreateOllamaSettings(kMaxOutputTokensBelowExplicitReserve), explicitReserve).Dispose());
    }

    /// <summary>
    /// Verifies that a user agent without a handler factory connects to the MCP server on a loopback port with the
    /// user's JWT, as in production, and offers the user's tools.
    /// </summary>
    [Test]
    public async Task RunAsync_WithoutMcpHandlerFactory_ConnectsToMcpServerOverLoopback()
    {
        await using WebApplication app = await McpTestServer.StartOnLoopback();
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);
        McpConnectionSettings mcpSettings = new()
        {
            Endpoint = new Uri(McpTestServer.GetBaseAddress(app), McpServerSetup.kMcpPath),
            AccessTokenProvider = _ => ValueTask.FromResult(accessToken)
        };
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = new(chatClient, kContextWindowTokens, new AgentSettings { ChatEnabled = true });

        await factory.CreateUserAgent(mcpSettings, kReporterRoles).RunAsync(kQuestion);

        Assert.That(HarnessTestSetup.GetToolNames(chatClient.ReceivedOptions.Single()), Is.EquivalentTo(kReporterTools));
    }

    /// <summary>
    /// Verifies that a run refuses an MCP endpoint receiving the JWT over plain http to a remote host before the token
    /// is asked for, and without asking the model.
    /// </summary>
    [Test]
    public async Task RunAsync_WithRemoteHttpEndpoint_ThrowsBeforeAskingForToken()
    {
        await using WebApplication app = await McpTestServer.Start();
        bool tokenRequested = false;
        McpConnectionSettings mcpSettings = CreateMcpSettings(new Uri(kRemoteHttpMcpEndpoint), () => tokenRequested = true);
        ScriptedChatClient chatClient = new();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = factory.CreateUserAgent(mcpSettings, kNoRoles);

        Assert.ThrowsAsync<ArgumentException>(async () => await agent.RunAsync(kQuestion));

        Assert.That(tokenRequested, Is.False);
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that a disposed factory refuses to create a user agent.
    /// </summary>
    [Test]
    public async Task CreateUserAgent_AfterDispose_Throws()
    {
        await using WebApplication app = await McpTestServer.Start();
        FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, new ScriptedChatClient());
        factory.Dispose();

        Assert.Throws<ObjectDisposedException>(() => HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter));
    }

    /// <summary>
    /// Verifies that missing connection settings are refused.
    /// </summary>
    [Test]
    public async Task CreateUserAgent_WithoutConnectionSettings_Throws()
    {
        await using WebApplication app = await McpTestServer.Start();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, new ScriptedChatClient());

        Assert.Throws<ArgumentNullException>(() => factory.CreateUserAgent(null!, kNoRoles));
    }

    /// <summary>
    /// Verifies that missing user roles are refused, as they decide which AI functions the user may run.
    /// </summary>
    [Test]
    public async Task CreateUserAgent_WithoutUserRoles_Throws()
    {
        await using WebApplication app = await McpTestServer.Start();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, new ScriptedChatClient());
        McpConnectionSettings mcpSettings = HarnessTestSetup.CreateMcpSettings(_ => ValueTask.FromResult(kToken));

        Assert.Throws<ArgumentNullException>(() => factory.CreateUserAgent(mcpSettings, null!));
    }

    /// <summary>
    /// Verifies that a run cancelled before it starts neither asks for the token nor asks the model.
    /// </summary>
    [Test]
    public async Task RunAsync_CancelledBeforeStart_ThrowsWithoutAskingModel()
    {
        await using WebApplication app = await McpTestServer.Start();
        bool tokenRequested = false;
        ScriptedChatClient chatClient = new();
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = factory.CreateUserAgent(CreateMcpSettings(HarnessTestSetup.kTestMcpEndpoint, () => tokenRequested = true), kNoRoles);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        Assert.CatchAsync<OperationCanceledException>(async () => await agent.RunAsync(kQuestion, cancellationToken: cancellation.Token));

        Assert.That(tokenRequested, Is.False);
        Assert.That(chatClient.ReceivedMessages, Is.Empty);
    }

    /// <summary>
    /// Verifies that the user agents of a factory run on one shared chat client agent with the name of the agent,
    /// with one function invocation with the configured limits, without detailed errors and without concurrent calls.
    /// </summary>
    [Test]
    public async Task CreateUserAgent_SharesOneAgentAndFunctionInvocationWithConfiguredLimits()
    {
        await using WebApplication app = await McpTestServer.Start();
        AgentSettings agentSettings = new() { MaxIterationsPerRequest = kConfiguredIterations, MaxConsecutiveErrorsPerRequest = kConfiguredErrors };
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, new ScriptedChatClient(), agentSettings);
        FwoUserAgent firstAgent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);
        FwoUserAgent secondAgent = HarnessTestSetup.CreateUserAgent(factory, Roles.Admin);

        FunctionInvokingChatClient functionInvocation = GetFunctionInvocation(firstAgent);

        Assert.That(firstAgent.GetService<ChatClientAgent>(), Is.SameAs(secondAgent.GetService<ChatClientAgent>()));
        Assert.That(firstAgent.GetService<ChatClientAgent>()!.Name, Is.EqualTo(FwoAgentFactory.kAgentName));
        Assert.That(GetFunctionInvocation(secondAgent), Is.SameAs(functionInvocation));
        Assert.That(functionInvocation.MaximumIterationsPerRequest, Is.EqualTo(kConfiguredIterations));
        Assert.That(functionInvocation.MaximumConsecutiveErrorsPerRequest, Is.EqualTo(kConfiguredErrors));
        Assert.That(functionInvocation.IncludeDetailedErrors, Is.False);
        Assert.That(functionInvocation.AllowConcurrentInvocation, Is.False);
    }

    /// <summary>
    /// Verifies that changing the settings after creating the factory does not change its agents' agent loop.
    /// </summary>
    [Test]
    public async Task CreateUserAgent_ChangingSettingsAfterwards_HasNoEffect()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        AgentSettings agentSettings = new() { MaxIterationsPerRequest = kConfiguredIterations, ChatEnabled = true };
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient, agentSettings);
        agentSettings.MaxIterationsPerRequest = kChangedIterations;
        agentSettings.CompactionReserveTokens = 0;
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        await agent.RunAsync(kQuestion);

        Assert.That(GetFunctionInvocation(agent).MaximumIterationsPerRequest, Is.EqualTo(kConfiguredIterations));
    }

    /// <summary>
    /// Verifies that the instructions of the MCP server reach the model as option of every request, not as a message,
    /// and are not part of the conversation the harness returns.
    /// </summary>
    [Test]
    public async Task RunAsync_SendsInstructionsOfMcpServerAsOptionAndNotAsMessage()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .RespondWithFunctionCall(kCallId, HarnessTestSetup.kCurrentUserTool).RespondWithText(kAnswer).RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        List<AgentResponseUpdate> firstUpdates = await HarnessTestSetup.RunStreamingAsync(agent, kQuestion);
        List<ChatMessage> nextMessages = [.. HarnessTestSetup.GetSnapshot(firstUpdates), new ChatMessage(ChatRole.User, kQuestion)];
        List<AgentResponseUpdate> nextUpdates = await HarnessTestSetup.RunStreamingAsync(agent, nextMessages);

        Assert.That(chatClient.ReceivedOptions.Select(options => options?.Instructions), Has.All.EqualTo(HarnessTestSetup.kInstructions));
        Assert.That(chatClient.ReceivedMessages.SelectMany(messages => messages).Select(message => message.Role), Has.None.EqualTo(ChatRole.System));
        Assert.That(HarnessTestSetup.GetTexts(HarnessTestSetup.GetSnapshot(nextUpdates)), Has.None.Contains(HarnessTestSetup.kInstructions));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that each request offers the tools the user may call and leaves it to the model whether to call them.
    /// </summary>
    [TestCase(Roles.Reporter)]
    [TestCase(Roles.Admin)]
    public async Task RunAsync_OffersToolsOfUserWithAutoToolMode(string role)
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, role);

        await agent.RunAsync(kQuestion);

        ChatOptions options = chatClient.ReceivedOptions.Single()!;
        List<string> expectedTools = role == Roles.Admin ? kAdminTools : kReporterTools;
        Assert.That(HarnessTestSetup.GetToolNames(options), Is.EquivalentTo(expectedTools));
        Assert.That(options.ToolMode, Is.InstanceOf<AutoChatToolMode>());
    }

    /// <summary>
    /// Verifies that a run of a user without tools offers neither tools nor a tool mode to the model.
    /// </summary>
    [Test]
    public async Task RunAsync_WithoutTools_OffersNoToolsAndNoToolMode()
    {
        await using WebApplication app = await McpTestServer.StartWith(mcpServer => mcpServer.WithRequestFilters(filters =>
            filters.AddListToolsFilter(_ => (_, _) => ValueTask.FromResult(new ListToolsResult()))));
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter);

        AgentResponse response = await agent.RunAsync(kQuestion);

        Assert.That(response.Text, Is.EqualTo(kAnswer));
        ChatOptions? options = chatClient.ReceivedOptions.Single();
        Assert.That(HarnessTestSetup.OffersTools(options), Is.False);
        Assert.That(options?.ToolMode, Is.Null);
    }

    /// <summary>
    /// Verifies that a failing tool call is reported to the model without the details of the exception.
    /// </summary>
    [Test]
    public async Task RunAsync_WithFailingTool_ReportsFailureWithoutDetails()
    {
        await using WebApplication app = await McpTestServer.Start();
        FailingTokenProvider tokenProvider = new(McpTestServer.CreateMiddlewareToken(Roles.Reporter));
        ScriptedChatClient chatClient = new ScriptedChatClient { Strict = true }
            .Respond((_, _, _) => tokenProvider.FailFromNowOn(ScriptedChatClient.CreateFunctionCallResponse(kCallId, HarnessTestSetup.kCurrentUserTool)))
            .RespondWithText(kAnswer);
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, tokenProvider.GetTokenAsync);

        AgentResponse response = await agent.RunAsync(kQuestion);

        Assert.That(response.Text, Is.EqualTo(kAnswer));
        FunctionResultContent toolResult = HarnessTestSetup.GetContents<FunctionResultContent>(chatClient.ReceivedMessages[1]).Single();
        Assert.That(toolResult.Exception, Is.Not.Null);
        Assert.That(JsonSerializer.Serialize(toolResult.Result), Does.Not.Contain(kTokenFailureDetail));
        Assert.That(chatClient.Rejections, Is.Empty);
    }

    /// <summary>
    /// Verifies that a failing tool call fails the turn once the configured number of consecutive errors is exceeded,
    /// without a snapshot, so that the client keeps the conversation it sent.
    /// </summary>
    [Test]
    public async Task RunAsync_WithFailingToolAndNoErrorsAllowed_FailsTurn()
    {
        await using WebApplication app = await McpTestServer.Start();
        FailingTokenProvider tokenProvider = new(McpTestServer.CreateMiddlewareToken(Roles.Reporter));
        ScriptedChatClient chatClient = new ScriptedChatClient()
            .Respond((_, _, _) => tokenProvider.FailFromNowOn(ScriptedChatClient.CreateFunctionCallResponse(kCallId, HarnessTestSetup.kCurrentUserTool)));
        using FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient, new AgentSettings { MaxConsecutiveErrorsPerRequest = kNoErrorsAllowed, ChatEnabled = true });
        FwoUserAgent agent = HarnessTestSetup.CreateUserAgent(factory, tokenProvider.GetTokenAsync);
        List<AgentResponseUpdate> updates = new();

        Assert.CatchAsync<Exception>(async () =>
        {
            await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(kQuestion))
            {
                updates.Add(update);
            }
        });

        Assert.That(chatClient.ReceivedMessages, Has.Count.EqualTo(1));
        Assert.That(updates.Any(HarnessTestSetup.IsSnapshot), Is.False);
    }

    /// <summary>
    /// Verifies that the runs of user agents leave the provider client shared by them open, and that disposing the
    /// factory disposes it once, also when disposed twice.
    /// </summary>
    [Test]
    public async Task Dispose_DisposesSharedClientWithFactoryOnly()
    {
        await using WebApplication app = await McpTestServer.Start();
        ScriptedChatClient chatClient = new ScriptedChatClient().RespondWithText(kAnswer).RespondWithText(kAnswer);
        FwoAgentFactory factory = HarnessTestSetup.CreateFactory(app, chatClient);

        await HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter).RunAsync(kQuestion);
        AgentResponse response = await HarnessTestSetup.CreateUserAgent(factory, Roles.Reporter).RunAsync(kQuestion);

        Assert.That(chatClient.DisposeCount, Is.EqualTo(0));
        Assert.That(response.Text, Is.EqualTo(kAnswer));
        factory.Dispose();
        factory.Dispose();
        Assert.That(chatClient.DisposeCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Returns the function invocation of the agent loop a user agent runs on.
    /// </summary>
    private static FunctionInvokingChatClient GetFunctionInvocation(FwoUserAgent agent)
    {
        ChatClientAgent chatClientAgent = agent.GetService<ChatClientAgent>()!;
        return chatClientAgent.ChatClient.GetService<FunctionInvokingChatClient>()!;
    }

    /// <summary>
    /// Creates valid Ollama settings with the context window of the tests and the given maximum of output tokens.
    /// </summary>
    private static AiProviderSettings CreateOllamaSettings(int? maxOutputTokens = null)
    {
        return new AiProviderSettings
        {
            Provider = AiProvider.Ollama,
            Model = kModel,
            ContextWindowTokens = kContextWindowTokens,
            MaxOutputTokens = maxOutputTokens
        };
    }

    /// <summary>
    /// Creates MCP settings whose token provider reports that it was asked.
    /// </summary>
    private static McpConnectionSettings CreateMcpSettings(Uri endpoint, Action onTokenRequested)
    {
        return new McpConnectionSettings
        {
            Endpoint = endpoint,
            AccessTokenProvider = _ =>
            {
                onTokenRequested();
                return ValueTask.FromResult(kToken);
            }
        };
    }

    /// <summary>
    /// Token provider that can be switched to fail, e.g. like a token renewal that fails, so that tool calls fail
    /// on the client side with an exception carrying details.
    /// </summary>
    private sealed class FailingTokenProvider(string accessToken)
    {
        private volatile bool fail;

        /// <summary>
        /// Lets every later token request fail and returns the given response, so that the model's tool call
        /// fails after the run has connected and loaded the tools.
        /// </summary>
        internal Task<ChatResponse> FailFromNowOn(ChatResponse response)
        {
            fail = true;
            return Task.FromResult(response);
        }

        /// <summary>
        /// Returns the token, or fails with an exception whose message must not reach the model.
        /// </summary>
        internal ValueTask<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            if (fail)
            {
                throw new InvalidOperationException(kTokenFailureDetail);
            }
            return ValueTask.FromResult(accessToken);
        }
    }
}
