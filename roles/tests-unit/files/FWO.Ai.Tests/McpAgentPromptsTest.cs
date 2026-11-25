using FWO.Ai.Harness.Agent;
using FWO.Ai.Mcp.Prompts;
using FWO.Basics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the prompt holding the instructions of the FWO agent, directly and via the MCP endpoint, and that the AI
/// harness asks for it by its name.
/// </summary>
[TestFixture]
internal class McpAgentPromptsTest
{
    /// <summary>
    /// Verifies that the prompt returns the instructions of the agent.
    /// </summary>
    [Test]
    public void GetAgentInstructions_ReturnsInstructions()
    {
        string instructions = AgentPrompts.GetAgentInstructions();

        Assert.That(instructions, Is.EqualTo(AgentPrompts.kAgentInstructions));
        Assert.That(instructions, Does.Contain("Firewall Orchestrator"));
    }

    /// <summary>
    /// Verifies that the harness asks for the prompt by the name the MCP server offers it under.
    /// </summary>
    [Test]
    public void PromptName_IsTheNameTheHarnessAsksFor()
    {
        Assert.That(AgentPrompts.kAgentInstructionsPromptName, Is.EqualTo(FwoUserAgent.kInstructionsPromptName));
    }

    /// <summary>
    /// Verifies that the MCP endpoint lists the prompt, without arguments.
    /// </summary>
    [Test]
    public async Task ListPrompts_ListsAgentInstructionsWithoutArguments()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        await using McpClient client = await McpTestServer.ConnectAsync(httpClient, Roles.Reporter);

        IList<McpClientPrompt> prompts = await client.ListPromptsAsync();

        McpClientPrompt prompt = prompts.Single(listed => listed.Name == AgentPrompts.kAgentInstructionsPromptName);
        Assert.That(prompt.ProtocolPrompt.Arguments, Is.Null.Or.Empty);
    }

    /// <summary>
    /// Verifies that the MCP endpoint returns the instructions as a single user message.
    /// </summary>
    [Test]
    public async Task GetPrompt_ReturnsInstructionsAsSingleUserMessage()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        await using McpClient client = await McpTestServer.ConnectAsync(httpClient, Roles.Reporter);

        GetPromptResult result = await client.GetPromptAsync(AgentPrompts.kAgentInstructionsPromptName);

        PromptMessage message = result.Messages.Single();
        Assert.That(message.Role, Is.EqualTo(Role.User));
        Assert.That(((TextContentBlock)message.Content).Text, Is.EqualTo(AgentPrompts.kAgentInstructions));
    }
}
