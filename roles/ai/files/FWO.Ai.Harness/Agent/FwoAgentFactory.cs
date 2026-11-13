using FWO.Ai.Harness.Mcp;
using FWO.Ai.Harness.Providers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// Creates the agents of the FWO users on Microsoft Agent Framework. All users share one <see cref="ChatClientAgent"/>
/// that runs the agent loop, keeps no history (the harness is stateless) and offers no tools itself. For each request
/// the harness creates an <see cref="FwoUserAgent"/>, which calls the tools of the FWO MCP server with the JWT of the
/// calling user. One factory serves all users of the harness server and is thread-safe.
/// </summary>
public sealed class FwoAgentFactory : IDisposable
{
    /// <summary>
    /// Name of the shared agent and of every user agent.
    /// </summary>
    public const string kAgentName = "fwo-agent";

    private const int kMinIterationsPerRequest = 1;

    private readonly IChatClient agentLoopClient;
    private readonly IChatReducer historyReducer;
    private readonly ChatClientAgent sharedAgent;
    private readonly AgentSettings agentSettings;
    private readonly ILoggerFactory? loggerFactory;
    private readonly Func<HttpMessageHandler>? mcpHandlerFactory;
    private bool disposed;

    /// <summary>
    /// Creates the factory with the chat client of the configured AI provider.
    /// </summary>
    /// <param name="providerSettings">AI provider, model, credentials and context window</param>
    /// <param name="agentSettings">Agent loop and compaction; copied, later changes do not apply</param>
    /// <param name="loggerFactory">Logger factory of the agents, the agent loop and the MCP clients</param>
    /// <exception cref="ArgumentException">Settings are incomplete or invalid.</exception>
    public FwoAgentFactory(AiProviderSettings providerSettings, AgentSettings agentSettings, ILoggerFactory? loggerFactory = null)
        : this(CreateProviderClient(providerSettings, agentSettings), providerSettings.ContextWindowTokens, agentSettings, loggerFactory)
    {
    }

