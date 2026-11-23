using FWO.Ai.Harness.Mcp;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// Agent of one FWO user for one request, created by <see cref="FwoAgentFactory.CreateUserAgent"/>. Each run
/// connects to the MCP server with the user's JWT, gets the instructions from it, offers the model the tools this user
/// may call for this run only, and closes the connection when the run ends. The harness is stateless: a run takes the
/// conversation the client sends (see <see cref="TurnInput"/>), compacts its history once it exceeds the context
/// window, and ends with the conversation for the next run (see <see cref="ConversationSnapshot"/>). A tool not marked read-only
/// runs only once its <see cref="ToolApprovalRequestContent"/> has been answered. A turn of an AI function that is switched
/// off or not allowed for the roles of the user is refused before it reaches the MCP server or the AI provider (see
/// <see cref="AgentFeatureGate"/>).
/// </summary>
public sealed class FwoUserAgent : DelegatingAIAgent
{
    /// <summary>
    /// Name of the MCP prompt holding the instructions (system prompt) of the agent. The MCP server offers it
    /// (FWO.Ai.Mcp, AgentPrompts.kAgentInstructionsPromptName), keep both in step.
    /// </summary>
    internal const string kInstructionsPromptName = "fwo_agent_instructions";

    private readonly IChatReducer historyReducer;
    private readonly AgentSettings agentSettings;
    private readonly IReadOnlyCollection<string> userRoles;
    private readonly McpConnectionSettings userConnection;
    private readonly Func<HttpMessageHandler>? mcpHandlerFactory;
    private readonly ILoggerFactory? loggerFactory;
    private readonly ILogger logger;

    /// <summary>
    /// Creates the user agent, see <see cref="FwoAgentFactory.CreateUserAgent"/>.
    /// </summary>
    /// <param name="sharedAgent">Agent shared by all users, running the turns; it keeps no history</param>
    /// <param name="historyReducer">Compacts the history of a run, see <see cref="HistoryCompaction"/></param>
    /// <param name="agentSettings">Settings of the factory, deciding which AI functions the user may run; not changed later</param>
    /// <param name="userRoles">Roles of the user, from the JWT, deciding whether the user may run an AI function</param>
    /// <param name="userConnection">MCP endpoint and the provider of the user's JWT</param>
    /// <param name="mcpHandlerFactory">Creates the handler sending the MCP requests of a run, e.g. of a test server;
    /// a new <see cref="SocketsHttpHandler"/> per run if null</param>
    /// <param name="loggerFactory">Logger factory of the MCP clients and of failed runs</param>
    internal FwoUserAgent(AIAgent sharedAgent, IChatReducer historyReducer, AgentSettings agentSettings, IReadOnlyCollection<string> userRoles,
        McpConnectionSettings userConnection, Func<HttpMessageHandler>? mcpHandlerFactory, ILoggerFactory? loggerFactory)
        : base(sharedAgent)
    {
        this.historyReducer = historyReducer;
        this.agentSettings = agentSettings;
        this.userRoles = userRoles;
        this.userConnection = userConnection;
        this.mcpHandlerFactory = mcpHandlerFactory;
        this.loggerFactory = loggerFactory;
        logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<FwoUserAgent>();
    }

