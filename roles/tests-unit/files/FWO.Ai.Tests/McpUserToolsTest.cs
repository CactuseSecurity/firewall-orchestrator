using FWO.Ai.Mcp.Tools;
using FWO.Basics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using ModelContextProtocol.Client;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the MCP user tools outside of an authenticated request, and the read-only annotation of the FWO tools;
/// the authenticated case is covered by <see cref="McpServerAuthenticationTest"/>.
/// </summary>
[TestFixture]
internal class McpUserToolsTest
{
    /// <summary>
    /// Verifies that an empty user is returned when there is no current request.
    /// </summary>
    [Test]
    public void GetCurrentUser_WithoutHttpContext_ReturnsEmptyUser()
    {
        CurrentUser currentUser = new UserTools(new HttpContextAccessor()).GetCurrentUser();

        Assert.That(currentUser.Name, Is.Empty);
        Assert.That(currentUser.Roles, Is.Empty);
    }

    /// <summary>
    /// Verifies that an empty user is returned for a request without authenticated user.
    /// </summary>
    [Test]
    public void GetCurrentUser_WithAnonymousUser_ReturnsEmptyUser()
    {
        HttpContextAccessor httpContextAccessor = new() { HttpContext = new DefaultHttpContext() };

        CurrentUser currentUser = new UserTools(httpContextAccessor).GetCurrentUser();

        Assert.That(currentUser.Name, Is.Empty);
        Assert.That(currentUser.Roles, Is.Empty);
    }

    /// <summary>
    /// Verifies that the FWO tools are announced as read-only, as the harness calls only read-only tools without
    /// asking the user for approval.
    /// </summary>
    [TestCase(HarnessTestSetup.kCurrentUserTool)]
    public async Task ListTools_AnnouncesToolAsReadOnly(string toolName)
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        await using McpClient client = await McpTestServer.ConnectAsync(httpClient, Roles.Admin);

        IList<McpClientTool> tools = await client.ListToolsAsync();

        McpClientTool tool = tools.Single(listedTool => listedTool.Name == toolName);
        Assert.That(tool.ProtocolTool.Annotations?.ReadOnlyHint, Is.True);
    }
}