    /// <summary>
    /// Creates the factory with the given chat client, e.g. a scripted client of a test. The factory takes ownership
    /// of the client: it is disposed with the factory, or right away if creating the factory fails.
    /// </summary>
    /// <param name="providerClient">Chat client of the AI provider, without function invocation</param>
    /// <param name="contextWindowTokens">Context window of the model in tokens</param>
    /// <param name="agentSettings">Agent loop and compaction; copied, later changes do not apply</param>
    /// <param name="loggerFactory">Logger factory of the agents, the agent loop and the MCP clients</param>
    /// <param name="mcpHandlerFactory">Creates the handler sending the MCP requests of a run, e.g. of a test server;
    /// a new <see cref="SocketsHttpHandler"/> per run if null</param>
    /// <exception cref="ArgumentException">Settings are incomplete or invalid.</exception>
    public FwoAgentFactory(IChatClient providerClient, int contextWindowTokens, AgentSettings agentSettings, ILoggerFactory? loggerFactory = null,
        Func<HttpMessageHandler>? mcpHandlerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(providerClient);
        try
        {
            int reserveTokens = ValidateAgentSettings(agentSettings, contextWindowTokens);
            AgentSettings settings = agentSettings.CreateSnapshot();
            this.agentSettings = settings;
            this.loggerFactory = loggerFactory;
            this.mcpHandlerFactory = mcpHandlerFactory;
            historyReducer = HistoryCompaction.CreateReducer(providerClient, contextWindowTokens, reserveTokens);
            agentLoopClient = CreateAgentLoopClient(providerClient, settings, loggerFactory);
            sharedAgent = new ChatClientAgent(agentLoopClient, CreateAgentOptions(), loggerFactory);
        }
        catch
        {
            providerClient.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates the agent running the turns of a user with the user's JWT. It connects to the MCP server only when a
    /// turn runs, so creating it per request is cheap. It refuses the turns of AI functions that are switched off in the
    /// agent settings of the factory, see <see cref="AgentFeatureGate"/>.
    /// </summary>
    /// <param name="userConnection">MCP endpoint and the provider of the user's JWT</param>
    /// <exception cref="ObjectDisposedException">The factory has been disposed.</exception>
    public FwoUserAgent CreateUserAgent(McpConnectionSettings userConnection)
    {
        ArgumentNullException.ThrowIfNull(userConnection);
        ObjectDisposedException.ThrowIf(disposed, this);
        return new FwoUserAgent(sharedAgent, historyReducer, agentSettings, userConnection, mcpHandlerFactory, loggerFactory);
    }

    /// <summary>
    /// Disposes the chat client shared by the agents, and with it the provider client.
    /// </summary>
    public void Dispose()
    {
        bool alreadyDisposed = Interlocked.Exchange(ref disposed, true);
        if (alreadyDisposed)
        {
            return;
        }
        // the agent loop client delegates to the provider client and disposes it
        agentLoopClient.Dispose();
    }

    /// <summary>
    /// Checks the settings and creates the provider client. Checks what the client needs before creating it:
    /// the agent settings, and that the compaction reserve leaves room for a full answer.
    /// </summary>
    /// <exception cref="ArgumentException">Settings are incomplete or invalid.</exception>
    private static IChatClient CreateProviderClient(AiProviderSettings providerSettings, AgentSettings agentSettings)
    {
        ChatClientFactory.Validate(providerSettings);
        int reserveTokens = ValidateAgentSettings(agentSettings, providerSettings.ContextWindowTokens);
        if (providerSettings.MaxOutputTokens is int maxOutputTokens && reserveTokens <= maxOutputTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(agentSettings), "The compaction reserve must be larger than the maximum number of output tokens.");
        }
        return ChatClientFactory.Create(providerSettings);
    }

    /// <summary>
    /// Checks the agent settings and returns the compaction reserve.
    /// </summary>
    /// <exception cref="ArgumentException">A setting is missing or out of range.</exception>
    private static int ValidateAgentSettings(AgentSettings agentSettings, int contextWindowTokens)
    {
        ArgumentNullException.ThrowIfNull(agentSettings);
        ArgumentOutOfRangeException.ThrowIfLessThan(agentSettings.MaxIterationsPerRequest, kMinIterationsPerRequest);
        ArgumentOutOfRangeException.ThrowIfNegative(agentSettings.MaxConsecutiveErrorsPerRequest);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contextWindowTokens);
        int reserveTokens = agentSettings.GetCompactionReserveTokens(contextWindowTokens);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reserveTokens);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(reserveTokens, contextWindowTokens);
        return reserveTokens;
    }

    /// <summary>
    /// Creates the agent loop: the model is called again with the tool results until it answers without tool calls,
    /// bounded by the maximum number of iterations. Exception details of failed tools are not passed to the model.
    /// The agent detects this function invocation and uses it instead of adding its own.
    /// </summary>
    private static IChatClient CreateAgentLoopClient(IChatClient providerClient, AgentSettings agentSettings, ILoggerFactory? loggerFactory)
    {
        return providerClient.AsBuilder()
            .UseFunctionInvocation(loggerFactory, functionInvocation =>
            {
                functionInvocation.MaximumIterationsPerRequest = agentSettings.MaxIterationsPerRequest;
                functionInvocation.MaximumConsecutiveErrorsPerRequest = agentSettings.MaxConsecutiveErrorsPerRequest;
                functionInvocation.IncludeDetailedErrors = false;
                functionInvocation.AllowConcurrentInvocation = false;
            })
            .Build();
    }

    /// <summary>
    /// Creates the history of the shared agent, which stores nothing: the harness is stateless and every run sends the
    /// whole conversation, so a session used for more than one run must not add its history to the next run.
    /// </summary>
    private static InMemoryChatHistoryProvider CreateStatelessHistoryProvider()
    {
        InMemoryChatHistoryProviderOptions historyOptions = new()
        {
            StorageInputRequestMessageFilter = _ => Enumerable.Empty<ChatMessage>(),
            StorageInputResponseMessageFilter = _ => Enumerable.Empty<ChatMessage>()
        };
        return new InMemoryChatHistoryProvider(historyOptions);
    }

    /// <summary>
    /// Creates the options of the shared agent: its name and a history that stores nothing. The instructions and the
    /// tools are set per run, see <see cref="FwoUserAgent"/>.
    /// </summary>
    /// <remarks>
    /// The binding of approval answers to the approval requests recorded in the session is disabled: the harness keeps
    /// no session between runs, so the binding would drop every answer. The harness trusts the answers of the client
    /// instead, an approved call runs as the answer names it, with the tools and the JWT of the user. The bypass of
    /// read-only tools stays: in a response that also calls a tool requiring approval, only that tool is asked for,
    /// and the read-only calls of that response are dropped, as their stored approval does not outlive the run; the
    /// model may call them again.
    /// </remarks>
    internal static ChatClientAgentOptions CreateAgentOptions()
    {
        return new ChatClientAgentOptions
        {
            Name = kAgentName,
            ChatHistoryProvider = CreateStatelessHistoryProvider(),
            DisableApprovalResponseBinding = true
        };
    }
}