    /// <summary>
    /// Runs a turn with the tools of the user; see <see cref="RunCoreStreamingAsync"/>.
    /// </summary>
    protected override Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null,
        AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        return RunCoreStreamingAsync(messages, session, options, cancellationToken).ToAgentResponseAsync(cancellationToken);
    }

    /// <summary>
    /// Runs a streaming turn with the tools of the user. The run options are not passed on: the agent only takes the
    /// conversation and the prompt from them and offers the user's tools, so neither the AG-UI input nor options of the
    /// client reach the AI provider. The last update holds the snapshot of the conversation. A failed run is logged, as
    /// the AG-UI hosting reports it to the client without details.
    /// </summary>
    /// <exception cref="ArgumentException">The run has no valid new user input, see <see cref="TurnInput.Read"/>.</exception>
    /// <exception cref="NotSupportedException">The AG-UI input declares client tools.</exception>
    /// <exception cref="AgentFeatureDisabledException">The AI function of the run is switched off.</exception>
    /// <exception cref="AgentFeatureNotPermittedException">The AI function of the run is not allowed for the roles of the user.</exception>
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages,
        AgentSession? session = null, AgentRunOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerable<AgentResponseUpdate> updates = RunTurnAsync(messages, session, options, cancellationToken);
        await using IAsyncEnumerator<AgentResponseUpdate> enumerator = updates.GetAsyncEnumerator(cancellationToken);
        while (await MoveNextLoggingFailureAsync(enumerator))
        {
            yield return enumerator.Current;
        }
    }

    /// <summary>
    /// Reads the turn input, refuses it if its AI function is switched off or not allowed for the roles of the user,
    /// connects to the MCP server, loads the user's tools and the instructions, compacts the history if needed, runs the
    /// turn on the shared agent with the history and the new input, and returns the conversation for the next run.
    /// </summary>
    private async IAsyncEnumerable<AgentResponseUpdate> RunTurnAsync(IEnumerable<ChatMessage> messages, AgentSession? session,
        AgentRunOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        TurnInput turnInput = TurnInput.Read(messages, options);
        AgentFeatureGate.EnsureAllowed(turnInput, agentSettings, userRoles);
        await using FwoMcpConnection mcpConnection = await FwoMcpConnection.ConnectAsync(userConnection, mcpHandlerFactory?.Invoke(),
            loggerFactory, cancellationToken);
        List<ChatMessage> newMessages = await turnInput.GetNewMessagesAsync(mcpConnection, cancellationToken);
        List<ChatMessage> runMessages = (await historyReducer.ReduceAsync(turnInput.History, cancellationToken)).ToList();
        runMessages.AddRange(newMessages);
        List<AITool> mcpTools = await mcpConnection.ListToolsAsync(cancellationToken);
        List<ChatMessage> instructionMessages = await mcpConnection.GetPromptMessagesAsync(kInstructionsPromptName, cancellationToken: cancellationToken);
        ChatClientAgentRunOptions runOptions = CreateRunOptions(ToolApproval.RequireApprovalUnlessReadOnly(mcpTools),
            JoinInstructions(instructionMessages));
        List<AgentResponseUpdate> responseUpdates = new();
        await foreach (AgentResponseUpdate update in InnerAgent.RunStreamingAsync(runMessages, session, runOptions, cancellationToken))
        {
            responseUpdates.Add(update);
            yield return update;
        }
        yield return ConversationSnapshot.Create(runMessages, responseUpdates);
    }

    /// <summary>
    /// Moves to the next update and logs a failure of the run; a cancellation is not logged, a run of an AI function that
    /// is switched off or not allowed for the user only as warning, as it is a refusal, not an error of the harness.
    /// </summary>
    private async Task<bool> MoveNextLoggingFailureAsync(IAsyncEnumerator<AgentResponseUpdate> enumerator)
    {
        try
        {
            return await enumerator.MoveNextAsync();
        }
        catch (Exception exception) when (exception is AgentFeatureDisabledException or AgentFeatureNotPermittedException)
        {
            logger.LogWarning("Refused a run of the FWO agent: {Reason}", exception.Message);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "A run of the FWO agent failed.");
            throw;
        }
    }

    /// <summary>
    /// Returns the text of the messages of the instructions prompt, one message per line; null if it is blank, so
    /// that no empty instructions are sent.
    /// </summary>
    internal static string? JoinInstructions(List<ChatMessage> instructionMessages)
    {
        string instructions = string.Join(Environment.NewLine, instructionMessages.Select(message => message.Text));
        return string.IsNullOrWhiteSpace(instructions) ? null : instructions;
    }

    /// <summary>
    /// Creates the options of a run with the given instructions, offering the given tools if there are any. The
    /// instructions are sent with every request of the run and are not part of the conversation.
    /// </summary>
    private static ChatClientAgentRunOptions CreateRunOptions(List<AITool> tools, string? instructions)
    {
        ChatOptions chatOptions = new() { Instructions = instructions };
        if (tools.Count > 0)
        {
            chatOptions.Tools = tools;
            chatOptions.ToolMode = ChatToolMode.Auto;
        }
        return new ChatClientAgentRunOptions { ChatOptions = chatOptions };
    }
}
