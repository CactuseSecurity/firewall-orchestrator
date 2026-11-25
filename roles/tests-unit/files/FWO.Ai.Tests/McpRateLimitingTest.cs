using FWO.Ai.Mcp;
using FWO.Basics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using NUnit.Framework;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the per-user rate limits and the request body limit of the MCP server.
/// </summary>
[TestFixture]
internal class McpRateLimitingTest
{
    private const string kOtherUserName = "other-mcp-user";
    private const string kNameClaimType = "unique_name";
    private const string kRoleClaimType = "role";
    private const string kUserDnClaimType = "x-hasura-uuid";
    private const int kRefusedRequests = 3;
    private const int kConfiguredRequestsPerMinute = 5;
    private const int kConfiguredConcurrentRequests = 3;

    private static readonly McpRateLimitSettings kSmallBudget = new() { RequestsPerMinute = 2, ConcurrentRequests = 1 };
    private static readonly List<HttpStatusCode> kTwoAcceptedThenRejected = [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests];
    private static readonly List<HttpStatusCode> kTwoAccepted = [HttpStatusCode.OK, HttpStatusCode.OK];

    /// <summary>
    /// Verifies that a user's requests beyond the budget are rejected with 429 and a retry hint.
    /// </summary>
    [Test]
    public async Task RequestsOverBudget_AreRejectedWith429AndRetryAfter()
    {
        await using WebApplication app = await McpTestServer.Start(kSmallBudget);
        using HttpClient httpClient = app.GetTestClient();
        string token = McpTestServer.CreateMiddlewareToken(Roles.Reporter);

        List<HttpStatusCode> statusCodes = [];
        string? retryAfter = null;
        for (int i = 0; i < kTwoAcceptedThenRejected.Count; i++)
        {
            using HttpResponseMessage response = await McpTestServer.PostToolsList(httpClient, token);
            statusCodes.Add(response.StatusCode);
            retryAfter = response.Headers.RetryAfter?.ToString();
        }

        Assert.Multiple(() =>
        {
            Assert.That(statusCodes, Is.EqualTo(kTwoAcceptedThenRejected));
            Assert.That(retryAfter, Is.EqualTo(McpRateLimiting.kRetryAfterSeconds.ToString()));
        });
    }

