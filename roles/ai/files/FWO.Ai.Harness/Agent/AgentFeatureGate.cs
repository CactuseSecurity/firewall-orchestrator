using FWO.Data.Ai;

namespace FWO.Ai.Harness.Agent;

/// <summary>
/// AI functions offered to the users, each switched on or off and limited to roles in the AI settings of the UI.
/// </summary>
public enum AgentFeature
{
    /// <summary>Chat: texts of the user, answers to tool approval requests and MCP prompts without a switch of their own.</summary>
    Chat,

    /// <summary>Check of the functional reason of a modelled connection, the MCP prompt of the reason check.</summary>
    ReasonCheck
}

/// <summary>
/// Refuses the turns of AI functions that are switched off or not allowed for the roles of the user, before the turn
/// reaches the MCP server or the AI provider. A turn naming the MCP prompt of the reason check belongs to the reason
/// check; every other turn belongs to the chat: a new text of the user, the answers to tool approval requests (the reason
/// check never asks for any), and any other MCP prompt. A prompt without a switch of its own is thus refused while the
/// chat is off or not allowed for the user (fail closed). A turn of the reason check must start a new conversation and
/// is offered no tools, so that a user allowed the reason check but not the chat cannot use it as chat.
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
    /// Tells whether a user holding the given roles may use the AI function: the user holds one of the roles the
    /// settings allow for it, or the settings allow every user (no roles set), see <see cref="AiFunctionRoles"/>.
    /// </summary>
    /// <param name="feature">AI function</param>
    /// <param name="agentSettings">Settings of the agent</param>
    /// <param name="userRoles">Roles of the user, from the JWT of the request</param>
    internal static bool IsAllowedForRoles(AgentFeature feature, AgentSettings agentSettings, IReadOnlyCollection<string> userRoles)
    {
        ArgumentNullException.ThrowIfNull(agentSettings);
        ArgumentNullException.ThrowIfNull(userRoles);
        IReadOnlyList<string>? allowedRoles = feature switch
        {
            AgentFeature.Chat => agentSettings.ChatRoles,
            AgentFeature.ReasonCheck => agentSettings.ReasonCheckRoles,
            _ => null
        };
        return allowedRoles != null && AiFunctionRoles.IsAllowed(allowedRoles, userRoles);
    }

    /// <summary>
    /// Tells whether a turn of the AI function is offered the tools of the user. Only the chat is: the reason check
    /// assesses the data of its prompt only. An unknown function is offered none (fail closed).
    /// </summary>
    /// <param name="feature">AI function</param>
    internal static bool OffersTools(AgentFeature feature)
    {
        return feature == AgentFeature.Chat;
    }

    /// <summary>
    /// Refuses the turn if it is malformed for the AI function it belongs to, or if that function is switched off or
    /// not allowed for the roles of the user.
    /// </summary>
    /// <param name="turnInput">Input of the turn</param>
    /// <param name="agentSettings">Settings of the agent</param>
    /// <param name="userRoles">Roles of the user, from the JWT of the request</param>
    /// <returns>The AI function the turn belongs to</returns>
    /// <exception cref="ArgumentException">A turn of the reason check sends messages besides its prompt.</exception>
    /// <exception cref="AgentFeatureDisabledException">The AI function of the turn is switched off.</exception>
    /// <exception cref="AgentFeatureNotPermittedException">The user holds none of the roles allowed to use the AI
    /// function of the turn.</exception>
    internal static AgentFeature EnsureAllowed(TurnInput turnInput, AgentSettings agentSettings, IReadOnlyCollection<string> userRoles)
    {
        AgentFeature feature = GetFeature(turnInput);
        if (feature == AgentFeature.ReasonCheck)
        {
            EnsureNewConversation(turnInput);
        }
        if (!IsSwitchedOn(feature, agentSettings))
        {
            throw new AgentFeatureDisabledException(feature);
        }
        if (!IsAllowedForRoles(feature, agentSettings, userRoles))
        {
            throw new AgentFeatureNotPermittedException(feature);
        }
        return feature;
    }

    /// <summary>
    /// Requires a turn of the reason check to start a new conversation: it sends no messages besides its prompt.
    /// Otherwise the conversation sent along would reach the model in front of the prompt, and a user allowed the reason
    /// check but not the chat could hold a chat this way.
    /// </summary>
    /// <exception cref="ArgumentException">The turn sends messages besides its prompt.</exception>
    private static void EnsureNewConversation(TurnInput turnInput)
    {
        if (!turnInput.IsPromptOnly)
        {
            throw new ArgumentException("A run of the reason check starts a new conversation and sends no messages besides its prompt.",
                nameof(turnInput));
        }
    }
}
