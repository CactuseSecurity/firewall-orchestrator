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
        /// <summary>Longest refill period; shorter periods keep bursts close to one minute's budget.</summary>
        internal const int kMaxRefillPeriodSeconds = 10;
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
            (int tokensPerPeriod, TimeSpan replenishmentPeriod) = GetRefill(settings.RequestsPerMinute);
            return PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                string? partitionKey = GetPartitionKey(context.User);
                return partitionKey == null
                    ? RateLimitPartition.GetNoLimiter(kUnlimitedPartition)
                    : RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = settings.RequestsPerMinute,
                        TokensPerPeriod = tokensPerPeriod,
                        ReplenishmentPeriod = replenishmentPeriod,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        }

        /// <summary>
        /// Returns a smooth refill of the given number of tokens per minute: every token is refilled within at most
        /// <see cref="kMaxRefillPeriodSeconds"/> seconds, so a user cannot get much more than one minute's budget at
        /// once. Below 60 per minute one token is added every 60 / requestsPerMinute seconds; from 60 on the refill is
        /// exact when a period of at most <see cref="kMaxRefillPeriodSeconds"/> whole seconds receives a whole number of
        /// tokens (e.g. 600: 10 every second, 100: 5 every 3 seconds) and otherwise rounded to the period with the
        /// smallest error, which is at most 3 tokens per minute (e.g. 601: 10 every second, 89: 3 every 2 seconds).
        /// </summary>
        /// <param name="requestsPerMinute">Requests allowed per minute, at least 1.</param>
        internal static (int TokensPerPeriod, TimeSpan ReplenishmentPeriod) GetRefill(int requestsPerMinute)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestsPerMinute);
            if (requestsPerMinute < kSecondsPerMinute)
            {
                return (1, TimeSpan.FromSeconds((double)kSecondsPerMinute / requestsPerMinute));
            }
            int periodsPerMinute = GreatestCommonDivisor(requestsPerMinute, kSecondsPerMinute);
            int exactPeriodSeconds = kSecondsPerMinute / periodsPerMinute;
            if (exactPeriodSeconds <= kMaxRefillPeriodSeconds)
            {
                return (requestsPerMinute / periodsPerMinute, TimeSpan.FromSeconds(exactPeriodSeconds));
            }
            return GetRoundedRefill(requestsPerMinute);
        }

        /// <summary>
        /// Returns the refill with a period of at most <see cref="kMaxRefillPeriodSeconds"/> whole seconds whose rate
        /// comes closest to the given number of tokens per minute.
        /// </summary>
        private static (int TokensPerPeriod, TimeSpan ReplenishmentPeriod) GetRoundedRefill(int requestsPerMinute)
        {
            int bestTokens = requestsPerMinute / kSecondsPerMinute;
            int bestPeriodSeconds = 1;
            double bestError = double.MaxValue;
            for (int periodSeconds = 1; periodSeconds <= kMaxRefillPeriodSeconds; periodSeconds++)
            {
                int tokens = (int)Math.Round((double)requestsPerMinute * periodSeconds / kSecondsPerMinute);
                double error = Math.Abs((double)tokens * kSecondsPerMinute / periodSeconds - requestsPerMinute);
                if (error < bestError)
                {
                    (bestTokens, bestPeriodSeconds, bestError) = (tokens, periodSeconds, error);
                }
            }
            return (bestTokens, TimeSpan.FromSeconds(bestPeriodSeconds));
        }

        private static int GreatestCommonDivisor(int first, int second)
        {
            while (second != 0)
            {
                (first, second) = (second, first % second);
            }
            return first;
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
