using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the getAutoPlanSettings endpoint: route and authorization, the inherited settings, filtering, paging, the
/// request defaults and the aggregated validation.
/// </summary>
[TestFixture]
internal class WorkflowAutoPlanSettingsControllerTest
{
    private const long kGlobalNodeId = 1;
    private const long kDeviceTypeNodeId = 2;
    private const long kManagementNodeId = 3;
    private const long kGatewayNodeId = 4;
    private const int kDeviceTypeId = 9;
    private const int kManagementId = 20;
    private const int kFirstGatewayId = 1;
    private const int kSecondGatewayId = 2;

    private static readonly List<int> kBothGateways = [kFirstGatewayId, kSecondGatewayId];
    private static readonly List<int> kManagement = [kManagementId];
    private static readonly List<int> kInvalidIds = [5, 0, -1];
    private static readonly List<int?> kAllIds = [null, kManagementId, kFirstGatewayId, kSecondGatewayId];
    private static readonly List<int?> kFirstGatewayOnly = [kFirstGatewayId];
    private static readonly List<int?> kGlobalOnly = [null];
    private static readonly List<int?> kLastSettingOnly = [kSecondGatewayId];
    private static readonly List<string> kPlannerRole = [Roles.Planner];
    private static readonly List<string> kAllInvalidKeyPaths =
    [
        GetAutoPlanSettingsValidationSchema.kDeviceIdsPath,
        GetAutoPlanSettingsValidationSchema.kManagementIdsPath + "[1]",
        GetAutoPlanSettingsValidationSchema.kManagementIdsPath + "[2]",
        GetAutoPlanSettingsValidationSchema.kLimitPath,
        GetAutoPlanSettingsValidationSchema.kOffsetPath,
        GetAutoPlanSettingsValidationSchema.kFilterScopeTypePath,
        "unknownRoot",
        "options.unknownOption",
        "options.filter.unknownFilter"
    ];

    [Test]
    public void GetAutoPlanSettingsUsesWorkflowRouteRolesAndExpensivePolicy()
    {
        MethodInfo endpoint = typeof(WorkflowImplTaskTargetController).GetMethod(nameof(WorkflowImplTaskTargetController.GetAutoPlanSettings))
            ?? throw new MissingMethodException(nameof(WorkflowImplTaskTargetController), nameof(WorkflowImplTaskTargetController.GetAutoPlanSettings));

        Assert.Multiple(() =>
        {
            Assert.That(endpoint.GetCustomAttribute<HttpPostAttribute>()?.Template, Is.EqualTo("getAutoPlanSettings"));
            Assert.That(endpoint.GetCustomAttribute<AuthorizeAttribute>()?.Roles, Is.EqualTo($"{Roles.Admin}, {Roles.WorkflowRolesList}, {Roles.Modeller}"));
            Assert.That(endpoint.GetCustomAttribute<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName,
                Is.EqualTo(ApiRateLimiting.kExpensivePolicy));
            Assert.That(endpoint.GetCustomAttribute<RequestSizeLimitAttribute>(), Is.Not.Null);
        });
    }

    [Test]
    public async Task GetAutoPlanSettingsReturnsInheritedSettingsOfAllRequestedLevels()
    {
        WorkflowImplTaskTargetController controller = CreateController(CreateApi());

        ActionResult<GetAutoPlanSettingsResponse> result = await controller.GetAutoPlanSettings(
            new GetAutoPlanSettingsRequest { DeviceIds = kBothGateways, ManagementIds = kManagement });

        GetAutoPlanSettingsResponse response = OkResponse(result);
        Assert.Multiple(() =>
        {
            Assert.That(response.Settings.Select(setting => setting.Id), Is.EqualTo(kAllIds));
            Assert.That(response.Settings[0].ScopeType, Is.EqualTo(AutoPlanScopeTypes.kGlobal));
            Assert.That(response.Settings[1].AutoPlanImplementation, Is.False);
            Assert.That(response.Settings[2].AutoPlanImplementationForRules, Is.True);
            Assert.That(response.Settings[3].AutoPlanImplementationForRules, Is.False);
            Assert.That(HasMoreHeader(controller), Is.EqualTo("false"));
        });
    }

