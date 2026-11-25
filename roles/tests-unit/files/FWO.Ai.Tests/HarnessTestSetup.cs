using AGUI.Abstractions;
using AGUI.Server;
using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Mcp;
using FWO.Ai.Mcp;
using FWO.Ai.Mcp.Prompts;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Ai.Tests;

/// <summary>
/// Connects the harness to the MCP server pipeline running on a test server (see <see cref="McpTestServer"/>), and
/// helpers shared by the harness tests.
/// </summary>
internal static class HarnessTestSetup
{
    internal const string kCurrentUserTool = "get_current_user";
    internal const string kRoleRestrictedTool = McpTestRoleRestrictedTools.kRoleRestrictedTool;
    internal const string kAccessRequestPrompt = "check_access_request_reason";
    internal const string kConnectionPrompt = "check_connection_reason";
    internal const string kReasonArgument = "reason";
    internal const string kInstructions = AgentPrompts.kAgentInstructions;

    /// <summary>
    /// Context window of the agent factories of the tests; large enough that a history is only compacted in the tests
    /// meant to compact it.
    /// </summary>
    internal const int kContextWindowTokens = 100000;

    private const string kTestServerBaseAddress = "http://localhost/";

    /// <summary>
    /// MCP endpoint of the test server.
    /// </summary>
    internal static readonly Uri kTestMcpEndpoint = new(new Uri(kTestServerBaseAddress), McpServerSetup.kMcpPath);

    /// <summary>
    /// Connects to the MCP server of the test server with a token of the default test user holding the given roles.
    /// </summary>
    internal static Task<FwoMcpConnection> ConnectAsync(WebApplication app, params string[] roles)
    {
        string accessToken = McpTestServer.CreateMiddlewareToken(roles);
        return ConnectAsync(app, _ => ValueTask.FromResult(accessToken));
    }

    /// <summary>
    /// Connects to the MCP server of the test server, asking the given provider for the token.
    /// </summary>
    internal static Task<FwoMcpConnection> ConnectAsync(WebApplication app, Func<CancellationToken, ValueTask<string>> accessTokenProvider)
    {
        McpConnectionSettings settings = new() { Endpoint = kTestMcpEndpoint, AccessTokenProvider = accessTokenProvider };
        return FwoMcpConnection.ConnectAsync(settings, app.GetTestServer().CreateHandler());
    }

    /// <summary>
    /// Creates a factory with the given provider client and the context window of the tests, whose user agents call
    /// the MCP server of the given test server.
    /// </summary>
    /// <param name="mcpApp">Test server running the MCP server pipeline</param>
    /// <param name="providerClient">Chat client standing in for the AI provider</param>
    /// <param name="agentSettings">Settings of the agents; the defaults with all AI functions switched on if null</param>
    /// <param name="loggerFactory">Logger factory of the agents; nothing is logged if null</param>
    internal static FwoAgentFactory CreateFactory(WebApplication mcpApp, IChatClient providerClient, AgentSettings? agentSettings = null,
        ILoggerFactory? loggerFactory = null)
    {
        return new FwoAgentFactory(providerClient, kContextWindowTokens, agentSettings ?? CreateAgentSettingsWithAllFeatures(),
            loggerFactory, () => mcpApp.GetTestServer().CreateHandler());
    }

    /// <summary>
    /// Creates the default agent settings with all AI functions switched on, which are off by default.
    /// </summary>
    internal static AgentSettings CreateAgentSettingsWithAllFeatures()
    {
        return new AgentSettings { ChatEnabled = true, ReasonCheckEnabled = true };
    }

    /// <summary>
    /// Creates the agent of the default test user holding the given roles, in its token and for the role filters of
    /// the AI functions.
    /// </summary>
    internal static FwoUserAgent CreateUserAgent(FwoAgentFactory factory, params string[] roles)
    {
        string accessToken = McpTestServer.CreateMiddlewareToken(roles);
        return CreateUserAgent(factory, _ => ValueTask.FromResult(accessToken), roles);
    }

    /// <summary>
    /// Creates the agent of a user, asking the given provider for the token; the user holds no roles for the role
    /// filters of the AI functions unless given.
    /// </summary>
    internal static FwoUserAgent CreateUserAgent(FwoAgentFactory factory, Func<CancellationToken, ValueTask<string>> accessTokenProvider,
        params string[] userRoles)
    {
        return factory.CreateUserAgent(CreateMcpSettings(accessTokenProvider), userRoles);
    }

    /// <summary>
    /// Creates the connection settings of the MCP server of the test server, asking the given provider for the token.
    /// </summary>
    internal static McpConnectionSettings CreateMcpSettings(Func<CancellationToken, ValueTask<string>> accessTokenProvider)
    {
        return new McpConnectionSettings { Endpoint = kTestMcpEndpoint, AccessTokenProvider = accessTokenProvider };
    }

    /// <summary>
    /// Creates the run options of an AG-UI run carrying the AG-UI input, the way the AG-UI hosting passes it.
    /// </summary>
    internal static ChatClientAgentRunOptions CreateAguiRunOptions(RunAgentInput aguiInput)
    {
        ChatRequestContext requestContext = aguiInput.ToChatRequestContext(JsonSerializerOptions.Web);
        return new ChatClientAgentRunOptions { ChatOptions = requestContext.ChatOptions };
    }