    /// <summary>
    /// Verifies that a user over budget does not affect the budget of other users.
    /// </summary>
    [Test]
    public async Task UserOverBudget_DoesNotAffectOtherUsers()
    {
        await using WebApplication app = await McpTestServer.Start(kSmallBudget);
        using HttpClient httpClient = app.GetTestClient();

        List<HttpStatusCode> userStatusCodes = await PostToolsListRepeatedly(httpClient, McpTestServer.CreateMiddlewareToken(Roles.Reporter),
            kTwoAcceptedThenRejected.Count);
        using HttpResponseMessage otherUserResponse = await McpTestServer.PostToolsList(httpClient,
            McpTestServer.CreateUserToken(kOtherUserName, Roles.Reporter));

        Assert.Multiple(() =>
        {
            Assert.That(userStatusCodes, Is.EqualTo(kTwoAcceptedThenRejected));
            Assert.That(otherUserResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    /// <summary>
    /// Verifies that refused requests (here: anonymous tokens naming the user) do not use up the user's budget.
    /// </summary>
    [Test]
    public async Task RefusedRequests_DoNotUseTheBudgetOfTheUserNamedInThem()
    {
        await using WebApplication app = await McpTestServer.Start(kSmallBudget);
        using HttpClient httpClient = app.GetTestClient();

        List<HttpStatusCode> refusedStatusCodes = await PostToolsListRepeatedly(httpClient, McpTestServer.CreateMiddlewareToken(Roles.Anonymous),
            kRefusedRequests);
        List<HttpStatusCode> userStatusCodes = await PostToolsListRepeatedly(httpClient, McpTestServer.CreateMiddlewareToken(Roles.Reporter),
            kTwoAccepted.Count);

        Assert.Multiple(() =>
        {
            Assert.That(refusedStatusCodes, Is.All.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(userStatusCodes, Is.EqualTo(kTwoAccepted));
        });
    }

    /// <summary>
    /// Verifies that a request body of exactly the maximum size is accepted (on Kestrel, which enforces the limit).
    /// </summary>
    [Test]
    public async Task RequestBodyAtLimit_IsAccepted()
    {
        await using WebApplication app = await McpTestServer.StartOnLoopback();
        using HttpClient httpClient = new() { BaseAddress = McpTestServer.GetBaseAddress(app) };

        using HttpResponseMessage response = await McpTestServer.Post(httpClient, McpTestServer.CreateMiddlewareToken(Roles.Reporter),
            PaddedToolsListRequest(McpRateLimiting.kMaxRequestBodyBytes));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// Verifies that a request body larger than the maximum size is rejected with 413 (on Kestrel, which enforces the limit).
    /// </summary>
    [Test]
    public async Task RequestBodyOverLimit_IsRejectedWith413()
    {
        await using WebApplication app = await McpTestServer.StartOnLoopback();
        using HttpClient httpClient = new() { BaseAddress = McpTestServer.GetBaseAddress(app) };

        using HttpResponseMessage response = await McpTestServer.Post(httpClient, McpTestServer.CreateMiddlewareToken(Roles.Reporter),
            PaddedToolsListRequest(McpRateLimiting.kMaxRequestBodyBytes + 1));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
    }

    /// <summary>
    /// Verifies that every MCP endpoint (POST, GET, DELETE) carries the concurrency policy and the body size limit.
    /// </summary>
    [Test]
    public async Task McpEndpoints_UseTheMcpPolicyAndTheBodySizeLimit()
    {
        await using WebApplication app = await McpTestServer.Start();
        List<Endpoint> endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(dataSource => dataSource.Endpoints).ToList();

        Assert.That(endpoints, Is.Not.Empty);
        foreach (Endpoint endpoint in endpoints)
        {
            Assert.Multiple(() =>
            {
                Assert.That(endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName, Is.EqualTo(McpRateLimiting.kMcpPolicy), endpoint.DisplayName);
                Assert.That(endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize, Is.EqualTo(McpRateLimiting.kMaxRequestBodyBytes), endpoint.DisplayName);
            });
        }
    }

    /// <summary>
    /// Verifies that the concurrency partition admits the configured number of requests of a user at the same time.
    /// </summary>
    [Test]
    public void ConcurrencyPartition_LimitsConcurrentRequestsPerUser()
    {
        DefaultHttpContext context = new() { User = User(new Claim(kUserDnClaimType, McpTestServer.UserDn(McpTestServer.kUserName))) };
        RateLimitPartition<string> partition = McpRateLimiting.CreateConcurrencyPartition(context, kSmallBudget);
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

    /// <summary>
    /// Verifies that users are partitioned by their directory identity, also when a name is present.
    /// </summary>
    [Test]
    public void PartitionKey_IsTheUserDn()
    {
        string userDn = McpTestServer.UserDn(McpTestServer.kUserName);
        ClaimsPrincipal user = User(new Claim(kUserDnClaimType, userDn), new Claim(kNameClaimType, McpTestServer.kUserName));

        Assert.That(McpRateLimiting.GetPartitionKey(user), Is.EqualTo("dn:" + userDn));
    }

    /// <summary>
    /// Verifies that users without directory identity are partitioned by name, and callers without both share a partition.
    /// </summary>
    [Test]
    public void PartitionKey_FallsBackToTheUserName()
    {
        ClaimsPrincipal namedUser = User(new Claim(kNameClaimType, McpTestServer.kUserName), new Claim(kUserDnClaimType, " "));

        Assert.Multiple(() =>
        {
            Assert.That(McpRateLimiting.GetPartitionKey(namedUser), Is.EqualTo("name:" + McpTestServer.kUserName));
            Assert.That(McpRateLimiting.GetPartitionKey(new ClaimsPrincipal()), Is.EqualTo("name:"));
        });
    }

    /// <summary>
    /// Verifies that configured limits are used and missing or non-positive values fall back to the defaults.
    /// </summary>
    [Test]
    public void Settings_UseConfiguredValuesOrFallBackToDefaults()
    {
        McpRateLimitSettings configured = McpRateLimitSettings.FromConfigValues(kConfiguredRequestsPerMinute, kConfiguredConcurrentRequests);
        McpRateLimitSettings missing = McpRateLimitSettings.FromConfigValues(null, 0);

        Assert.Multiple(() =>
        {
            Assert.That(configured.RequestsPerMinute, Is.EqualTo(kConfiguredRequestsPerMinute));
            Assert.That(configured.ConcurrentRequests, Is.EqualTo(kConfiguredConcurrentRequests));
            Assert.That(missing.RequestsPerMinute, Is.EqualTo(McpRateLimitSettings.kDefaultRequestsPerMinute));
            Assert.That(missing.ConcurrentRequests, Is.EqualTo(McpRateLimitSettings.kDefaultConcurrentRequests));
        });
    }

    /// <summary>
    /// Sends the given number of tools/list requests one after the other and returns their status codes.
    /// </summary>
    private static async Task<List<HttpStatusCode>> PostToolsListRepeatedly(HttpClient httpClient, string token, int count)
    {
        List<HttpStatusCode> statusCodes = [];
        for (int i = 0; i < count; i++)
        {
            using HttpResponseMessage response = await McpTestServer.PostToolsList(httpClient, token);
            statusCodes.Add(response.StatusCode);
        }
        return statusCodes;
    }

    /// <summary>
    /// Returns a tools/list request padded with trailing whitespace to the given size in bytes.
    /// </summary>
    private static string PaddedToolsListRequest(long totalBytes)
    {
        int paddingLength = (int)(totalBytes - McpTestServer.kToolsListRequest.Length);
        return McpTestServer.kToolsListRequest + new string(' ', paddingLength);
    }

    /// <summary>
    /// Creates an authenticated user with the given claims and the claim types of the MCP server.
    /// </summary>
    private static ClaimsPrincipal User(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", kNameClaimType, kRoleClaimType));
    }
}
