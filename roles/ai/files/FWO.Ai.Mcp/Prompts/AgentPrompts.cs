using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FWO.Ai.Mcp.Prompts;

/// <summary>
/// MCP prompt holding the instructions (system prompt) of an AI agent that supports FWO users with the tools of this
/// server. The AI harness (FWO.Ai.Harness) gets them with the JWT of the user for each run and sends them to its AI
/// provider with every request.
/// </summary>
[Authorize]
internal class AgentPrompts
{
    /// <summary>
    /// Name of the prompt. The AI harness gets its instructions by this name (FwoUserAgent.kInstructionsPromptName),
    /// keep both in step.
    /// </summary>
    internal const string kAgentInstructionsPromptName = "fwo_agent_instructions";

    /// <summary>Instructions of the agent.</summary>
    internal const string kAgentInstructions = """
        You support the users of Firewall Orchestrator (FWO). Use the tools to look up FWO data instead of guessing,
        and keep names, UIDs, IP addresses, networks and ports exactly as given.
        """;

    /// <summary>
    /// Returns the instructions of an agent supporting FWO users.
    /// </summary>
    [McpServerPrompt(Name = kAgentInstructionsPromptName, Title = "FWO agent instructions")]
    [Description("Instructions (system prompt) for an AI agent that supports FWO users with the tools of this server.")]
    public static string GetAgentInstructions()
    {
        return kAgentInstructions;
    }
}
