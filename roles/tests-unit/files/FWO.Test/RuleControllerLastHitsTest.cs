using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the GetRuleLastHits endpoint: its route, authorization and rate limiting, the request contract including
/// defaults, filters, paging and aggregated validation, and the restriction to the managements and gateways
/// visible to the caller.
/// </summary>
[TestFixture]
internal class RuleControllerLastHitsTest
{
    private const int kMaxLimit = GlobalConst.kDefaultMaxRestResponseObjectLimit;
    private const int kOwnManagementId = 5;
    private const int kForeignManagementId = 6;
    private const int kOwnGatewayId = 51;
    private const int kForeignGatewayId = 52;
    private const string kOwnGatewayName = "gw-own";
    private const string kForeignGatewayName = "gw-foreign";
    private const string kFirstRuleUid = "uid-1";
    private const string kSecondRuleUid = "uid-2";
    private const string kFirstRuleName = "allow web";
    private const int kRestrictedTenantId = 7;
    private const string kVisibleManagementsClaim = "x-hasura-visible-managements";
    private const string kVisibleDevicesClaim = "x-hasura-visible-devices";
    private const string kTenantIdClaim = "x-hasura-tenant-id";
    private const string kBaseWhere = "{\"_and\":[{\"mgm_id\":{\"_eq\":5}},{\"removed\":{\"_is_null\":true}}]}";

    private static readonly DateTime kFirstRuleLastHit = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime kOwnGatewayLastHit = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime kForeignGatewayLastHit = new(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc);
    private static readonly List<string> kAdminRole = [Roles.Admin];
    private static readonly List<string> kAuditorRole = [Roles.Auditor];
    private static readonly List<string> kReporterViewAllRole = [Roles.ReporterViewAll];
    private static readonly List<string> kReporterRole = [Roles.Reporter];
    private static readonly List<string> kAllowedRoles = [Roles.Admin, Roles.Auditor, Roles.Reporter, Roles.ReporterViewAll, Roles.Recertifier];
    private static readonly List<string> kDeniedRoles =
    [
        Roles.Modeller, Roles.Requester, Roles.Approver, Roles.Planner, Roles.Implementer, Roles.Reviewer,
        Roles.Importer, Roles.MiddlewareServer, Roles.Anonymous
    ];
    private static readonly List<int> kBothGateways = [kOwnGatewayId, kForeignGatewayId];
    private static readonly List<int> kOwnGatewayOnly = [kOwnGatewayId];
    private static readonly List<string> kBothRuleUids = [kFirstRuleUid, kSecondRuleUid];
    private static readonly List<string> kFirstRuleUidOnly = [kFirstRuleUid];
    private static readonly List<string> kSecondRuleUidOnly = [kSecondRuleUid];
    private static readonly List<string> kAllInvalidPaths =
    [
        GetRuleLastHitsValidationSchema.kManagementIdPath,
        GetRuleLastHitsValidationSchema.kLimitPath,
        GetRuleLastHitsValidationSchema.kOffsetPath,
        GetRuleLastHitsValidationSchema.kFilterMgmIdPath,
        GetRuleLastHitsValidationSchema.kFilterDevIdPath,
        GetRuleLastHitsValidationSchema.kFilterRuleUidPath + "[1]",
        GetRuleLastHitsValidationSchema.kFilterRuleUidPath + "[2]",
        GetRuleLastHitsValidationSchema.kFilterRuleNamePath,
        GetRuleLastHitsValidationSchema.kFilterGatewayNamePath,
        GetRuleLastHitsValidationSchema.kFilterLastHitBeforePath,
        GetRuleLastHitsValidationSchema.kFilterNeverHitPath,
        "unknownRoot",
        "options.unknownOption",
        "options.filter.unknownFilter"
    ];

