using FWO.Basics;
using FWO.Config.File;
using FWO.Logging;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace FWO.Ai.Mcp;

/// <summary>
/// Limits of the MCP server per user, read from the same config file keys as the limits of the middleware REST API
/// (rate_limit_requests_per_minute, rate_limit_concurrent_expensive_requests). Both services count separately.
/// </summary>
internal sealed class McpRateLimitSettings
{
    internal const int kDefaultRequestsPerMinute = 600;
    internal const int kDefaultConcurrentRequests = 2;

    /// <summary>Requests one user may send within any minute.</summary>
    public int RequestsPerMinute { get; init; } = kDefaultRequestsPerMinute;

    /// <summary>Requests one user may run at the same time.</summary>
    public int ConcurrentRequests { get; init; } = kDefaultConcurrentRequests;

    /// <summary>
    /// Reads the limits from the config file; missing or non-positive values fall back to the defaults.
    /// </summary>
    public static McpRateLimitSettings FromConfigFile()
    {
        return FromConfigValues(ConfigFile.RateLimitRequestsPerMinute, ConfigFile.RateLimitConcurrentExpensiveRequests);
    }

    /// <summary>
    /// Creates the limits from configured values; missing or non-positive values fall back to the defaults.
    /// </summary>
    internal static McpRateLimitSettings FromConfigValues(int? requestsPerMinute, int? concurrentRequests)
    {
        return new McpRateLimitSettings
        {
            RequestsPerMinute = PositiveOrDefault(requestsPerMinute, kDefaultRequestsPerMinute),
            ConcurrentRequests = PositiveOrDefault(concurrentRequests, kDefaultConcurrentRequests)
        };
    }

    private static int PositiveOrDefault(int? value, int defaultValue)
    {
        return value is > 0 ? value.Value : defaultValue;
    }
}

/// <summary>
/// Rate limiting of the MCP server: every request counts against a request budget of its user per minute and
/// against a limit of concurrent requests per user, because a single tool call may cause much work. Requests are
/// partitioned by the user's directory identity. Only requests that passed the authorization reach the limiter;
/// callers without an FWO user role (anonymous, technical, invalid tokens) are refused before.
/// </summary>
internal static class McpRateLimiting
{
    /// <summary>
    /// Policy of the MCP endpoint, limiting the requests one user runs at the same time.
    /// </summary>
    public const string kMcpPolicy = "mcp";

    /// <summary>Maximum request body size of the MCP endpoint; larger bodies get 413.</summary>
    public const long kMaxRequestBodyBytes = 1024 * 1024;

    /// <summary>Retry hint for rejected requests.</summary>
    public const int kRetryAfterSeconds = 60;

    /// <summary>Requests of one user that may wait for a free concurrency slot before being rejected.</summary>
    internal const int kQueueLimit = 4;

    /// <summary>The budget window of one minute moves on in steps of 10 seconds.</summary>
    private const int kSegmentsPerWindow = 6;
    private const string kUserDnPartitionPrefix = "dn:";
    private const string kUserNamePartitionPrefix = "name:";
    private static readonly TimeSpan kBudgetWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Registers the rate limiter with the request budget and the concurrency policy.
    /// </summary>
    public static IServiceCollection AddFwoMcpRateLimiting(this IServiceCollection services, McpRateLimitSettings settings)
    {
        return services.AddRateLimiter(options => Configure(options, settings));
    }

    /// <summary>
    /// Configures the request budget, the concurrency policy and the rejection response.
    /// </summary>
    internal static void Configure(RateLimiterOptions options, McpRateLimitSettings settings)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = CreateBudgetLimiter(settings);
        options.AddPolicy(kMcpPolicy, context => CreateConcurrencyPartition(context, settings));
        options.OnRejected = (context, _) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = kRetryAfterSeconds.ToString();
            Log.WriteWarning("Rate Limit", $"Rejected MCP request for {GetPartitionKey(context.HttpContext.User)}.");
            return ValueTask.CompletedTask;
        };
    }

    /// <summary>
    /// Creates the per-user request budget: at most RequestsPerMinute requests within any minute.
    /// </summary>
    internal static PartitionedRateLimiter<HttpContext> CreateBudgetLimiter(McpRateLimitSettings settings)
    {
        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetSlidingWindowLimiter(GetPartitionKey(context.User), _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = settings.RequestsPerMinute,
                Window = kBudgetWindow,
                SegmentsPerWindow = kSegmentsPerWindow,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    /// <summary>
    /// Creates the per-user concurrency partition of the MCP policy.
    /// </summary>
    internal static RateLimitPartition<string> CreateConcurrencyPartition(HttpContext context, McpRateLimitSettings settings)
    {
        return RateLimitPartition.GetConcurrencyLimiter(GetPartitionKey(context.User), _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = settings.ConcurrentRequests,
            QueueLimit = kQueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    }

    /// <summary>
    /// Returns the partition of a caller: the user's directory identity (x-hasura-uuid), else the user name.
    /// Callers without either share one partition, so no caller is unlimited.
    /// </summary>
    internal static string GetPartitionKey(ClaimsPrincipal user)
    {
        string? userDn = user.FindFirstValue(JwtClaimTypes.kUserDn);
        if (!string.IsNullOrWhiteSpace(userDn))
        {
            return kUserDnPartitionPrefix + userDn;
        }
        return kUserNamePartitionPrefix + (user.Identity?.Name ?? "");
    }
}