    [Test]
    public async Task GetAutoPlanSettingsAppliesEveryFilter()
    {
        WorkflowImplTaskTargetController controller = CreateController(CreateApi());

        ActionResult<GetAutoPlanSettingsResponse> byRules = await controller.GetAutoPlanSettings(Request(new AutoPlanSettingsFilter
        {
            ScopeType = AutoPlanScopeTypes.kGateway,
            AutoPlanImplementationForRules = true
        }));
        ActionResult<GetAutoPlanSettingsResponse> byIdAndAutoPlan = await controller.GetAutoPlanSettings(Request(new AutoPlanSettingsFilter
        {
            Id = null,
            AutoPlanImplementation = true
        }));

        Assert.Multiple(() =>
        {
            Assert.That(OkResponse(byRules).Settings.Select(setting => setting.Id), Is.EqualTo(kFirstGatewayOnly));
            Assert.That(OkResponse(byIdAndAutoPlan).Settings.Select(setting => setting.Id), Is.EqualTo(kGlobalOnly));
        });
    }

    [Test]
    public async Task GetAutoPlanSettingsPagesTheSettingsAndSetsTheHasMoreHeader()
    {
        WorkflowImplTaskTargetController firstPageController = CreateController(CreateApi());
        WorkflowImplTaskTargetController lastPageController = CreateController(CreateApi());

        ActionResult<GetAutoPlanSettingsResponse> firstPage = await firstPageController.GetAutoPlanSettings(Paged(0));
        ActionResult<GetAutoPlanSettingsResponse> lastPage = await lastPageController.GetAutoPlanSettings(Paged(3));

        Assert.Multiple(() =>
        {
            Assert.That(OkResponse(firstPage).Settings.Select(setting => setting.Id), Is.EqualTo(kGlobalOnly));
            Assert.That(HasMoreHeader(firstPageController), Is.EqualTo("true"));
            Assert.That(OkResponse(lastPage).Settings.Select(setting => setting.Id), Is.EqualTo(kLastSettingOnly));
            Assert.That(HasMoreHeader(lastPageController), Is.EqualTo("false"));
        });
    }

    [Test]
    public void RequestDefaultsApplyToOmittedAndNullKeys()
    {
        GetAutoPlanSettingsRequest omitted = JsonSerializer.Deserialize<GetAutoPlanSettingsRequest>("{}")!;
        GetAutoPlanSettingsRequest nulls = JsonSerializer.Deserialize<GetAutoPlanSettingsRequest>(
            "{\"deviceIds\":null,\"managementIds\":null,\"options\":null}")!;

        Assert.Multiple(() =>
        {
            Assert.That(omitted.DeviceIds, Is.Empty);
            Assert.That(omitted.Options.Limit, Is.EqualTo(GetAutoPlanSettingsOptions.kMaxLimit));
            Assert.That(nulls.ManagementIds, Is.Empty);
            Assert.That(nulls.Options.Filter, Is.Null);
            Assert.That(GetAutoPlanSettingsRequestValidator.Validate(nulls).Errors, Is.Empty);
        });
    }

    [Test]
    public async Task GetAutoPlanSettingsReportsEveryInvalidKeyAtOnce()
    {
        GetAutoPlanSettingsRequest request = JsonSerializer.Deserialize<GetAutoPlanSettingsRequest>(
            "{\"unknownRoot\":1,\"options\":{\"limit\":0,\"offset\":-1,\"unknownOption\":1,\"filter\":{\"scopeType\":\"device\",\"unknownFilter\":1}}}")!;
        request.DeviceIds = [.. Enumerable.Range(1, GetAutoPlanSettingsRequest.kMaxIds + 1)];
        request.ManagementIds = kInvalidIds;
        WorkflowImplTaskTargetController controller = CreateController(CreateApi());

        ActionResult<GetAutoPlanSettingsResponse> result = await controller.GetAutoPlanSettings(request);

        RequestValidationErrorResponse errors = (RequestValidationErrorResponse)((BadRequestObjectResult)result.Result!).Value!;
        Assert.That(errors.Errors.Select(error => error.Path), Is.EquivalentTo(kAllInvalidKeyPaths));
    }

