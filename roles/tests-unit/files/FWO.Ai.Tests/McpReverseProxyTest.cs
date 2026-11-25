using FWO.Ai.Mcp;
using FWO.Basics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using NUnit.Framework;
using System.Net;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests how the MCP server sits behind the middleware's Apache reverse proxy: the endpoint path Apache publishes.
/// The forwarded headers it trusts are tested with the shared options (FWO.Test, ReverseProxyForwardingOptionsTest).
/// </summary>
[TestFixture]
internal class McpReverseProxyTest
{
    private const string kRootPath = "/";

    /// <summary>
    /// Verifies that the MCP endpoint is served at the path the Apache vhost publishes (positive control for the test below).
    /// </summary>
    [Test]
    public async Task McpPath_ServesTheMcpEndpoint()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();

        using HttpResponseMessage response = await McpTestServer.PostTo(httpClient, McpServerSetup.kMcpPath,
            McpTestServer.CreateMiddlewareToken(Roles.Reporter), McpTestServer.kToolsListRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// Verifies that the root path is not the MCP endpoint, so only the path Apache publishes reaches it.
    /// </summary>
    [Test]
    public async Task RootPath_IsNotTheMcpEndpoint()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();

        using HttpResponseMessage response = await McpTestServer.PostTo(httpClient, kRootPath,
            McpTestServer.CreateMiddlewareToken(Roles.Reporter), McpTestServer.kToolsListRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
