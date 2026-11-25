using FWO.Basics;
using FWO.Logging;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace FWO.Ai.Harness.Hosting;

/// <summary>
/// Rate limiting of the harness server, partitioned by the user's directory identity like the MCP server: every run
/// counts against a budget of runs per user and minute, as each run calls the AI provider up to the maximum number
/// of iterations, and a user runs a limited number of runs at the same time. Only requests that passed the
/// authorization reach the limiter.
/// </summary>
internal static class HarnessRateLimiting
{
    /// <summary>
    /// Policy of the AG-UI endpoint: at most the configured number of runs per user at a time, without waiting. It
    /// limits the load a user puts on the AI provider. The harness keeps no state between runs, so runs of one thread
    /// may run at the same time as well.
    /// </summary>
    public const string kRunPolicy = "agent-run";

    /// <summary>Retry hint for rejected requests.</summary>
    public const int kRetryAfterSeconds = 60;

    /// <summary>The budget window of one minute moves on in steps of 10 seconds.</summary>
    private const int kSegmentsPerWindow = 6;
    private const string kUserDnPartitionPrefix = "dn:";
    private const string kUserNamePartitionPrefix = "name:";
    private static readonly TimeSpan kBudgetWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Registers the rate limiter with the run budget and the run policy.
    /// </summary>
    public static IServiceCollection AddFwoHarnessRateLimiting(this IServiceCollection services, int runsPerMinute, int concurrentRunsPerUser)
    {
        return services.AddRateLimiter(options => Configure(options, runsPerMinute, concurrentRunsPerUser));
    }

    /// <summary>
    /// Configures the run budget, the run policy and the rejection response.
    /// </summary>
    internal static void Configure(RateLimiterOptions options, int runsPerMinute, int concurrentRunsPerUser)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = CreateBudgetLimiter(runsPerMinute);
        options.AddPolicy(kRunPolicy, context => CreateRunPartition(context, concurrentRunsPerUser));
        options.OnRejected = (context, _) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = kRetryAfterSeconds.ToString();
            Log.WriteWarning("Rate Limit", $"Rejected AG-UI request for {GetPartitionKey(context.HttpContext.User)}.");
            return ValueTask.CompletedTask;
        };
    }

    /// <summary>
    /// Creates the per-user run budget: at most the given number of requests within any minute.
    /// </summary>
    internal static PartitionedRateLimiter<HttpContext> CreateBudgetLimiter(int runsPerMinute)
    {
        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetSlidingWindowLimiter(GetPartitionKey(context.User), _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = runsPerMinute,
                Window = kBudgetWindow,
                SegmentsPerWindow = kSegmentsPerWindow,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    /// <summary>
    /// Creates the per-user partition of the run policy: the given number of runs at a time, without a queue.
    /// </summary>
    internal static RateLimitPartition<string> CreateRunPartition(HttpContext context, int concurrentRunsPerUser)
    {
        return RateLimitPartition.GetConcurrencyLimiter(GetPartitionKey(context.User), _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = concurrentRunsPerUser,
            QueueLimit = 0
        });
    }

    /// <summary>
    /// Returns the partition of a caller: the user's directory identity, which the AG-UI endpoint requires, else the
    /// user name. Callers without either share one partition.
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