    [Test]
    public void ValidatorRejectsAMissingBody()
    {
        Assert.That(GetAutoPlanSettingsRequestValidator.Validate(null).Errors, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetAutoPlanSettingsAnswers500WhenTheSettingsCannotBeRead()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        api.FailingQuery = DeviceSpecificSettingsQueries.getNodesByNaturalKeys;
        WorkflowImplTaskTargetController controller = CreateController(api);

        ActionResult<GetAutoPlanSettingsResponse> result = await controller.GetAutoPlanSettings(new GetAutoPlanSettingsRequest());

        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    private static GetAutoPlanSettingsRequest Request(AutoPlanSettingsFilter filter)
    {
        return new GetAutoPlanSettingsRequest
        {
            DeviceIds = kBothGateways,
            ManagementIds = kManagement,
            Options = new GetAutoPlanSettingsOptions { Filter = filter }
        };
    }

    private static GetAutoPlanSettingsRequest Paged(int offset)
    {
        return new GetAutoPlanSettingsRequest
        {
            DeviceIds = kBothGateways,
            ManagementIds = kManagement,
            Options = new GetAutoPlanSettingsOptions { Limit = 1, Offset = offset }
        };
    }

    private static InMemoryDeviceSpecificSettingsApiConnection CreateApi()
    {
        DeviceType deviceType = new() { Id = kDeviceTypeId, Name = "Check Point", Version = "R8x" };
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        api.Managements.Add(new Management
        {
            Id = kManagementId,
            Name = "mgm",
            DeviceType = deviceType,
            Devices = [new Device { Id = kFirstGatewayId, Name = "gw-1" }, new Device { Id = kSecondGatewayId, Name = "gw-2" }]
        });
        api.AddNode(kGlobalNodeId, DeviceSpecificScopeType.Global, DeviceSpecificScopePath.GlobalObjectKey, displayName: "Global");
        api.AddNode(kDeviceTypeNodeId, DeviceSpecificScopeType.DeviceType, kDeviceTypeId.ToString(), kGlobalNodeId);
        api.AddNode(kManagementNodeId, DeviceSpecificScopeType.Management, kManagementId.ToString(), kDeviceTypeNodeId);
        api.AddNode(kGatewayNodeId, DeviceSpecificScopeType.Gateway, kFirstGatewayId.ToString(), kManagementNodeId);
        api.SetValue(kManagementNodeId, DeviceSpecificSettingKeys.AutoPlanImplementation, false);
        api.SetValue(kGatewayNodeId, DeviceSpecificSettingKeys.AutoPlanImplementationForRules, true);
        return api;
    }

    private static WorkflowImplTaskTargetController CreateController(InMemoryDeviceSpecificSettingsApiConnection api)
    {
        List<Claim> claims = [.. kPlannerRole.Select(role => new Claim(ClaimTypes.Role, role))];
        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test", ClaimTypes.Name, ClaimTypes.Role));
        return new WorkflowImplTaskTargetController(new ImplTaskTargetService(api))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static string HasMoreHeader(WorkflowImplTaskTargetController controller)
    {
        return controller.Response.Headers[ListPaging.kHasMoreHeader].ToString();
    }

    private static GetAutoPlanSettingsResponse OkResponse(ActionResult<GetAutoPlanSettingsResponse> result)
    {
        return (GetAutoPlanSettingsResponse)((OkObjectResult)result.Result!).Value!;
    }
}
