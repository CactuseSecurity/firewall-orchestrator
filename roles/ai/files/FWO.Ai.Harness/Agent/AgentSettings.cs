using FWO.Data.Ai;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// Settings of the agents created by an <see cref="FwoAgentFactory"/>: agent loop, history compaction and the AI
/// functions the users may run, taken from the AI settings of the UI (see <see cref="Settings.AiRuntimeSettings"/>).
/// </summary>
public class AgentSettings
{
    /// <summary>
    /// Default maximum number of model requests offering the tools per turn, see
    /// <see cref="AiSettings.kDefaultMaxIterationsPerRequest"/>.
    /// </summary>
    public const int kDefaultMaxIterationsPerRequest = AiSettings.kDefaultMaxIterationsPerRequest;

    /// <summary>Default maximum number of consecutive failed tool call rounds before a turn fails.</summary>
    public const int kDefaultMaxConsecutiveErrorsPerRequest = AiSettings.kDefaultMaxConsecutiveErrorsPerRequest;

    /// <summary>
    /// The default compaction reserve is the context window divided by this, i.e. a quarter of the context window.
    /// </summary>
    public const int kDefaultCompactionReserveDivisor = AiSettings.kDefaultCompactionReserveDivisor;

    /// <summary>
    /// Maximum number of model requests offering the tools per turn, the initial request included; must be at least 1.
    /// If the model still calls tools in the last of them, it is asked once more without tools for its answer.
    /// </summary>
    public int MaxIterationsPerRequest { get; set; } = kDefaultMaxIterationsPerRequest;

    /// <summary>Maximum number of consecutive failed tool call rounds before a turn fails; must not be negative.</summary>
    public int MaxConsecutiveErrorsPerRequest { get; set; } = kDefaultMaxConsecutiveErrorsPerRequest;

    /// <summary>
    /// Tokens of the context window kept free of the conversation: the older part of the conversation is summarized
    /// before a turn once it exceeds the context window minus this reserve. The reserve has to hold the instructions,
    /// the tool definitions, the new messages of the turn, the tool results of the agent loop and the answer.
    /// Must be positive and smaller than the context window, and larger than the maximum number of output tokens
    /// if that is set; a quarter of the context window if null.
    /// </summary>
    public int? CompactionReserveTokens { get; set; }

    /// <summary>
    /// Whether the users may chat with the agent: send texts of their own, answer tool approval requests and run MCP
    /// prompts other than the reason check. Off by default, so that a run is refused unless the function is switched on
    /// (see <see cref="AgentFeatureGate"/>).
    /// </summary>
    public bool ChatEnabled { get; set; }

    /// <summary>
    /// Whether the users may run the reason check, the MCP prompt <see cref="AgentFeatureGate.kReasonCheckPromptName"/>.
    /// Off by default.
    /// </summary>
    public bool ReasonCheckEnabled { get; set; }

    /// <summary>
    /// Returns the compaction reserve for the given context window, see <see cref="CompactionReserveTokens"/>.
    /// </summary>
    /// <param name="contextWindowTokens">Context window of the model in tokens</param>
    internal int GetCompactionReserveTokens(int contextWindowTokens)
    {
        return CompactionReserveTokens ?? contextWindowTokens / kDefaultCompactionReserveDivisor;
    }

    /// <summary>
    /// Copies the settings, so that changes made after a factory was created do not affect it.
    /// </summary>
    internal AgentSettings CreateSnapshot()
    {
        return (AgentSettings)MemberwiseClone();
    }
}
