using FWO.Ai.Client;
using FWO.Ai.Client.ReasonChecks;
using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Hosting;
using FWO.Ai.Mcp.Prompts;
using FWO.Basics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using ModelContextProtocol.Client;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests that the constants the chat client of the UI (FWO.Ai.Client) shares with the harness and the MCP server are
/// in step: the AG-UI path, the forwarded prompt properties, and the name, arguments and limits of the reason check
/// prompt.
/// </summary>
[TestFixture]
internal class ClientConformanceTest
{
    private const string kPathSeparator = "/";

    /// <summary>
    /// Verifies that the MCP server offers the prompt of the connection reason check with every argument the client
    /// may send.
    /// </summary>
    [Test]
    public async Task ListPrompts_OffersConnectionReasonCheckWithEveryClientArgument()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        await using McpClient client = await McpTestServer.ConnectAsync(httpClient, Roles.Modeller);

        IList<McpClientPrompt> prompts = await client.ListPromptsAsync();

        McpClientPrompt prompt = prompts.Single(offeredPrompt => offeredPrompt.Name == ConnectionReasonCheck.kPromptName);
        List<string> argumentNames = prompt.ProtocolPrompt.Arguments?.Select(argument => argument.Name).ToList() ?? [];
        Assert.That(argumentNames, Is.SupersetOf(ConnectionReasonCheck.kArgumentNames));
    }

    /// <summary>
    /// Verifies that the harness switches the reason check of the client on and off by the prompt name the client sends,
    /// which the MCP server offers (see <see cref="ListPrompts_OffersConnectionReasonCheckWithEveryClientArgument"/>).
    /// </summary>
    [Test]
    public void ReasonCheckPromptName_MatchesFeatureGateOfHarness()
    {
        Assert.That(AgentFeatureGate.kReasonCheckPromptName, Is.EqualTo(ConnectionReasonCheck.kPromptName));
    }

    /// <summary>
    /// Verifies that the limits and the language pattern of the client are the ones of the MCP server.
    /// </summary>
    [Test]
    public void ReasonCheckLimits_MatchMcpServer()
    {
        Assert.That(ReasonCheckLimits.kMaxReasonLength, Is.EqualTo(RequestApplicationPrompts.kMaxReasonLength));
        Assert.That(ReasonCheckLimits.kMaxContextLength, Is.EqualTo(RequestApplicationPrompts.kMaxContextLength));
        Assert.That(ReasonCheckLimits.kMaxLanguageLength, Is.EqualTo(RequestApplicationPrompts.kMaxLanguageLength));
        Assert.That(ReasonCheckPatterns.kLanguagePattern, Is.EqualTo(RequestApplicationPrompts.kLanguagePattern));
    }

    /// <summary>
    /// Verifies that the kinds of connections the client sends are the ones the MCP server knows.
    /// </summary>
    [Test]
    public void ConnectionKinds_MatchMcpServer()
    {
        Assert.That(ConnectionReasonCheck.kKindConnection, Is.EqualTo(RequestApplicationPrompts.kKindConnection));
        Assert.That(ConnectionReasonCheck.kKindInterface, Is.EqualTo(RequestApplicationPrompts.kKindInterface));
        Assert.That(ConnectionReasonCheck.kKindInterfaceUsage, Is.EqualTo(RequestApplicationPrompts.kKindInterfaceUsage));
    }

    /// <summary>
    /// Verifies that the client posts to the AG-UI path of the harness and names the prompt the way the harness reads it.
    /// </summary>
    [Test]
    public void AgUiContract_MatchesHarness()
    {
        Assert.That(kPathSeparator + FwoAgentChatClientFactory.kAgUiPath, Is.EqualTo(HarnessServerSetup.kAgUiPath));
        Assert.That(AgentPromptRequest.kPromptProperty, Is.EqualTo(PromptRequest.kPromptProperty));
        Assert.That(AgentPromptRequest.kPromptNameProperty, Is.EqualTo(PromptRequest.kNameProperty));
        Assert.That(AgentPromptRequest.kPromptArgumentsProperty, Is.EqualTo(PromptRequest.kArgumentsProperty));
    }
}