    [Test]
    public void EndpointCarriesRouteRolesAndTheExpensiveRateLimit()
    {
        MethodInfo endpoint = Endpoint();
        string roles = endpoint.GetCustomAttribute<AuthorizeAttribute>()?.Roles ?? "";
        List<string> allowedRoles = [.. roles.Split(',', StringSplitOptions.TrimEntries)];

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.GetCustomAttribute<HttpPostAttribute>()?.Template, Is.EqualTo("GetRuleLastHits"));
            Assert.That(allowedRoles, Is.EquivalentTo(kAllowedRoles));
            Assert.That(endpoint.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName, Is.EqualTo(ApiRateLimiting.kExpensivePolicy));
            Assert.That(endpoint.GetCustomAttribute<RequestSizeLimitAttribute>(), Is.Not.Null);
            Assert.That(endpoint.GetCustomAttribute<DisableRateLimitingAttribute>(), Is.Null);
            Assert.That(endpoint.GetCustomAttribute<AggregatedValidationErrorsAttribute>(), Is.Not.Null);
        });
    }

    [Test]
    public void EndpointDeniesRolesWithoutRuleReportingAccess()
    {
        string roles = Endpoint().GetCustomAttribute<AuthorizeAttribute>()?.Roles ?? "";
        List<string> allowedRoles = [.. roles.Split(',', StringSplitOptions.TrimEntries)];

        Assert.That(allowedRoles.Intersect(kDeniedRoles), Is.Empty);
    }

    [Test]
    public async Task DefaultOptionsReturnOnePageOfTheMaximumSizeWithoutFilter()
    {
        RuleHitApiConnection api = CreateApi();
        RuleController controller = CreateController(api, Principal(kAdminRole));
        GetRuleLastHitsRequest request = Deserialize("{\"managementId\":5,\"options\":null}");

        GetRuleLastHitsResponse response = OkResponse(await controller.GetRuleLastHits(request));

        Assert.Multiple(() =>
        {
            Assert.That(request.Options.Filter, Is.Null);
            Assert.That(request.Options.Limit, Is.Null);
            Assert.That(request.Options.Offset, Is.Null);
            Assert.That(api.Variable("where"), Is.EqualTo(kBaseWhere));
            Assert.That(api.Variable("hitWhere"), Is.EqualTo("{}"));
            Assert.That(api.Variable("limit"), Is.EqualTo((kMaxLimit + 1).ToString()));
            Assert.That(api.Variable("offset"), Is.EqualTo("0"));
            Assert.That(api.Variable("gatewayLimit"), Is.EqualTo((RuleLastHitResponse.kMaxGatewaysPerRule + 1).ToString()));
            Assert.That(response.Rules.Select(rule => rule.RuleUid), Is.EqualTo(kBothRuleUids));
            Assert.That(HasMoreHeader(controller), Is.EqualTo("false"));
        });
    }

    [Test]
    public async Task ResponseHoldsTheAggregatedAndThePerGatewayLastHitInUtc()
    {
        RuleController controller = CreateController(CreateApi(), Principal(kAdminRole));

        GetRuleLastHitsResponse response = OkResponse(await controller.GetRuleLastHits(Deserialize("{\"managementId\":5}")));

        RuleLastHitResponse hitRule = response.Rules[0];
        RuleLastHitResponse neverHitRule = response.Rules[1];
        Assert.Multiple(() =>
        {
            Assert.That(hitRule.MgmId, Is.EqualTo(kOwnManagementId));
            Assert.That(hitRule.RuleName, Is.EqualTo(kFirstRuleName));
            Assert.That(hitRule.LastHit, Is.EqualTo(kOwnGatewayLastHit));
            Assert.That(hitRule.LastHit?.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(hitRule.Gateways.Select(gateway => gateway.DevId), Is.EqualTo(kBothGateways));
            Assert.That(hitRule.Gateways[1].GatewayName, Is.EqualTo(kForeignGatewayName));
            Assert.That(hitRule.Gateways[1].LastHit, Is.EqualTo(kForeignGatewayLastHit));
            Assert.That(hitRule.GatewaysTruncated, Is.False);
            Assert.That(neverHitRule.LastHit, Is.Null);
            Assert.That(neverHitRule.RuleName, Is.Empty);
            Assert.That(neverHitRule.Gateways, Is.Empty);
            Assert.That(JsonSerializer.Serialize(neverHitRule), Does.Contain("\"lastHit\":null"));
        });
    }

    [TestCase("\"mgmId\":5", "{\"mgm_id\":{\"_eq\":5}},{\"removed\":{\"_is_null\":true}},{\"mgm_id\":{\"_eq\":5}}")]
    [TestCase("\"ruleUid\":[\"uid-1\",\"uid-2\",\"uid-1\"]", "{\"rule_uid\":{\"_in\":[\"uid-1\",\"uid-2\"]}}")]
    [TestCase("\"ruleName\":\"web\"", "{\"rules\":{\"_and\":[{\"rule_name\":{\"_ilike\":\"%web%\"}},{\"removed\":{\"_is_null\":true}}]}}")]
    [TestCase("\"lastHitAfter\":\"2026-01-31T01:00:00+01:00\"", "{\"rule_last_hit\":{\"_gte\":\"2026-01-31T00:00:00.000000\"}}")]
    [TestCase("\"lastHitBefore\":\"2026-01-31T00:00:00Z\"", "{\"rule_last_hit\":{\"_lt\":\"2026-01-31T00:00:00.000000\"}}")]
    [TestCase("\"neverHit\":true", "{\"rule_last_hit\":{\"_is_null\":true}}")]
    [TestCase("\"neverHit\":false", "{\"rule_last_hit\":{\"_is_null\":false}}")]
    [TestCase("\"devId\":51", "{\"rule_hits\":{\"dev_id\":{\"_eq\":51}}}")]
    [TestCase("\"gatewayName\":\"gw-*\"", "{\"rule_hits\":{\"device\":{\"dev_name\":{\"_ilike\":\"gw-%\"}}}}")]
    public async Task EveryFilterKeyRestrictsTheRuleQuery(string filterJson, string expectedPredicate)
    {
        RuleHitApiConnection api = CreateApi();
        RuleController controller = CreateController(api, Principal(kAdminRole));

        ActionResult<GetRuleLastHitsResponse> result = await controller.GetRuleLastHits(
            Deserialize($"{{\"managementId\":5,\"options\":{{\"filter\":{{{filterJson}}}}}}}"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            Assert.That(api.Variable("where"), Does.Contain(expectedPredicate));
        });
    }

    [Test]
    public async Task GatewayFiltersAlsoRestrictTheListedGateways()
    {
        RuleHitApiConnection api = CreateApi();
        RuleController controller = CreateController(api, Principal(kAdminRole));

        await controller.GetRuleLastHits(Deserialize("{\"managementId\":5,\"options\":{\"filter\":{\"devId\":51,\"gatewayName\":\"own\"}}}"));

        Assert.That(api.Variable("hitWhere"),
            Is.EqualTo("{\"_and\":[{\"dev_id\":{\"_eq\":51}},{\"device\":{\"dev_name\":{\"_ilike\":\"%own%\"}}}]}"));
    }

    [Test]
    public async Task NullFilterKeysApplyNoRestriction()
    {
        RuleHitApiConnection api = CreateApi();
        RuleController controller = CreateController(api, Principal(kAdminRole));
        const string allKeysNull = "{\"managementId\":5,\"options\":{\"limit\":null,\"offset\":null,\"filter\":{\"mgmId\":null,\"ruleUid\":null,"
            + "\"ruleName\":null,\"lastHitAfter\":null,\"lastHitBefore\":null,\"neverHit\":null,\"devId\":null,\"gatewayName\":null}}}";

        ActionResult<GetRuleLastHitsResponse> result = await controller.GetRuleLastHits(Deserialize(allKeysNull));

        Assert.Multiple(() =>
        {
            Assert.That(OkResponse(result).Rules, Has.Count.EqualTo(2));
            Assert.That(api.Variable("where"), Is.EqualTo(kBaseWhere));
            Assert.That(api.Variable("hitWhere"), Is.EqualTo("{}"));
        });
    }

    [Test]
    public async Task LimitAndOffsetPageTheRulesAndSetTheHasMoreHeader()
    {
        RuleController firstPageController = CreateController(CreateApi(), Principal(kAdminRole));
        RuleController lastPageController = CreateController(CreateApi(), Principal(kAdminRole));

        ActionResult<GetRuleLastHitsResponse> firstPage = await firstPageController.GetRuleLastHits(
            Deserialize("{\"managementId\":5,\"options\":{\"limit\":1}}"));
        ActionResult<GetRuleLastHitsResponse> lastPage = await lastPageController.GetRuleLastHits(
            Deserialize("{\"managementId\":5,\"options\":{\"limit\":1,\"offset\":1}}"));

        Assert.Multiple(() =>
        {
            Assert.That(OkResponse(firstPage).Rules.Select(rule => rule.RuleUid), Is.EqualTo(kFirstRuleUidOnly));
            Assert.That(HasMoreHeader(firstPageController), Is.EqualTo("true"));
            Assert.That(OkResponse(lastPage).Rules.Select(rule => rule.RuleUid), Is.EqualTo(kSecondRuleUidOnly));
            Assert.That(HasMoreHeader(lastPageController), Is.EqualTo("false"));
        });
    }

    [TestCase(1, 0, true)]
    [TestCase(kMaxLimit, 0, true)]
    [TestCase(null, null, true)]
    [TestCase(0, 0, false)]
    [TestCase(kMaxLimit + 1, 0, false)]
    [TestCase(1, -1, false)]
    public void ValidationChecksTheLimitAndOffsetBounds(int? limit, int? offset, bool isValid)
    {
        GetRuleLastHitsRequest request = new()
        {
            ManagementId = kOwnManagementId,
            Options = new GetRuleLastHitsOptions { Limit = limit, Offset = offset }
        };

        Assert.That(GetRuleLastHitsRequestValidator.Validate(request, kMaxLimit).Errors, isValid ? Is.Empty : Is.Not.Empty);
    }

    [Test]
    public async Task MaximumPageSizeComesFromTheConfigurationAndIsNotClamped()
    {
        SimulatedGlobalConfig globalConfig = new() { MaxRestResponseObjectLimit = GlobalConst.kMinRestResponseObjectLimit };
        RuleHitApiConnection api = CreateApi();
        RuleController controller = CreateController(api, Principal(kAdminRole), globalConfig);

        ActionResult<GetRuleLastHitsResponse> result = await controller.GetRuleLastHits(
            Deserialize($"{{\"managementId\":5,\"options\":{{\"limit\":{GlobalConst.kMinRestResponseObjectLimit + 1}}}}}"));

        RequestValidationErrorResponse errors = BadRequestResponse(result);
        Assert.Multiple(() =>
        {
            Assert.That(errors.Errors.Single().Path, Is.EqualTo(GetRuleLastHitsValidationSchema.kLimitPath));
            Assert.That(errors.Errors.Single().Message, Does.Contain($"between 1 and {GlobalConst.kMinRestResponseObjectLimit}"));
            Assert.That(api.QueryCount, Is.Zero);
        });
    }

    [Test]
    public async Task ValidationReportsEveryErrorTogether()
    {
        string tooLong = new('a', RuleLastHitFilter.kMaxFilterTextLength + 1);
        string json = "{\"unknownRoot\":1,\"options\":{\"unknownOption\":1,\"limit\":0,\"offset\":-1,\"filter\":{\"unknownFilter\":1,"
            + $"\"mgmId\":0,\"devId\":-1,\"ruleUid\":[\"ok\",\"{tooLong}\",\"\"],\"ruleName\":\"{tooLong}\",\"gatewayName\":\"{tooLong}\","
            + "\"lastHitAfter\":\"2026-02-01T00:00:00Z\",\"lastHitBefore\":\"2026-01-01T00:00:00Z\",\"neverHit\":true}}}";
        RuleHitApiConnection api = CreateApi();
        RuleController controller = CreateController(api, Principal(kAdminRole));

        RequestValidationErrorResponse errors = BadRequestResponse(await controller.GetRuleLastHits(Deserialize(json)));

        Assert.Multiple(() =>
        {
            Assert.That(errors.Errors.Select(error => error.Path), Is.EquivalentTo(kAllInvalidPaths));
            Assert.That(api.QueryCount, Is.Zero);
        });
    }

    [Test]
    public void ValidationAcceptsTheLongestTextAndRejectsAMissingBodyOrManagement()
    {
        string longest = new('a', RuleLastHitFilter.kMaxFilterTextLength);
        GetRuleLastHitsRequest longestTexts = new()
        {
            ManagementId = kOwnManagementId,
            Options = new GetRuleLastHitsOptions { Filter = new RuleLastHitFilter { RuleUid = Enumerable.Repeat(longest, RuleLastHitFilter.kMaxFilterValues).ToList(), RuleName = longest, GatewayName = longest } }
        };
        RequestValidationErrorResponse bodyErrors = GetRuleLastHitsRequestValidator.Validate(null, kMaxLimit);
        RequestValidationErrorResponse zeroErrors = GetRuleLastHitsRequestValidator.Validate(new GetRuleLastHitsRequest { ManagementId = 0 }, kMaxLimit);
        RequestValidationErrorResponse missingErrors = GetRuleLastHitsRequestValidator.Validate(new GetRuleLastHitsRequest(), kMaxLimit);

        Assert.Multiple(() =>
        {
            Assert.That(GetRuleLastHitsRequestValidator.Validate(longestTexts, kMaxLimit).Errors, Is.Empty);
            Assert.That(bodyErrors.Errors.Single().Path, Is.Empty);
            Assert.That(bodyErrors.Errors.Single().Message, Does.Contain("requires a request body"));
            Assert.That(zeroErrors.Errors.Single().Message, Does.Contain("greater than 0"));
            Assert.That(missingErrors.Errors.Single().Message, Does.Contain("is required"));
            Assert.That(GetRuleLastHitsValidationSchema.DescribeFilterKey("unknown"), Is.Empty);
        });
    }

    [TestCase(0)]
    [TestCase(RuleLastHitFilter.kMaxFilterValues + 1)]
    public void ValidationRejectsAnEmptyOrOversizedRuleUidList(int count)
    {
        GetRuleLastHitsRequest request = new()
        {
            ManagementId = kOwnManagementId,
            Options = new GetRuleLastHitsOptions { Filter = new RuleLastHitFilter { RuleUid = Enumerable.Repeat(kFirstRuleUid, count).ToList() } }
        };

        RequestValidationError error = GetRuleLastHitsRequestValidator.Validate(request, kMaxLimit).Errors.Single();

        Assert.Multiple(() =>
        {
            Assert.That(error.Path, Is.EqualTo(GetRuleLastHitsValidationSchema.kFilterRuleUidPath));
            Assert.That(error.Message, Does.Contain($"between 1 and {RuleLastHitFilter.kMaxFilterValues}"));
        });
    }

    [Test]
    public async Task TenantRestrictedCallerGetsNoDataOfAForeignManagement()
    {
        RuleHitApiConnection api = CreateApi();
        RuleController controller = CreateController(api, TenantReporter(kRestrictedTenantId));

        ActionResult<GetRuleLastHitsResponse> result = await controller.GetRuleLastHits(
            new GetRuleLastHitsRequest { ManagementId = kForeignManagementId });

        Assert.Multiple(() =>
        {
            Assert.That(OkResponse(result).Rules, Is.Empty);
            Assert.That(HasMoreHeader(controller), Is.EqualTo("false"));
            Assert.That(api.QueryCount, Is.Zero, "a management hidden from the caller must not be queried");
        });
    }

    [Test]
    public async Task TenantRestrictedCallerGetsItsOwnManagementWithVisibleGatewaysOnly()
    {
        RuleHitApiConnection api = CreateApi();
        RuleController controller = CreateController(api, TenantReporter(kRestrictedTenantId));

        GetRuleLastHitsResponse response = OkResponse(await controller.GetRuleLastHits(
            new GetRuleLastHitsRequest { ManagementId = kOwnManagementId }));

        Assert.Multiple(() =>
        {
            Assert.That(response.Rules.Select(rule => rule.RuleUid), Is.EqualTo(kBothRuleUids));
            Assert.That(response.Rules[0].Gateways.Select(gateway => gateway.DevId), Is.EqualTo(kOwnGatewayOnly));
            Assert.That(response.Rules[0].Gateways[0].GatewayName, Is.EqualTo(kOwnGatewayName));
            Assert.That(response.Rules[0].RuleName, Is.Empty, "rule relevance per tenant cannot be evaluated here");
            Assert.That(api.Variable("hitWhere"), Is.EqualTo("{\"dev_id\":{\"_in\":[51]}}"));
        });
    }

    [Test]
    public async Task TenantRestrictedCallerCannotReachAForeignGatewayThroughTheGatewayFilter()
    {
        RuleHitApiConnection api = CreateApi();
        RuleController controller = CreateController(api, TenantReporter(kRestrictedTenantId));

        await controller.GetRuleLastHits(new GetRuleLastHitsRequest
        {
            ManagementId = kOwnManagementId,
            Options = new GetRuleLastHitsOptions { Filter = new RuleLastHitFilter { DevId = kForeignGatewayId } }
        });

        const string expectedHitWhere = "{\"_and\":[{\"dev_id\":{\"_in\":[51]}},{\"dev_id\":{\"_eq\":52}}]}";
        Assert.Multiple(() =>
        {
            Assert.That(api.Variable("hitWhere"), Is.EqualTo(expectedHitWhere));
            Assert.That(api.Variable("where"), Does.Contain($"{{\"rule_hits\":{expectedHitWhere}}}"));
        });
    }

    [Test]
    public async Task TenantRestrictedCallerCannotProbeRuleNamesThroughTheFilter()
    {
        RuleHitApiConnection api = CreateApi();
        RuleController controller = CreateController(api, TenantReporter(kRestrictedTenantId));

        ActionResult<GetRuleLastHitsResponse> result = await controller.GetRuleLastHits(new GetRuleLastHitsRequest
        {
            ManagementId = kOwnManagementId,
            Options = new GetRuleLastHitsOptions { Filter = new RuleLastHitFilter { RuleName = "web" } }
        });

        Assert.Multiple(() =>
        {
            Assert.That(OkResponse(result).Rules, Is.Empty);
            Assert.That(api.QueryCount, Is.Zero);
        });
    }

    [Test]
    public async Task CallerWithoutVisibilityClaimsGetsNothing()
    {
        RuleHitApiConnection api = CreateApi();
        RuleController controller = CreateController(api, Principal(kReporterRole));

        ActionResult<GetRuleLastHitsResponse> result = await controller.GetRuleLastHits(
            new GetRuleLastHitsRequest { ManagementId = kOwnManagementId });

        Assert.Multiple(() =>
        {
            Assert.That(OkResponse(result).Rules, Is.Empty);
            Assert.That(api.QueryCount, Is.Zero);
        });
    }

    [Test]
    public async Task Tenant0ReporterGetsRuleNamesWithinItsVisibility()
    {
        RuleController controller = CreateController(CreateApi(), TenantReporter(GlobalConst.kTenant0Id));

        GetRuleLastHitsResponse response = OkResponse(await controller.GetRuleLastHits(
            new GetRuleLastHitsRequest { ManagementId = kOwnManagementId }));

        Assert.Multiple(() =>
        {
            Assert.That(response.Rules[0].RuleName, Is.EqualTo(kFirstRuleName));
            Assert.That(response.Rules[0].Gateways.Select(gateway => gateway.DevId), Is.EqualTo(kOwnGatewayOnly));
        });
    }

    [Test]
    public void AdminAuditorAndReporterViewAllSeeEverything()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RuleLastHitScope.FromUser(Principal(kAdminRole)), Is.SameAs(RuleLastHitScope.kUnrestricted));
            Assert.That(RuleLastHitScope.FromUser(Principal(kAuditorRole)), Is.SameAs(RuleLastHitScope.kUnrestricted));
            Assert.That(RuleLastHitScope.FromUser(Principal(kReporterViewAllRole)), Is.SameAs(RuleLastHitScope.kUnrestricted));
            Assert.That(RuleLastHitScope.kUnrestricted.SeesManagement(kForeignManagementId), Is.True);
            Assert.That(RuleLastHitScope.FromUser(TenantReporter(kRestrictedTenantId)).SeesManagement(kForeignManagementId), Is.False);
        });
    }

    [Test]
    public async Task GatewaysBeyondTheCapAreCutAndSignalled()
    {
        RuleHitApiConnection api = new();
        RuleLastHitRecord rule = new() { Id = 1, MgmId = kOwnManagementId, RuleUid = kFirstRuleUid, LastHit = kFirstRuleLastHit };
        for (int devId = 1; devId <= RuleLastHitResponse.kMaxGatewaysPerRule + 1; devId++)
        {
            rule.Hits.Add(new RuleGatewayHitRecord { DevId = devId, LastHit = kOwnGatewayLastHit });
        }
        api.Records.Add(rule);
        RuleController controller = CreateController(api, Principal(kAdminRole));

        GetRuleLastHitsResponse response = OkResponse(await controller.GetRuleLastHits(
            new GetRuleLastHitsRequest { ManagementId = kOwnManagementId }));

        Assert.Multiple(() =>
        {
            Assert.That(response.Rules.Single().Gateways, Has.Count.EqualTo(RuleLastHitResponse.kMaxGatewaysPerRule));
            Assert.That(response.Rules.Single().GatewaysTruncated, Is.True);
            Assert.That(response.Rules.Single().Gateways[0].GatewayName, Is.Empty);
        });
    }

    [Test]
    public async Task ApiFailureAnswers500()
    {
        RuleHitApiConnection api = CreateApi();
        api.Fail = true;
        RuleController controller = CreateController(api, Principal(kAdminRole));

        ActionResult<GetRuleLastHitsResponse> result = await controller.GetRuleLastHits(
            new GetRuleLastHitsRequest { ManagementId = kOwnManagementId });

        Assert.That((result.Result as ObjectResult)?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    private static MethodInfo Endpoint()
    {
        return typeof(RuleController).GetMethod(nameof(RuleController.GetRuleLastHits))
            ?? throw new MissingMethodException(nameof(RuleController), nameof(RuleController.GetRuleLastHits));
    }

    private static GetRuleLastHitsRequest Deserialize(string json)
    {
        return JsonSerializer.Deserialize<GetRuleLastHitsRequest>(json)
            ?? throw new JsonException("request expected");
    }

    private static RuleHitApiConnection CreateApi()
    {
        RuleHitApiConnection api = new();
        RuleLastHitRecord hitRule = new() { Id = 1, MgmId = kOwnManagementId, RuleUid = kFirstRuleUid, LastHit = kFirstRuleLastHit };
        hitRule.Rules.Add(new RuleLastHitRuleRecord { Name = kFirstRuleName });
        hitRule.Hits.Add(new RuleGatewayHitRecord { DevId = kOwnGatewayId, LastHit = kOwnGatewayLastHit, Device = new RuleHitGatewayRecord { Name = kOwnGatewayName } });
        hitRule.Hits.Add(new RuleGatewayHitRecord { DevId = kForeignGatewayId, LastHit = kForeignGatewayLastHit, Device = new RuleHitGatewayRecord { Name = kForeignGatewayName } });
        api.Records.Add(hitRule);
        api.Records.Add(new RuleLastHitRecord { Id = 2, MgmId = kOwnManagementId, RuleUid = kSecondRuleUid });
        return api;
    }

    private static RuleController CreateController(RuleHitApiConnection api, ClaimsPrincipal user, SimulatedGlobalConfig? globalConfig = null)
    {
        return new RuleController(api, globalConfig ?? new SimulatedGlobalConfig())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static ClaimsPrincipal TenantReporter(int tenantId)
    {
        return Principal(kReporterRole, $"{{ {kOwnManagementId} }}", $"{{ {kOwnGatewayId} }}", tenantId);
    }

    private static ClaimsPrincipal Principal(List<string> roles, string? visibleManagements = null, string? visibleDevices = null, int? tenantId = null)
    {
        List<Claim> claims = [.. roles.Select(role => new Claim(ClaimTypes.Role, role))];
        if (visibleManagements != null)
        {
            claims.Add(new Claim(kVisibleManagementsClaim, visibleManagements));
        }
        if (visibleDevices != null)
        {
            claims.Add(new Claim(kVisibleDevicesClaim, visibleDevices));
        }
        if (tenantId != null)
        {
            claims.Add(new Claim(kTenantIdClaim, tenantId.Value.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, ClaimTypes.Role));
    }

    private static string HasMoreHeader(RuleController controller)
    {
        return controller.Response.Headers[ListPaging.kHasMoreHeader].ToString();
    }

    private static GetRuleLastHitsResponse OkResponse(ActionResult<GetRuleLastHitsResponse> result)
    {
        return (GetRuleLastHitsResponse)((OkObjectResult)result.Result!).Value!;
    }

    private static RequestValidationErrorResponse BadRequestResponse(ActionResult<GetRuleLastHitsResponse> result)
    {
        return (RequestValidationErrorResponse)((BadRequestObjectResult)result.Result!).Value!;
    }

    /// <summary>
    /// Answers the rule last hit query from memory. It applies what the tests observe in the result - paging and
    /// the gateway visibility of <c>hitWhere</c> - and records the variables, so the rule filters are asserted on
    /// the where expression sent to the API.
    /// </summary>
    private sealed class RuleHitApiConnection : SimulatedApiConnection
    {
        private Dictionary<string, object> lastVariables = [];

        public List<RuleLastHitRecord> Records { get; } = [];

        public int QueryCount { get; private set; }

        public bool Fail { get; set; }

        public string Variable(string name)
        {
            return JsonSerializer.Serialize(lastVariables[name]);
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (Fail)
            {
                throw new InvalidOperationException("simulated API failure");
            }
            if (query != RuleQueries.getRuleLastHits)
            {
                throw new NotSupportedException("unexpected query");
            }

            QueryCount++;
            lastVariables = (Dictionary<string, object>)variables!;
            HashSet<int>? visibleDeviceIds = ReadVisibleDeviceIds();
            List<RuleLastHitRecord> page = [.. Records
                .Skip((int)lastVariables["offset"])
                .Take((int)lastVariables["limit"])
                .Select(record => RestrictHits(record, visibleDeviceIds, (int)lastVariables["gatewayLimit"]))];
            return Task.FromResult((QueryResponseType)(object)page);
        }

        private HashSet<int>? ReadVisibleDeviceIds()
        {
            using JsonDocument hitWhere = JsonDocument.Parse(Variable("hitWhere"));
            JsonElement root = hitWhere.RootElement;
            if (root.TryGetProperty("_and", out JsonElement predicates))
            {
                root = predicates[0];
            }
            return root.TryGetProperty("dev_id", out JsonElement deviceId) && deviceId.TryGetProperty("_in", out JsonElement visibleIds)
                ? [.. visibleIds.EnumerateArray().Select(id => id.GetInt32())]
                : null;
        }

        private static RuleLastHitRecord RestrictHits(RuleLastHitRecord record, HashSet<int>? visibleDeviceIds, int gatewayLimit)
        {
            return new RuleLastHitRecord
            {
                Id = record.Id,
                MgmId = record.MgmId,
                RuleUid = record.RuleUid,
                LastHit = record.LastHit,
                Rules = record.Rules,
                Hits = [.. record.Hits.Where(hit => visibleDeviceIds == null || visibleDeviceIds.Contains(hit.DevId)).Take(gatewayLimit)]
            };
        }
    }
}
