using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Mcp;
using FWO.Basics;
using FWO.Config.File;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;

namespace FWO.Ai.Harness.Hosting;

/// <summary>
/// Service and pipeline setup of the harness server, used by Program and by the unit tests. The server offers the
/// agent of the FWO users over AG-UI (Agent-User Interaction Protocol): a client posts a run and receives the answer
/// as server-sent events.
/// </summary>
internal static class HarnessServerSetup
{
    /// <summary>Path of the AG-UI endpoint.</summary>
    public const string kAgUiPath = "/agui";

    private const string kBearerScheme = "Bearer";

    /// <summary>
    /// Accepts the JWTs issued by the middleware server, validated with the given public key. By default only users
    /// holding an FWO user role and a directory identity are authorized, see <see cref="CreateFwoUserPolicy"/>.
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
        services.AddAuthorization(options => options.DefaultPolicy = CreateFwoUserPolicy());
        return services;
    }

    /// <summary>
    /// Adds the services of the AG-UI server: the holder of the agent factory and the rate limiting. The server is
    /// stateless: no session store is registered, so the AG-UI hosting runs each request on a session of its own, and the
    /// client sends the whole conversation with every run (see <see cref="TurnInput"/>).
    /// </summary>
    /// <param name="services">Services of the harness server</param>
    /// <param name="settings">Settings of the harness server</param>
    /// <param name="agentFactories">Holder of the agent factory of the current AI settings, which the service provider
    /// disposes at shutdown; filled by the settings refresh (see <see cref="HarnessSettingsRefreshSetup"/>), or by a test</param>
    public static IServiceCollection AddFwoAgentServer(this IServiceCollection services, HarnessSettings settings,
        AgentFactoryHolder agentFactories)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(agentFactories);
        services.AddSingleton(settings);
        // registered by a factory, as the service provider only disposes the singletons it created
        services.AddSingleton(_ => agentFactories);
        services.AddAGUIServer();
        services.AddFwoHarnessRateLimiting(settings.RunsPerMinute, settings.MaxConcurrentRunsPerUser);
        return services;
    }

    /// <summary>
    /// Maps the AG-UI endpoint at <see cref="kAgUiPath"/>, reachable only with a valid JWT of a user holding an FWO user
    /// role, rate limited per user, with a limited number of runs per user at a time and a limited request body size
    /// (<see cref="HarnessSettings.MaxRequestBodyBytes"/>). Each request runs the agent of the calling user, which calls
    /// the MCP server with the JWT of the request. The server starts without AI settings; while none are in effect, e.g.
    /// as no AI provider is configured, every run fails with the reason of the <see cref="AgentFactoryHolder"/> (500).
    /// </summary>
    public static WebApplication MapFwoAgent(this WebApplication app)
    {
        HarnessSettings settings = app.Services.GetRequiredService<HarnessSettings>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapAGUIServer(kAgUiPath, context => SelectUserAgent(context, context.RequestServices.GetRequiredService<AgentFactoryHolder>(), settings))
            .RequireAuthorization()
            .RequireRateLimiting(HarnessRateLimiting.kRunPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(settings.MaxRequestBodyBytes));
        return app;
    }

    /// <summary>
    /// Creates the agent of the calling user, calling the MCP server with the bearer token of the request, which the
    /// authorization has validated, and deciding about the AI functions with the roles of that token
    /// (<see cref="JwtClaimTypes.kRole"/>). The agent factory of the current AI settings is leased until the response has
    /// ended, so that a change of the settings does not dispose it during the run. Returns null, i.e. 404, for a request
    /// without bearer token.
    /// </summary>
    /// <exception cref="InvalidOperationException">No AI settings are in effect, see <see cref="AgentFactoryHolder.UnavailableReason"/>.</exception>
    internal static AIAgent? SelectUserAgent(HttpContext context, AgentFactoryHolder agentFactories, HarnessSettings settings)
    {
        string? accessToken = GetBearerToken(context.Request);
        if (accessToken == null)
        {
            return null;
        }
        AgentFactoryLease lease = agentFactories.Acquire();
        context.Response.RegisterForDispose(lease);
        McpConnectionSettings userConnection = new()
        {
            Endpoint = settings.McpEndpoint,
            AccessTokenProvider = _ => ValueTask.FromResult(accessToken),
            ConnectionTimeout = settings.McpConnectionTimeout
        };
        List<string> userRoles = [.. context.User.FindAll(JwtClaimTypes.kRole).Select(roleClaim => roleClaim.Value)];
        return lease.Factory.CreateUserAgent(userConnection, userRoles);
    }

    /// <summary>
    /// Returns the bearer token of the request's authorization header, or null if there is none.
    /// </summary>
    private static string? GetBearerToken(HttpRequest request)
    {
        bool hasAuthorization = AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out AuthenticationHeaderValue? authorization);
        bool isBearer = hasAuthorization && string.Equals(authorization?.Scheme, kBearerScheme, StringComparison.OrdinalIgnoreCase);
        return isBearer && !string.IsNullOrWhiteSpace(authorization?.Parameter) ? authorization.Parameter : null;
    }

    /// <summary>
    /// Requires a user holding at least one FWO user role, and a directory identity
    /// (<see cref="JwtClaimTypes.kUserDn"/>), which partitions the rate limits.
    /// </summary>
    private static AuthorizationPolicy CreateFwoUserPolicy()
    {
        return FwoUserPolicy.CreateBuilder()
            .RequireAssertion(context => !string.IsNullOrWhiteSpace(context.User.FindFirst(JwtClaimTypes.kUserDn)?.Value))
            .Build();
    }
}
