using FWO.Ai.Harness.Mcp;
using FWO.Ai.Mcp;
using FWO.Ai.Mcp.Prompts;
using FWO.Basics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the harness's connection to the MCP server pipeline running on a test server: tools by role, prompts,
/// and that the user's JWT reaches the tools.
/// </summary>
[TestFixture]
internal class HarnessMcpConnectionTest
{
    private const string kReasonWithMarkup = "Reads <b>customer data</b> for order checks.";
    private const string kEscapedReasonWithMarkup = "Reads &lt;b&gt;customer data&lt;/b&gt; for order checks.";
    private const string kUnknownPrompt = "unknown_prompt";

    private static readonly List<string> kAllTools = [HarnessTestSetup.kCurrentUserTool, HarnessTestSetup.kRoleRestrictedTool];
    private static readonly List<string> kUnrestrictedTools = [HarnessTestSetup.kCurrentUserTool];

    /// <summary>
    /// Verifies that plain http to a remote MCP server is refused before the token is asked for or any request is sent.
    /// </summary>
    [Test]
    public void ConnectAsync_WithRemoteHttpEndpoint_ThrowsBeforeAskingForToken()
    {
        bool tokenRequested = false;
        McpConnectionSettings settings = new()
        {
            Endpoint = new Uri("http://middleware.example.com/mcp"),
            AccessTokenProvider = _ =>
            {
                tokenRequested = true;
                return ValueTask.FromResult("token");
            }
        };

        Assert.ThrowsAsync<ArgumentException>(async () => await FwoMcpConnection.ConnectAsync(settings));
        Assert.That(tokenRequested, Is.False);
    }

