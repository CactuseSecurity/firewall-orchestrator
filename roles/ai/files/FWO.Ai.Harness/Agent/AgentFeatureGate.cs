namespace FWO.Ai.Harness.Agent;

/// <summary>
/// AI functions offered to the users, each switched on or off in the AI settings of the UI.
/// </summary>
public enum AgentFeature
{
    /// <summary>Chat: texts of the user, answers to tool approval requests and MCP prompts without a switch of their own.</summary>
    Chat,

    /// <summary>Check of the functional reason of a modelled connection, the MCP prompt of the reason check.</summary>
    ReasonCheck
}

/// <summary>
/// Refuses the turns of AI functions that are switched off, before the turn reaches the MCP server or the AI provider.
/// A turn naming the MCP prompt of the reason check belongs to the reason check; every other turn belongs to the chat:
/// a new text of the user, the answers to tool approval requests (the reason check never asks for any), and any other
/// MCP prompt. A prompt without a switch of its own is thus refused while the chat is off (fail closed).
/// </summary>
internal static class AgentFeatureGate
{
    /// <summary>
    /// Name of the MCP prompt of the reason check. The MCP server offers it (FWO.Ai.Mcp, RequestApplicationPrompts
    /// .CheckConnectionReason) and the UI runs it (FWO.Ai.Client, ConnectionReasonCheck.kPromptName), keep all in step.
    /// </summary>
    internal const string kReasonCheckPromptName = "check_connection_reason";

    /// <summary>
    /// Returns the AI function the turn belongs to.
    /// </summary>
    /// <param name="turnInput">Input of the turn</param>
    internal static AgentFeature GetFeature(TurnInput turnInput)
    {
        ArgumentNullException.ThrowIfNull(turnInput);
        return turnInput.PromptName == kReasonCheckPromptName ? AgentFeature.ReasonCheck : AgentFeature.Chat;
    }

    /// <summary>
    /// Tells whether the AI function is switched on in the given settings.
    /// </summary>
    /// <param name="feature">AI function</param>
    /// <param name="agentSettings">Settings of the agent</param>
    internal static bool IsSwitchedOn(AgentFeature feature, AgentSettings agentSettings)
    {
        ArgumentNullException.ThrowIfNull(agentSettings);
        return feature switch
        {
            AgentFeature.Chat => agentSettings.ChatEnabled,
            AgentFeature.ReasonCheck => agentSettings.ReasonCheckEnabled,
            _ => false
        };
    }

    /// <summary>
    /// Refuses the turn if the AI function it belongs to is switched off.
    /// </summary>
    /// <param name="turnInput">Input of the turn</param>
    /// <param name="agentSettings">Settings of the agent</param>
    /// <exception cref="AgentFeatureDisabledException">The AI function of the turn is switched off.</exception>
    internal static void EnsureSwitchedOn(TurnInput turnInput, AgentSettings agentSettings)
    {
        AgentFeature feature = GetFeature(turnInput);
        if (!IsSwitchedOn(feature, agentSettings))
        {
            throw new AgentFeatureDisabledException(feature);
        }
    }
}
