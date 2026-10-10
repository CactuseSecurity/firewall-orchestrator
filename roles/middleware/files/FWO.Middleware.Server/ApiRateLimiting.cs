using FWO.Basics;
using FWO.Config.File;
using FWO.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Limits of the middleware REST API per user, read from the config file.
    /// </summary>
    public sealed class ApiRateLimitSettings
    {
        internal const int kDefaultRequestsPerMinute = 600;
        internal const int kDefaultConcurrentExpensiveRequests = 2;

        /// <summary>Requests per minute one user may send to any endpoint; a burst of up to one minute's budget is allowed.</summary>
        public int RequestsPerMinute { get; init; } = kDefaultRequestsPerMinute;

        /// <summary>Requests one user may run at the same time on endpoints with the expensive policy.</summary>
        public int ConcurrentExpensiveRequests { get; init; } = kDefaultConcurrentExpensiveRequests;

        /// <summary>
        /// Reads the limits from the config file; missing or non-positive values fall back to the defaults.
        /// </summary>
        public static ApiRateLimitSettings FromConfigFile()
        {
            return new ApiRateLimitSettings
            {
                RequestsPerMinute = PositiveOrDefault(ConfigFile.RateLimitRequestsPerMinute, kDefaultRequestsPerMinute),
                ConcurrentExpensiveRequests = PositiveOrDefault(ConfigFile.RateLimitConcurrentExpensiveRequests, kDefaultConcurrentExpensiveRequests)
            };
        }

        private static int PositiveOrDefault(int? value, int defaultValue)
        {
            return value is > 0 ? value.Value : defaultValue;
        }
    }

    /// <summary>
    /// Rate limiting of the middleware REST API: a global per-user request budget for every endpoint and named
    /// policies for endpoints whose single requests are expensive. Requests are partitioned by the authenticated
    /// user; service identities and unauthenticated requests are not limited here (logins are limited by the
    /// <see cref="LoginThrottle"/>).
    /// </summary>
    public static class ApiRateLimiting
    {
        /// <summary>
        /// Policy for endpoints whose single requests cause much database or compute work: it limits the
        /// requests one user runs at the same time, on top of the global request budget.
        /// </summary>
        public const string kExpensivePolicy = "expensive";

        /// <summary>Maximum request body size of the endpoints with the expensive policy; larger bodies get 413.</summary>
        public const long kMaxExpensiveRequestBodyBytes = 1024 * 1024;

        /// <summary>Retry hint for rejected requests.</summary>
        public const int kRetryAfterSeconds = 60;

        /// <summary>Requests of one user that may wait for a free slot of the expensive policy before being rejected.</summary>
        internal const int kExpensiveQueueLimit = 4;

        private const int kSecondsPerMinute = 60;
        private const string kUserPartitionPrefix = "user:";
        private const string kUnlimitedPartition = "";
        private static readonly List<string> kExemptRoles = [Roles.Anonymous, Roles.Importer, Roles.MiddlewareServer];

        /// <summary>
        /// Registers the rate limiter with the global budget and the named policies.
        /// </summary>
        public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, ApiRateLimitSettings settings)
        {
            return services.AddRateLimiter(options => Configure(options, settings));
        }

        /// <summary>
        /// Configures the global budget, the named policies and the rejection response.
        /// </summary>
        internal static void Configure(RateLimiterOptions options, ApiRateLimitSettings settings)
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = CreateGlobalLimiter(settings);
            options.AddPolicy(kExpensivePolicy, context => CreateExpensivePartition(context, settings));
            options.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = kRetryAfterSeconds.ToString();
                Log.WriteWarning("Rate Limit", $"Rejected {context.HttpContext.Request.Path} for {GetPartitionKey(context.HttpContext.User)}.");
                return ValueTask.CompletedTask;
            };
        }

        /// <summary>
        /// Creates the per-user token bucket applied to every endpoint.
        /// </summary>
        internal static PartitionedRateLimiter<HttpContext> CreateGlobalLimiter(ApiRateLimitSettings settings)
        {
            return PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                string? partitionKey = GetPartitionKey(context.User);
                return partitionKey == null
                    ? RateLimitPartition.GetNoLimiter(kUnlimitedPartition)
                    : RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = settings.RequestsPerMinute,
                        TokensPerPeriod = Math.Max(1, settings.RequestsPerMinute / kSecondsPerMinute),
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        }

        /// <summary>
        /// Creates the per-user concurrency partition of the expensive policy.
        /// </summary>
        internal static RateLimitPartition<string> CreateExpensivePartition(HttpContext context, ApiRateLimitSettings settings)
        {
            string? partitionKey = GetPartitionKey(context.User);
            return partitionKey == null
                ? RateLimitPartition.GetNoLimiter(kUnlimitedPartition)
                : RateLimitPartition.GetConcurrencyLimiter(partitionKey, _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = settings.ConcurrentExpensiveRequests,
                    QueueLimit = kExpensiveQueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
        }

        /// <summary>
        /// Returns the partition of a caller: the user's directory identity, or null for callers that are not limited
        /// here (unauthenticated requests, anonymous login tokens and service identities without a user identity).
        /// </summary>
        internal static string? GetPartitionKey(ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true || kExemptRoles.Exists(user.IsInRole))
            {
                return null;
            }

            string? userDn = user.FindFirstValue("x-hasura-uuid");
            return string.IsNullOrWhiteSpace(userDn) ? null : kUserPartitionPrefix + userDn;
        }
    }
}
