using FWO.Ai.Mcp.Prompts;
using FWO.Ai.Mcp.Tools;
using FWO.Basics;
using FWO.Config.File;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace FWO.Ai.Mcp;

/// <summary>
/// Service and pipeline setup of the MCP server, used by Program and by the unit tests.
/// </summary>
internal static class McpServerSetup
{
    /// <summary>
    /// Path of the MCP endpoint. The middleware's Apache vhost publishes it under the same path,
    /// see ai_mcp_path in the installer inventory, which has to stay in step.
    /// </summary>
    public const string kMcpPath = "/mcp";

    /// <summary>
    /// Accepts the JWTs issued by the middleware server, validated with the given public key, see
    /// <see cref="JwtValidation.CreateParameters"/>. By default only users holding an FWO user role are authorized,
    /// see <see cref="FwoUserPolicy.CreateBuilder"/>.
    /// </summary>
    public static IServiceCollection AddFwoJwtAuthentication(this IServiceCollection services, SecurityKey jwtPublicKey)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = JwtValidation.CreateParameters(jwtPublicKey);
        });
        // the default policy applies to the MCP endpoint and to every plain [Authorize] on a tool
        services.AddAuthorization(options => options.DefaultPolicy = FwoUserPolicy.CreateBuilder().Build());
        return services;
    }

    /// <summary>
    /// Adds the MCP services: the transport to use (http), the tools and prompts to register and the rate limiting.
    /// </summary>
    public static IServiceCollection AddFwoMcpServer(this IServiceCollection services, McpRateLimitSettings rateLimitSettings)
    {
        services.AddFwoMcpRateLimiting(rateLimitSettings);
        services.AddHttpContextAccessor();
        services
            .AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .AddAuthorizationFilters()
            .WithTools<UserTools>()
            .WithPrompts<RequestApplicationPrompts>()
            .WithPrompts<AgentPrompts>();
        return services;
    }

    /// <summary>
    /// Maps the MCP endpoint at <see cref="kMcpPath"/>, reachable only with a valid JWT of a user holding an FWO user role,
    /// rate limited per user and with a limited request body size. Requests arrive through the local Apache reverse proxy,
    /// see <see cref="ReverseProxyForwardingOptions.Create"/>.
    /// </summary>
    public static WebApplication MapFwoMcp(this WebApplication app)
    {
        app.UseForwardedHeaders(ReverseProxyForwardingOptions.Create());
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapMcp(kMcpPath)
            .RequireAuthorization()
            .RequireRateLimiting(McpRateLimiting.kMcpPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(McpRateLimiting.kMaxRequestBodyBytes));
        return app;
    }
}
