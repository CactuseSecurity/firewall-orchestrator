using FWO.Basics;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace FWO.Test
{
    [TestFixture]
    internal class ApiRateLimitingTest
    {
        private const string kUserDn = "uid=user,ou=users,dc=test";
        private const string kOtherUserDn = "uid=other,ou=users,dc=test";
        private static readonly List<bool> kTwoGrantedThenRejected = [true, true, false];
        private static readonly ApiRateLimitSettings kSmallBudget = new() { RequestsPerMinute = 2, ConcurrentExpensiveRequests = 1 };
        private static readonly List<(Type Controller, string Method)> kExpensiveEndpoints =
        [
            (typeof(FlowComplianceController), nameof(FlowComplianceController.GetFlowComplianceState)),
            (typeof(ApplicationAddressesController), nameof(ApplicationAddressesController.Get)),
            (typeof(FlowCatalogController), nameof(FlowCatalogController.GetAddressObjects)),
            (typeof(FlowCatalogController), nameof(FlowCatalogController.GetAddressGroups)),
            (typeof(FlowCatalogController), nameof(FlowCatalogController.GetServiceObjects)),
            (typeof(FlowCatalogController), nameof(FlowCatalogController.GetServiceGroups)),
            (typeof(FlowCatalogController), nameof(FlowCatalogController.GetTimeObjects))
        ];

        [Test]
        public void PartitionKeyIsTheUserDnOfAuthenticatedUsers()
        {
            Assert.That(ApiRateLimiting.GetPartitionKey(User(kUserDn, Roles.Reporter)), Is.EqualTo("user:" + kUserDn));
        }

        [TestCase(Roles.Anonymous)]
        [TestCase(Roles.Importer)]
        [TestCase(Roles.MiddlewareServer)]
        public void ServiceAndAnonymousIdentitiesAreNotLimited(string role)
        {
            Assert.That(ApiRateLimiting.GetPartitionKey(User(kUserDn, role)), Is.Null);
        }

        [Test]
        public void UnauthenticatedCallersAndTokensWithoutUserIdentityAreNotLimited()
        {
            ClaimsPrincipal unauthenticated = new(new ClaimsIdentity());
            List<Claim> internalTokenClaims = [new Claim("unique_name", Roles.ReporterViewAll)];
            ClaimsPrincipal internalToken = new(new ClaimsIdentity(internalTokenClaims, "test"));

            Assert.Multiple(() =>
            {
                Assert.That(ApiRateLimiting.GetPartitionKey(unauthenticated), Is.Null);
                Assert.That(ApiRateLimiting.GetPartitionKey(internalToken), Is.Null);
            });
        }

        [Test]
        public void GlobalLimiterRejectsAUserOverBudgetWithoutAffectingOthers()
        {
            using PartitionedRateLimiter<HttpContext> limiter = ApiRateLimiting.CreateGlobalLimiter(kSmallBudget);
            HttpContext user = Context(User(kUserDn, Roles.Reporter));
            HttpContext otherUser = Context(User(kOtherUserDn, Roles.Reporter));

            List<bool> userLeases = [Acquire(limiter, user), Acquire(limiter, user), Acquire(limiter, user)];

            Assert.Multiple(() =>
            {
                Assert.That(userLeases, Is.EqualTo(kTwoGrantedThenRejected));
                Assert.That(Acquire(limiter, otherUser), Is.True);
            });
        }

        [Test]
        public void GlobalLimiterDoesNotLimitExemptCallers()
        {
            using PartitionedRateLimiter<HttpContext> limiter = ApiRateLimiting.CreateGlobalLimiter(kSmallBudget);
            HttpContext importer = Context(User(kUserDn, Roles.Importer));

            for (int i = 0; i < 10; i++)
            {
                Assert.That(Acquire(limiter, importer), Is.True);
            }
        }

        [Test]
        public void ExpensivePartitionLimitsConcurrentRequestsPerUser()
        {
            RateLimitPartition<string> partition = ApiRateLimiting.CreateExpensivePartition(Context(User(kUserDn, Roles.Admin)), kSmallBudget);
            using RateLimiter limiter = partition.Factory(partition.PartitionKey);

            using RateLimitLease first = limiter.AttemptAcquire();
            using RateLimitLease second = limiter.AttemptAcquire();
            Assert.Multiple(() =>
            {
                Assert.That(first.IsAcquired, Is.True);
                Assert.That(second.IsAcquired, Is.False);
            });

            first.Dispose();
            using RateLimitLease afterRelease = limiter.AttemptAcquire();
            Assert.That(afterRelease.IsAcquired, Is.True);
        }

        [Test]
        public void ExpensivePartitionDoesNotLimitExemptCallers()
        {
            RateLimitPartition<string> partition = ApiRateLimiting.CreateExpensivePartition(Context(User(kUserDn, Roles.MiddlewareServer)), kSmallBudget);
            using RateLimiter limiter = partition.Factory(partition.PartitionKey);

            using RateLimitLease first = limiter.AttemptAcquire();
            using RateLimitLease second = limiter.AttemptAcquire();
            Assert.That(first.IsAcquired && second.IsAcquired, Is.True);
        }

        [Test]
        public async Task ConfigureRejectsWith429AndRetryAfter()
        {
            RateLimiterOptions options = new();
            ApiRateLimiting.Configure(options, kSmallBudget);
            using PartitionedRateLimiter<HttpContext> limiter = ApiRateLimiting.CreateGlobalLimiter(new ApiRateLimitSettings { RequestsPerMinute = 1 });
            HttpContext context = Context(User(kUserDn, Roles.Reporter));
            using RateLimitLease granted = limiter.AttemptAcquire(context);
            using RateLimitLease rejected = limiter.AttemptAcquire(context);

            await options.OnRejected!(new OnRejectedContext { HttpContext = context, Lease = rejected }, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(options.RejectionStatusCode, Is.EqualTo(StatusCodes.Status429TooManyRequests));
                Assert.That(options.GlobalLimiter, Is.Not.Null);
                Assert.That(rejected.IsAcquired, Is.False);
                Assert.That(context.Response.Headers.RetryAfter.ToString(), Is.EqualTo(ApiRateLimiting.kRetryAfterSeconds.ToString()));
            });
        }

        [Test]
        public void SettingsFallBackToDefaultsWithoutConfiguredValues()
        {
            ApiRateLimitSettings settings = new();

            Assert.Multiple(() =>
            {
                Assert.That(settings.RequestsPerMinute, Is.EqualTo(ApiRateLimitSettings.kDefaultRequestsPerMinute));
                Assert.That(settings.ConcurrentExpensiveRequests, Is.EqualTo(ApiRateLimitSettings.kDefaultConcurrentExpensiveRequests));
                Assert.That(ApiRateLimitSettings.FromConfigFile().RequestsPerMinute, Is.Positive);
                Assert.That(ApiRateLimitSettings.FromConfigFile().ConcurrentExpensiveRequests, Is.Positive);
            });
        }

        [Test]
        public void ExpensiveEndpointsUseTheExpensivePolicyAndABodySizeLimit()
        {
            foreach ((Type controller, string method) in kExpensiveEndpoints)
            {
                MethodInfo endpoint = controller.GetMethod(method)!;
                EnableRateLimitingAttribute? rateLimiting = endpoint.GetCustomAttribute<EnableRateLimitingAttribute>();
                RequestSizeLimitAttribute? sizeLimit = endpoint.GetCustomAttribute<RequestSizeLimitAttribute>();

                Assert.Multiple(() =>
                {
                    Assert.That(rateLimiting?.PolicyName, Is.EqualTo(ApiRateLimiting.kExpensivePolicy), $"{controller.Name}.{method}");
                    Assert.That(sizeLimit, Is.Not.Null, $"{controller.Name}.{method}");
                });
            }
        }

        private static bool Acquire(PartitionedRateLimiter<HttpContext> limiter, HttpContext context)
        {
            using RateLimitLease lease = limiter.AttemptAcquire(context);
            return lease.IsAcquired;
        }

        private static HttpContext Context(ClaimsPrincipal user)
        {
            return new DefaultHttpContext { User = user };
        }

        private static ClaimsPrincipal User(string userDn, string role)
        {
            List<Claim> claims = [new Claim("x-hasura-uuid", userDn), new Claim(ClaimTypes.Role, role)];
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, ClaimTypes.Role));
        }
    }
}
