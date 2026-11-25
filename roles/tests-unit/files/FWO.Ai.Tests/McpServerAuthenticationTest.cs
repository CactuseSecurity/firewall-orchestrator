using FWO.Basics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NUnit.Framework;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using JwtConstants = FWO.Basics.JwtConstants;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests that the MCP server only accepts JWTs issued by the middleware server and offers its tools by role.
/// </summary>
[TestFixture]
internal class McpServerAuthenticationTest
{
    private const string kRoleRestrictedTool = McpTestRoleRestrictedTools.kRoleRestrictedTool;
    private const string kCurrentUserTool = "get_current_user";

    private static readonly List<string> kAllTools = [kRoleRestrictedTool, kCurrentUserTool];
    private static readonly List<string> kToolsWithoutRoleRestriction = [kCurrentUserTool];
    private static readonly List<string> kReporterAndModellerRoles = [Roles.Reporter, Roles.Modeller];

    /// <summary>
    /// Verifies that a token issued by the middleware server is accepted (positive control for the rejection tests).
    /// </summary>
    [Test]
    public async Task RequestWithMiddlewareToken_IsAccepted()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();

        using HttpResponseMessage response = await McpTestServer.PostToolsList(httpClient, McpTestServer.CreateMiddlewareToken(Roles.Reporter));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// Verifies that requests without a token are rejected.
    /// </summary>
    [Test]
    public async Task RequestWithoutToken_IsRejected()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();

        using HttpResponseMessage response = await McpTestServer.PostToolsList(httpClient, null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    /// <summary>
    /// Verifies that expired tokens are rejected (beyond the default clock skew of 5 minutes).
    /// </summary>
    [Test]
    public async Task RequestWithExpiredToken_IsRejected()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        string expiredToken = McpTestServer.CreateToken(McpTestServer.PrivateKey, JwtConstants.Issuer, JwtConstants.Audience,
            DateTime.UtcNow.AddMinutes(-10), McpTestServer.kUserName, Roles.Admin);

        using HttpResponseMessage response = await McpTestServer.PostToolsList(httpClient, expiredToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    /// <summary>
    /// Verifies that tokens signed with another key than the middleware server's are rejected.
    /// </summary>
    [Test]
    public async Task RequestWithTokenSignedByOtherKey_IsRejected()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        using RSA otherRsa = RSA.Create(2048);
        string forgedToken = McpTestServer.CreateToken(new RsaSecurityKey(otherRsa.ExportParameters(true)), JwtConstants.Issuer, JwtConstants.Audience,
            DateTime.UtcNow.AddMinutes(5), McpTestServer.kUserName, Roles.Admin);

        using HttpResponseMessage response = await McpTestServer.PostToolsList(httpClient, forgedToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    /// <summary>
    /// Verifies that tokens with another issuer or audience than the middleware server's are rejected.
    /// </summary>
    [TestCase("Other Issuer", JwtConstants.Audience)]
    [TestCase(JwtConstants.Issuer, "Other Audience")]
    public async Task RequestWithTokenOfOtherIssuerOrAudience_IsRejected(string issuer, string audience)
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        string token = McpTestServer.CreateToken(McpTestServer.PrivateKey, issuer, audience, DateTime.UtcNow.AddMinutes(5), McpTestServer.kUserName, Roles.Admin);

        using HttpResponseMessage response = await McpTestServer.PostToolsList(httpClient, token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    /// <summary>
    /// Verifies that valid tokens holding only an anonymous or technical role are refused. The middleware server
    /// issues anonymous bootstrap tokens without credentials, so these must not open the MCP server.
    /// </summary>
    [TestCase(Roles.Anonymous)]
    [TestCase(Roles.MiddlewareServer)]
    [TestCase(Roles.Importer)]
    [TestCase(Roles.DbBackup)]
    public async Task RequestWithAnonymousOrTechnicalRoleToken_IsForbidden(string role)
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();

        using HttpResponseMessage response = await McpTestServer.PostToolsList(httpClient, McpTestServer.CreateMiddlewareToken(role));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    /// <summary>
    /// Verifies that valid tokens without role claims are refused, like the internal tokens of the middleware server.
    /// </summary>
    [Test]
    public async Task RequestWithTokenWithoutRole_IsForbidden()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();

        using HttpResponseMessage response = await McpTestServer.PostToolsList(httpClient, McpTestServer.CreateMiddlewareToken());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    /// <summary>
    /// Verifies that a caller holding a required role sees all tools.
    /// </summary>
    [Test]
    public async Task AdminToken_ListsAllTools()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        await using McpClient client = await McpTestServer.ConnectAsync(httpClient, Roles.Admin);

        IList<McpClientTool> tools = await client.ListToolsAsync();

        Assert.That(tools.Select(tool => tool.Name), Is.EquivalentTo(kAllTools));
    }

    /// <summary>
    /// Verifies that tools restricted to other roles are not listed.
    /// </summary>
    [Test]
    public async Task ReporterToken_HidesToolRestrictedToOtherRoles()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        await using McpClient client = await McpTestServer.ConnectAsync(httpClient, Roles.Reporter);

        IList<McpClientTool> tools = await client.ListToolsAsync();

        Assert.That(tools.Select(tool => tool.Name), Is.EquivalentTo(kToolsWithoutRoleRestriction));
    }

    /// <summary>
    /// Verifies that calling a tool restricted to other roles is refused even though it is not listed.
    /// </summary>
    [Test]
    public async Task ReporterToken_CallingToolRestrictedToOtherRoles_IsRefused()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        await using McpClient client = await McpTestServer.ConnectAsync(httpClient, Roles.Reporter);

        Assert.ThrowsAsync<McpProtocolException>(async () => await client.CallToolAsync(kRoleRestrictedTool));
    }

    /// <summary>
    /// Verifies that a caller holding a required role can call the restricted tool.
    /// </summary>
    [Test]
    public async Task AuditorToken_CallingRestrictedTool_ReturnsResult()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        await using McpClient client = await McpTestServer.ConnectAsync(httpClient, Roles.Auditor);

        CallToolResult result = await client.CallToolAsync(kRoleRestrictedTool);

        Assert.That(result.IsError, Is.Not.True);
        Assert.That(GetText(result), Is.EqualTo(McpTestRoleRestrictedTools.kRoleRestrictedValue));
    }

    /// <summary>
    /// Verifies that the current user tool returns the name and roles of a middleware token.
    /// </summary>
    [Test]
    public async Task CurrentUserTool_ReturnsNameAndRolesOfMiddlewareToken()
    {
        await using WebApplication app = await McpTestServer.Start();
        using HttpClient httpClient = app.GetTestClient();
        await using McpClient client = await McpTestServer.ConnectAsync(httpClient, kReporterAndModellerRoles.ToArray());

        CallToolResult result = await client.CallToolAsync(kCurrentUserTool);

        using JsonDocument currentUser = JsonDocument.Parse(GetText(result));
        Assert.That(currentUser.RootElement.GetProperty("name").GetString(), Is.EqualTo(McpTestServer.kUserName));
        Assert.That(currentUser.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()),
            Is.EquivalentTo(kReporterAndModellerRoles));
    }

    /// <summary>
    /// Returns the single text content of a tool result.
    /// </summary>
    private static string GetText(CallToolResult result)
    {
        return result.Content.OfType<TextContentBlock>().Single().Text;
    }
}