    /// <summary>
    /// Returns the messages and run options of an AG-UI run, the way the AG-UI hosting passes them to the agent.
    /// </summary>
    internal static (List<ChatMessage> Messages, ChatClientAgentRunOptions Options) ToAgentRun(RunAgentInput aguiInput)
    {
        ChatRequestContext requestContext = aguiInput.ToChatRequestContext(JsonSerializerOptions.Web);
        return (requestContext.Messages, new ChatClientAgentRunOptions { ChatOptions = requestContext.ChatOptions });
    }

    /// <summary>
    /// Creates the run options of an AG-UI run naming the given prompt with the given arguments (none if null).
    /// </summary>
    internal static ChatClientAgentRunOptions CreatePromptRunOptions(string promptName, Dictionary<string, string>? arguments = null)
    {
        return CreateAguiRunOptions(new RunAgentInput { ForwardedProperties = CreatePromptProperties(promptName, arguments) });
    }

    /// <summary>
    /// Creates the forwarded properties of an AG-UI input naming the given prompt with the given arguments (none if null).
    /// </summary>
    internal static JsonElement CreatePromptProperties(string promptName, Dictionary<string, string>? arguments = null)
    {
        JsonObject prompt = new() { [PromptRequest.kNameProperty] = promptName };
        if (arguments != null)
        {
            JsonObject argumentObject = [];
            foreach (KeyValuePair<string, string> argument in arguments)
            {
                argumentObject[argument.Key] = argument.Value;
            }
            prompt[PromptRequest.kArgumentsProperty] = argumentObject;
        }
        JsonObject forwardedProperties = new() { [PromptRequest.kPromptProperty] = prompt };
        return JsonSerializer.SerializeToElement(forwardedProperties);
    }

    /// <summary>
    /// Runs a streaming turn of the agent with the given messages and returns its updates.
    /// </summary>
    internal static async Task<List<AgentResponseUpdate>> RunStreamingAsync(AIAgent agent, IEnumerable<ChatMessage> messages,
        AgentSession? session = null, AgentRunOptions? options = null)
    {
        List<AgentResponseUpdate> updates = [];
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session, options))
        {
            updates.Add(update);
        }
        return updates;
    }

    /// <summary>
    /// Runs a streaming turn of the agent with the given user text and returns its updates.
    /// </summary>
    internal static Task<List<AgentResponseUpdate>> RunStreamingAsync(AIAgent agent, string userText)
    {
        List<ChatMessage> messages = [new ChatMessage(ChatRole.User, userText)];
        return RunStreamingAsync(agent, messages);
    }

    /// <summary>
    /// Tells whether an update of a run holds the snapshot of the conversation (AG-UI MESSAGES_SNAPSHOT).
    /// </summary>
    internal static bool IsSnapshot(AgentResponseUpdate update)
    {
        return update.AsChatResponseUpdate().RawRepresentation is MessagesSnapshotEvent;
    }

    /// <summary>
    /// Returns the conversation of the snapshot the updates of a run end with, as the client reads it.
    /// </summary>
    internal static List<ChatMessage> GetSnapshot(IEnumerable<AgentResponseUpdate> updates)
    {
        MessagesSnapshotEvent snapshot = (MessagesSnapshotEvent)updates.Last().AsChatResponseUpdate().RawRepresentation!;
        return snapshot.Messages.AsChatMessages().ToList();
    }

    /// <summary>
    /// Returns the texts of the messages.
    /// </summary>
    internal static List<string> GetTexts(IEnumerable<ChatMessage> messages)
    {
        return messages.Select(message => message.Text).ToList();
    }

    /// <summary>
    /// Returns the contents of the given type in the messages, in order.
    /// </summary>
    internal static List<TContent> GetContents<TContent>(IEnumerable<ChatMessage> messages) where TContent : AIContent
    {
        return messages.SelectMany(message => message.Contents).OfType<TContent>().ToList();
    }

    /// <summary>
    /// Returns the call ids of the tool calls in the messages, in order.
    /// </summary>
    internal static List<string> GetCallIds(IEnumerable<ChatMessage> messages)
    {
        return GetContents<FunctionCallContent>(messages).Select(call => call.CallId).ToList();
    }

    /// <summary>
    /// Returns the call ids of the tool results in the messages, in order.
    /// </summary>
    internal static List<string> GetResultCallIds(IEnumerable<ChatMessage> messages)
    {
        return GetContents<FunctionResultContent>(messages).Select(result => result.CallId).ToList();
    }

    /// <summary>
    /// Tells whether a request offers tools to the model.
    /// </summary>
    internal static bool OffersTools(ChatOptions? options)
    {
        return options?.Tools is { Count: > 0 };
    }

    /// <summary>
    /// Returns the names of the tools a request offers to the model.
    /// </summary>
    internal static List<string> GetToolNames(ChatOptions? options)
    {
        return options?.Tools?.Select(tool => tool.Name).ToList() ?? [];
    }
}