    /// <summary>
    /// Verifies that missing settings are refused.
    /// </summary>
    [Test]
    public void ConnectAsync_WithoutSettings_Throws()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () => await FwoMcpConnection.ConnectAsync(null!));
    }

    /// <summary>
    /// Verifies that a user without a role offered by the MCP server cannot connect.
    /// </summary>
    [TestCase(Roles.Anonymous)]
    [TestCase(Roles.MiddlewareServer)]
    public async Task ConnectAsync_WithTechnicalOrAnonymousRole_Fails(string role)
    {
        await using WebApplication app = await McpTestServer.Start();

        Assert.CatchAsync<HttpRequestException>(async () => await HarnessTestSetup.ConnectAsync(app, role));
    }

    /// <summary>
    /// Verifies that the token provider is asked for the token of each request.
    /// </summary>
    [Test]
    public async Task ConnectAsync_AsksTokenProviderPerRequest()
    {
        await using WebApplication app = await McpTestServer.Start();
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);
        int tokenRequests = 0;
        await using FwoMcpConnection connection = await HarnessTestSetup.ConnectAsync(app, _ =>
        {
            Interlocked.Increment(ref tokenRequests);
            return ValueTask.FromResult(accessToken);
        });
        int tokenRequestsAfterConnect = tokenRequests;

        await connection.ListToolsAsync();

        Assert.That(tokenRequestsAfterConnect, Is.GreaterThan(0));
        Assert.That(tokenRequests, Is.GreaterThan(tokenRequestsAfterConnect));
    }

    /// <summary>
    /// Verifies that a user without the role of a restricted tool only gets the unrestricted tools.
    /// </summary>
    [Test]
    public async Task ListToolsAsync_AsReporter_HidesRestrictedTool()
    {
        await using WebApplication app = await McpTestServer.Start();
        await using FwoMcpConnection connection = await HarnessTestSetup.ConnectAsync(app, Roles.Reporter);

        List<AITool> tools = await connection.ListToolsAsync();

        Assert.That(tools.Select(tool => tool.Name), Is.EquivalentTo(kUnrestrictedTools));
    }

    /// <summary>
    /// Verifies that an admin gets all tools.
    /// </summary>
    [Test]
    public async Task ListToolsAsync_AsAdmin_ListsAllTools()
    {
        await using WebApplication app = await McpTestServer.Start();
        await using FwoMcpConnection connection = await HarnessTestSetup.ConnectAsync(app, Roles.Admin);

        List<AITool> tools = await connection.ListToolsAsync();

        Assert.That(tools.Select(tool => tool.Name), Is.EquivalentTo(kAllTools));
    }

    /// <summary>
    /// Verifies that invoking a listed tool calls the MCP server with the user's JWT.
    /// </summary>
    [Test]
    public async Task ListToolsAsync_InvokingCurrentUserTool_ReturnsUserOfToken()
    {
        await using WebApplication app = await McpTestServer.Start();
        await using FwoMcpConnection connection = await HarnessTestSetup.ConnectAsync(app, Roles.Reporter);
        List<AITool> tools = await connection.ListToolsAsync();
        AIFunction currentUserTool = tools.OfType<AIFunction>().Single(tool => tool.Name == HarnessTestSetup.kCurrentUserTool);

        object? result = await currentUserTool.InvokeAsync();

        string resultJson = JsonSerializer.Serialize(result);
        Assert.That(resultJson, Does.Contain(McpTestServer.kUserName));
        Assert.That(resultJson, Does.Contain(Roles.Reporter));
    }

    /// <summary>
    /// Verifies that a prompt is returned as a single user message embedding the escaped reason.
    /// </summary>
    [Test]
    public async Task GetPromptMessagesAsync_ReturnsUserMessageWithEscapedReason()
    {
        await using WebApplication app = await McpTestServer.Start();
        await using FwoMcpConnection connection = await HarnessTestSetup.ConnectAsync(app, Roles.Requester);

        List<ChatMessage> messages = await connection.GetPromptMessagesAsync(HarnessTestSetup.kAccessRequestPrompt,
            new Dictionary<string, object?> { [HarnessTestSetup.kReasonArgument] = kReasonWithMarkup });

        ChatMessage message = messages.Single();
        Assert.That(message.Role, Is.EqualTo(ChatRole.User));
        Assert.That(message.Text, Does.Contain(kEscapedReasonWithMarkup));
        Assert.That(message.Text, Does.Not.Contain(kReasonWithMarkup));
    }

    /// <summary>
    /// Verifies that a blank prompt name is refused without a request.
    /// </summary>
    [TestCase("")]
    [TestCase(" ")]
    public async Task GetPromptMessagesAsync_WithBlankName_Throws(string promptName)
    {
        await using WebApplication app = await McpTestServer.Start();
        await using FwoMcpConnection connection = await HarnessTestSetup.ConnectAsync(app, Roles.Requester);

        Assert.CatchAsync<ArgumentException>(async () => await connection.GetPromptMessagesAsync(promptName));
    }

    /// <summary>
    /// Verifies that an unknown prompt is refused by the MCP server.
    /// </summary>
    [Test]
    public async Task GetPromptMessagesAsync_WithUnknownPrompt_Throws()
    {
        await using WebApplication app = await McpTestServer.Start();
        await using FwoMcpConnection connection = await HarnessTestSetup.ConnectAsync(app, Roles.Requester);

        Assert.CatchAsync<McpException>(async () => await connection.GetPromptMessagesAsync(kUnknownPrompt));
    }

    /// <summary>
    /// Verifies that a reason exceeding the maximum length of the MCP server is refused.
    /// </summary>
    [Test]
    public async Task GetPromptMessagesAsync_WithTooLongReason_Throws()
    {
        await using WebApplication app = await McpTestServer.Start();
        await using FwoMcpConnection connection = await HarnessTestSetup.ConnectAsync(app, Roles.Requester);
        string reason = new('a', RequestApplicationPrompts.kMaxReasonLength + 1);

        Assert.CatchAsync<McpException>(async () => await connection.GetPromptMessagesAsync(HarnessTestSetup.kAccessRequestPrompt,
            new Dictionary<string, object?> { [HarnessTestSetup.kReasonArgument] = reason }));
    }

    /// <summary>
    /// Verifies that disposing the connection twice does no harm.
    /// </summary>
    [Test]
    public async Task DisposeAsync_Twice_DoesNotThrow()
    {
        await using WebApplication app = await McpTestServer.Start();
        FwoMcpConnection connection = await HarnessTestSetup.ConnectAsync(app, Roles.Reporter);

        await connection.DisposeAsync();

        Assert.DoesNotThrowAsync(async () => await connection.DisposeAsync());
    }

    /// <summary>
    /// Verifies that connecting through a real HTTP handler works, using the MCP server on a loopback port.
    /// </summary>
    [Test]
    public async Task ConnectAsync_WithoutInnerHandler_ConnectsOverLoopback()
    {
        await using WebApplication app = await McpTestServer.StartOnLoopback();
        string accessToken = McpTestServer.CreateMiddlewareToken(Roles.Reporter);
        McpConnectionSettings settings = new()
        {
            Endpoint = new Uri(McpTestServer.GetBaseAddress(app), McpServerSetup.kMcpPath),
            AccessTokenProvider = _ => ValueTask.FromResult(accessToken),
            ConnectionTimeout = TimeSpan.FromSeconds(30)
        };
        await using FwoMcpConnection connection = await FwoMcpConnection.ConnectAsync(settings);

        List<AITool> tools = await connection.ListToolsAsync();

        Assert.That(tools.Select(tool => tool.Name), Does.Contain(HarnessTestSetup.kCurrentUserTool));
    }
}
