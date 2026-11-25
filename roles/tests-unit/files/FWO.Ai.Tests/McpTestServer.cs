using FWO.Ai.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Client;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JwtConstants = FWO.Basics.JwtConstants;

namespace FWO.Ai.Tests;

/// <summary>
/// Runs the MCP server pipeline on a test server and creates tokens the way the middleware server's JwtWriter does,
/// signed with a test key pair standing in for the middleware server's jwt keys.
/// </summary>
internal static class McpTestServer
{
    internal const string kUserName = "mcp-user";
    internal const string kToolsListRequest = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";
    private const string kUserDnClaimType = "x-hasura-uuid";
    private const string kLoopbackUrlWithFreePort = "http://127.0.0.1:0";
    private const int kTokenLifetimeMinutes = 5;
    private const int kTokenValidityMinutes = 30;

    private static readonly RSAParameters KeyPair = CreateKeyPair();
    internal static readonly RsaSecurityKey PrivateKey = new(KeyPair);
    internal static readonly RsaSecurityKey PublicKey = new(new RSAParameters { Modulus = KeyPair.Modulus, Exponent = KeyPair.Exponent });

    /// <summary>
    /// Starts the MCP server pipeline on a test server, validating tokens with the test public key.
    /// </summary>
    /// <param name="rateLimitSettings">Rate limits to apply; the defaults if null.</param>
    internal static async Task<WebApplication> Start(McpRateLimitSettings? rateLimitSettings = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        return await BuildAndStart(builder, rateLimitSettings ?? new McpRateLimitSettings());
    }

    /// <summary>
    /// Starts the MCP server pipeline on a test server and adds test-only configuration to its MCP server,
    /// e.g. a tool or prompt the production server does not offer, or a request filter.
    /// </summary>
    /// <param name="configureMcpServer">Adds the test-only configuration</param>
    internal static async Task<WebApplication> StartWith(Action<IMcpServerBuilder> configureMcpServer)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        return await BuildAndStart(builder, new McpRateLimitSettings(), configureMcpServer);
    }

    /// <summary>
    /// Starts the MCP server pipeline on Kestrel at a free loopback port, for limits the test server does not
    /// enforce (such as the request body size). Its address is returned by <see cref="GetBaseAddress"/>.
    /// </summary>
    internal static async Task<WebApplication> StartOnLoopback()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(kLoopbackUrlWithFreePort);
        return await BuildAndStart(builder, new McpRateLimitSettings());
    }

    /// <summary>
    /// Returns the address Kestrel listens on, after <see cref="StartOnLoopback"/>.
    /// </summary>
    internal static Uri GetBaseAddress(WebApplication app)
    {
        IServerAddressesFeature addresses = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>();
        return new Uri(addresses.Addresses.First());
    }

    /// <summary>
    /// Adds the services of the MCP server pipeline, with optional test-only configuration of its MCP server, and starts it.
    /// </summary>
    private static async Task<WebApplication> BuildAndStart(WebApplicationBuilder builder, McpRateLimitSettings rateLimitSettings,
        Action<IMcpServerBuilder>? configureMcpServer = null)
    {
        builder.Services.AddFwoJwtAuthentication(PublicKey);
        builder.Services.AddFwoMcpServer(rateLimitSettings);
        IMcpServerBuilder mcpServer = builder.Services.AddMcpServer().WithTools<McpTestRoleRestrictedTools>();
        configureMcpServer?.Invoke(mcpServer);

        WebApplication app = builder.Build();
        app.MapFwoMcp();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Sends a raw tools/list request, optionally with a bearer token.
    /// </summary>
    internal static async Task<HttpResponseMessage> PostToolsList(HttpClient httpClient, string? accessToken)
    {
        return await Post(httpClient, accessToken, kToolsListRequest);
    }

    /// <summary>
    /// Sends a raw MCP request with the given body to the MCP endpoint, optionally with a bearer token.
    /// </summary>
    internal static async Task<HttpResponseMessage> Post(HttpClient httpClient, string? accessToken, string body)
    {
        return await PostTo(httpClient, McpServerSetup.kMcpPath, accessToken, body);
    }

    /// <summary>
    /// Sends a raw MCP request with the given body to the given path, optionally with a bearer token.
    /// </summary>
    internal static async Task<HttpResponseMessage> PostTo(HttpClient httpClient, string path, string? accessToken, string body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, path);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (accessToken != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return await httpClient.SendAsync(request);
    }

    /// <summary>
    /// Connects an MCP client sending a middleware token with the given roles.
    /// </summary>
    internal static async Task<McpClient> ConnectAsync(HttpClient httpClient, params string[] roles)
    {
        HttpClientTransport transport = new(new HttpClientTransportOptions
        {
            Endpoint = new Uri(httpClient.BaseAddress!, McpServerSetup.kMcpPath),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {CreateMiddlewareToken(roles)}"
            }
        }, httpClient);
        return await McpClient.CreateAsync(transport);
    }

    /// <summary>
    /// Creates a valid token for the default test user with the given roles, signed with the test key.
    /// </summary>
    internal static string CreateMiddlewareToken(params string[] roles)
    {
        return CreateUserToken(kUserName, roles);
    }

    /// <summary>
    /// Creates a valid token for the given user with the given roles, signed with the test key.
    /// </summary>
    internal static string CreateUserToken(string userName, params string[] roles)
    {
        return CreateToken(PrivateKey, JwtConstants.Issuer, JwtConstants.Audience, DateTime.UtcNow.AddMinutes(kTokenLifetimeMinutes), userName, roles);
    }

    /// <summary>
    /// Creates a token the way the middleware server's JwtWriter does: name and roles are added as ClaimTypes,
    /// which the token handler writes as unique_name and role (pinned on the writer side by JwtWriterClaimsTest),
    /// and the user's dn as x-hasura-uuid.
    /// </summary>
    internal static string CreateToken(SecurityKey signingKey, string issuer, string audience, DateTime expires, string userName, params string[] roles)
    {
        List<Claim> claims = [new Claim(ClaimTypes.Name, userName), new Claim(kUserDnClaimType, UserDn(userName))];
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        ClaimsIdentity subject = new(claims);
        JwtSecurityTokenHandler tokenHandler = new();
        JwtSecurityToken token = tokenHandler.CreateJwtSecurityToken(
            issuer: issuer,
            audience: audience,
            subject: subject,
            notBefore: expires.AddMinutes(-kTokenValidityMinutes),
            expires: expires,
            issuedAt: expires.AddMinutes(-kTokenValidityMinutes),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Returns the directory identity of a test user.
    /// </summary>
    internal static string UserDn(string userName)
    {
        return $"uid={userName},ou=users,dc=test";
    }

    /// <summary>
    /// Creates the key pair standing in for the middleware server's jwt keys.
    /// </summary>
    private static RSAParameters CreateKeyPair()
    {
        using RSA rsa = RSA.Create(2048);
        return rsa.ExportParameters(true);
    }
}
