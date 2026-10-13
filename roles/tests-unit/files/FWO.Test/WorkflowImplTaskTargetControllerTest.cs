using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Provisioning;
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
/// Covers the getImplTaskTargets endpoint: its route and authorization, the restriction to the managements
/// visible to the caller, the request contract including defaults and aggregated validation, the response
/// filtering, and the shared client DTOs.
/// </summary>
[TestFixture]
internal class WorkflowImplTaskTargetControllerTest
{
    private const int kSuperManagementId = 20;
    private const int kFirstSubManagementId = 21;
    private const int kSecondSubManagementId = 22;
    private const long kGlobalNodeId = 1;
    private const string kFirstSubName = "domain-a";
    private const string kSecondSubName = "domain-b";
    private const string kVisibleManagementsClaim = "x-hasura-visible-managements";
    private const string kWholeHierarchyVisible = "{20,21,22}";

    private static readonly List<string> kControllerRoutes = ["api/workflow"];
    private static readonly List<int> kBothSubManagements = [kFirstSubManagementId, kSecondSubManagementId];
    private static readonly List<int> kSecondSubManagementOnly = [kSecondSubManagementId];
    private static readonly List<int> kFirstSubManagementOnly = [kFirstSubManagementId];
    private static readonly List<string> kAllPagingAndLengthErrorPaths =
    [
        GetImplTaskTargetsValidationSchema.kLimitPath,
        GetImplTaskTargetsValidationSchema.kOffsetPath,
        GetImplTaskTargetsValidationSchema.kFilterNamePath
    ];
    private static readonly List<string> kPlannerRole = [Roles.Planner];
    private static readonly List<string> kAdminRole = [Roles.Admin];
    private static readonly List<string> kAllInvalidKeyPaths =
    [
        GetImplTaskTargetsValidationSchema.kManagementIdPath,
        "unknownRoot",
        "options.unknownOption",
        "options.filter.unknownFilter"
    ];

    [Test]
    public void GetImplTaskTargetsUsesWorkflowRouteAndWorkflowRoles()
    {
        List<RouteAttribute> controllerRoutes = [.. typeof(WorkflowImplTaskTargetController).GetCustomAttributes<RouteAttribute>()];
        MethodInfo endpoint = typeof(WorkflowImplTaskTargetController).GetMethod(nameof(WorkflowImplTaskTargetController.GetImplTaskTargets))
            ?? throw new MissingMethodException(nameof(WorkflowImplTaskTargetController), nameof(WorkflowImplTaskTargetController.GetImplTaskTargets));

        Assert.Multiple(() =>
        {
            Assert.That(controllerRoutes.Select(route => route.Template), Is.EquivalentTo(kControllerRoutes));
            Assert.That(endpoint.GetCustomAttribute<HttpPostAttribute>()?.Template, Is.EqualTo("getImplTaskTargets"));
            Assert.That(endpoint.GetCustomAttribute<AuthorizeAttribute>()?.Roles, Is.EqualTo($"{Roles.Admin}, {Roles.WorkflowRolesList}, {Roles.Modeller}"));
        });
    }

    [Test]
    public async Task GetImplTaskTargetsReturnsEverySubManagementForAVisibleManagement()
    {
        WorkflowImplTaskTargetController controller = CreateController(CreateApi(), Principal(kPlannerRole, kWholeHierarchyVisible));

        ActionResult<GetImplTaskTargetsResponse> result = await controller.GetImplTaskTargets(new GetImplTaskTargetsRequest { ManagementId = kSuperManagementId });

        GetImplTaskTargetsResponse response = OkResponse(result);
        Assert.Multiple(() =>
        {
            Assert.That(response.Targets.Select(target => target.Id), Is.EqualTo(kBothSubManagements));
            Assert.That(response.Targets[0].Name, Is.EqualTo(kFirstSubName));
        });
    }

    [Test]
    public async Task GetImplTaskTargetsForbidsAManagementNotVisibleToTheCaller()
    {
        WorkflowImplTaskTargetController controller = CreateController(CreateApi(), Planner(kFirstSubManagementId));

        ActionResult<GetImplTaskTargetsResponse> result = await controller.GetImplTaskTargets(new GetImplTaskTargetsRequest { ManagementId = kSuperManagementId });

        Assert.That(result.Result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task GetImplTaskTargetsOmitsTheNamesOfTargetsNotVisibleToTheCaller()
    {
        WorkflowImplTaskTargetController controller = CreateController(CreateApi(), Planner(kFirstSubManagementId));

        ActionResult<GetImplTaskTargetsResponse> result = await controller.GetImplTaskTargets(new GetImplTaskTargetsRequest { ManagementId = kFirstSubManagementId });
        ActionResult<GetImplTaskTargetsResponse> byHiddenName = await controller.GetImplTaskTargets(new GetImplTaskTargetsRequest
        {
            ManagementId = kFirstSubManagementId,
            Options = new GetImplTaskTargetsOptions { Filter = new ImplTaskTargetFilter { Name = kSecondSubName } }
        });

        GetImplTaskTargetsResponse response = OkResponse(result);
        Assert.Multiple(() =>
        {
            Assert.That(response.Targets.Select(target => target.Id), Is.EqualTo(kBothSubManagements));
            Assert.That(response.Targets[0].Name, Is.EqualTo(kFirstSubName));
            Assert.That(response.Targets[1].Name, Is.Empty);
            Assert.That(OkResponse(byHiddenName).Targets, Is.Empty);
        });
    }

    [Test]
    public async Task GetImplTaskTargetsLetsTheAdminAskForAnyManagement()
    {
        WorkflowImplTaskTargetController controller = CreateController(CreateApi(), Principal(kAdminRole, ""));

        ActionResult<GetImplTaskTargetsResponse> result = await controller.GetImplTaskTargets(new GetImplTaskTargetsRequest { ManagementId = kSuperManagementId });

        GetImplTaskTargetsResponse response = OkResponse(result);
        Assert.Multiple(() =>
        {
            Assert.That(response.Targets.Select(target => target.Id), Is.EqualTo(kBothSubManagements));
            Assert.That(response.Targets[1].Name, Is.EqualTo(kSecondSubName));
        });
    }

    [Test]
    public async Task GetImplTaskTargetsUsesTheServiceSettingForServiceObjects()
    {
        WorkflowImplTaskTargetController controller = CreateController(CreateApi(), Planner(kSuperManagementId));

        ActionResult<GetImplTaskTargetsResponse> result = await controller.GetImplTaskTargets(
            new GetImplTaskTargetsRequest { ManagementId = kSuperManagementId, IsServiceObject = true });

        Assert.That(OkResponse(result).Targets.Single().Id, Is.EqualTo(kSuperManagementId));
    }

    [Test]
    public async Task GetImplTaskTargetsAppliesTheFilterToIdAndNameCaseInsensitively()
    {
        WorkflowImplTaskTargetController controller = CreateController(CreateApi(), Principal(kPlannerRole, kWholeHierarchyVisible));

        ActionResult<GetImplTaskTargetsResponse> byName = await controller.GetImplTaskTargets(new GetImplTaskTargetsRequest
        {
            ManagementId = kSuperManagementId,
            Options = new GetImplTaskTargetsOptions { Filter = new ImplTaskTargetFilter { Name = kSecondSubName.ToUpperInvariant() } }
        });
        ActionResult<GetImplTaskTargetsResponse> byId = await controller.GetImplTaskTargets(new GetImplTaskTargetsRequest
        {
            ManagementId = kSuperManagementId,
            Options = new GetImplTaskTargetsOptions { Filter = new ImplTaskTargetFilter { Id = kSecondSubManagementId } }
        });
        ActionResult<GetImplTaskTargetsResponse> unrestricted = await controller.GetImplTaskTargets(new GetImplTaskTargetsRequest
        {
            ManagementId = kSuperManagementId,
            Options = new GetImplTaskTargetsOptions { Filter = new ImplTaskTargetFilter() }
        });

        Assert.Multiple(() =>
        {
            Assert.That(OkResponse(byName).Targets.Select(target => target.Id), Is.EqualTo(kSecondSubManagementOnly));
            Assert.That(OkResponse(byId).Targets.Select(target => target.Id), Is.EqualTo(kSecondSubManagementOnly));
            Assert.That(OkResponse(unrestricted).Targets.Select(target => target.Id), Is.EqualTo(kBothSubManagements));
        });
    }

    [Test]
    public async Task GetImplTaskTargetsPagesTheOrderedTargetsAndSetsTheHasMoreHeader()
    {
        WorkflowImplTaskTargetController firstPageController = CreateController(CreateApi(), Principal(kPlannerRole, kWholeHierarchyVisible));
        WorkflowImplTaskTargetController lastPageController = CreateController(CreateApi(), Principal(kPlannerRole, kWholeHierarchyVisible));

        ActionResult<GetImplTaskTargetsResponse> firstPage = await firstPageController.GetImplTaskTargets(new GetImplTaskTargetsRequest
        {
            ManagementId = kSuperManagementId,
            Options = new GetImplTaskTargetsOptions { Limit = 1 }
        });
        ActionResult<GetImplTaskTargetsResponse> lastPage = await lastPageController.GetImplTaskTargets(new GetImplTaskTargetsRequest
        {
            ManagementId = kSuperManagementId,
            Options = new GetImplTaskTargetsOptions { Limit = 1, Offset = 1 }
        });

        Assert.Multiple(() =>
        {
            Assert.That(OkResponse(firstPage).Targets.Select(target => target.Id), Is.EqualTo(kFirstSubManagementOnly));
            Assert.That(HasMoreHeader(firstPageController), Is.EqualTo("true"));
            Assert.That(OkResponse(lastPage).Targets.Select(target => target.Id), Is.EqualTo(kSecondSubManagementOnly));
            Assert.That(HasMoreHeader(lastPageController), Is.EqualTo("false"));
        });
    }

    [Test]
    public async Task GetImplTaskTargetsOrdersTargetsWithoutNameLastSoTheOrderDoesNotRevealHiddenNames()
    {
        InMemoryProvisioningApiConnection api = CreateApi();
        api.Managements.Single(management => management.Id == kFirstSubManagementId).Name = "aa-hidden";
        WorkflowImplTaskTargetController controller = CreateController(api, Planner(kSecondSubManagementId));

        ActionResult<GetImplTaskTargetsResponse> result = await controller.GetImplTaskTargets(new GetImplTaskTargetsRequest { ManagementId = kSecondSubManagementId });

        GetImplTaskTargetsResponse response = OkResponse(result);
        Assert.Multiple(() =>
        {
            Assert.That(response.Targets[0].Id, Is.EqualTo(kSecondSubManagementId));
            Assert.That(response.Targets[1].Id, Is.EqualTo(kFirstSubManagementId));
            Assert.That(response.Targets[1].Name, Is.Empty);
        });
    }

    [Test]
    public async Task GetImplTaskTargetsAnswers500WhenTheResolutionFails()
    {
        InMemoryProvisioningApiConnection api = CreateApi();
        api.FailingQuery = FWO.Api.Client.Queries.DeviceQueries.getManagementHierarchy;
        WorkflowImplTaskTargetController controller = CreateController(api, Planner(kSuperManagementId));

        ActionResult<GetImplTaskTargetsResponse> result = await controller.GetImplTaskTargets(new GetImplTaskTargetsRequest { ManagementId = kSuperManagementId });

        Assert.That((result.Result as ObjectResult)?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    [Test]
    public void RequestDefaultsApplyForOmittedAndNullKeys()
    {
        GetImplTaskTargetsRequest request = JsonSerializer.Deserialize<GetImplTaskTargetsRequest>("{\"managementId\":20,\"options\":null}")!;

        Assert.Multiple(() =>
        {
            Assert.That(request.IsServiceObject, Is.False);
            Assert.That(request.Options, Is.Not.Null);
            Assert.That(request.Options.Filter, Is.Null);
            Assert.That(request.Options.Limit, Is.EqualTo(GetImplTaskTargetsOptions.kMaxLimit));
            Assert.That(request.Options.Offset, Is.Null);
            Assert.That(GetImplTaskTargetsRequestValidator.Validate(request).Errors, Is.Empty);
        });
    }

    [Test]
    public async Task ValidationReportsEveryErrorTogether()
    {
        GetImplTaskTargetsRequest request = JsonSerializer.Deserialize<GetImplTaskTargetsRequest>(
            "{\"unknownRoot\":1,\"options\":{\"unknownOption\":1,\"filter\":{\"unknownFilter\":1}}}")!;
        WorkflowImplTaskTargetController controller = CreateController(CreateApi(), Planner(kSuperManagementId));

        ActionResult<GetImplTaskTargetsResponse> result = await controller.GetImplTaskTargets(request);

        RequestValidationErrorResponse errors = (RequestValidationErrorResponse)((BadRequestObjectResult)result.Result!).Value!;
        Assert.That(errors.Errors.Select(error => error.Path), Is.EquivalentTo(kAllInvalidKeyPaths));
    }

    [TestCase(1, 0, true)]
    [TestCase(GetImplTaskTargetsOptions.kMaxLimit, 0, true)]
    [TestCase(null, null, true)]
    [TestCase(0, 0, false)]
    [TestCase(GetImplTaskTargetsOptions.kMaxLimit + 1, 0, false)]
    [TestCase(1, -1, false)]
    public void ValidationChecksTheLimitAndOffsetBounds(int? limit, int? offset, bool isValid)
    {
        GetImplTaskTargetsRequest request = new()
        {
            ManagementId = kSuperManagementId,
            Options = new GetImplTaskTargetsOptions { Limit = limit, Offset = offset }
        };

        Assert.That(GetImplTaskTargetsRequestValidator.Validate(request).Errors, isValid ? Is.Empty : Is.Not.Empty);
    }

    [Test]
    public void ValidationReportsPagingAndNameLengthErrorsTogether()
    {
        GetImplTaskTargetsRequest request = new()
        {
            ManagementId = kSuperManagementId,
            Options = new GetImplTaskTargetsOptions
            {
                Limit = 0,
                Offset = -1,
                Filter = new ImplTaskTargetFilter { Name = new string('a', ImplTaskTargetFilter.kMaxNameLength + 1) }
            }
        };
        GetImplTaskTargetsRequest longestValidName = new()
        {
            ManagementId = kSuperManagementId,
            Options = new GetImplTaskTargetsOptions { Filter = new ImplTaskTargetFilter { Name = new string('a', ImplTaskTargetFilter.kMaxNameLength) } }
        };

        Assert.Multiple(() =>
        {
            Assert.That(GetImplTaskTargetsRequestValidator.Validate(request).Errors.Select(error => error.Path), Is.EquivalentTo(kAllPagingAndLengthErrorPaths));
            Assert.That(GetImplTaskTargetsRequestValidator.Validate(longestValidName).Errors, Is.Empty);
        });
    }

    [Test]
    public void ValidationRejectsANonPositiveManagementIdAndAMissingBody()
    {
        RequestValidationErrorResponse zeroErrors = GetImplTaskTargetsRequestValidator.Validate(new GetImplTaskTargetsRequest { ManagementId = 0 });
        RequestValidationErrorResponse bodyErrors = GetImplTaskTargetsRequestValidator.Validate(null);

        Assert.Multiple(() =>
        {
            Assert.That(zeroErrors.Errors.Single().Message, Does.Contain("greater than 0"));
            Assert.That(bodyErrors.Errors.Single().Path, Is.Empty);
            Assert.That(bodyErrors.Errors.Single().Message, Does.Contain("requires a request body"));
        });
    }

    [Test]
    public void ClientDtosMatchTheEndpointContract()
    {
        string requestJson = JsonSerializer.Serialize(new ImplTaskTargetParameters
        {
            ManagementId = kSuperManagementId,
            IsServiceObject = true,
            Options = new ImplTaskTargetPaging { Offset = 1 }
        });
        string responseJson = JsonSerializer.Serialize(new GetImplTaskTargetsResponse
        {
            Targets = [new ImplTaskTargetResponse { Id = kFirstSubManagementId, Name = kFirstSubName }]
        });
        GetImplTaskTargetsRequest serverRequest = JsonSerializer.Deserialize<GetImplTaskTargetsRequest>(requestJson)!;
        ImplTaskTargetResult clientResult = JsonSerializer.Deserialize<ImplTaskTargetResult>(responseJson)!;

        Assert.Multiple(() =>
        {
            Assert.That(serverRequest.ManagementId, Is.EqualTo(kSuperManagementId));
            Assert.That(serverRequest.IsServiceObject, Is.True);
            Assert.That(serverRequest.AdditionalData, Is.Null);
            Assert.That(serverRequest.Options.Offset, Is.EqualTo(1));
            Assert.That(serverRequest.Options.Limit, Is.EqualTo(GetImplTaskTargetsOptions.kMaxLimit));
            Assert.That(GetImplTaskTargetsRequestValidator.Validate(serverRequest).Errors, Is.Empty);
            Assert.That(ImplTaskTargetPaging.kHasMoreHeader, Is.EqualTo(ListPaging.kHasMoreHeader));
            Assert.That(clientResult.Targets.Single().Id, Is.EqualTo(kFirstSubManagementId));
            Assert.That(clientResult.Targets.Single().Name, Is.EqualTo(kFirstSubName));
        });
    }

    private static InMemoryProvisioningApiConnection CreateApi()
    {
        DeviceType mdsType = new() { Id = 13, Name = "Check Point MDS", Version = "R8x" };
        DeviceType domainType = new() { Id = 9, Name = "Check Point", Version = "R8x" };
        InMemoryProvisioningApiConnection api = new();
        api.Managements.Add(new Management { Id = kSuperManagementId, Name = "mds", DeviceType = mdsType, IsSupermanager = true });
        api.Managements.Add(new Management { Id = kSecondSubManagementId, Name = kSecondSubName, DeviceType = domainType, SuperManagerId = kSuperManagementId });
        api.Managements.Add(new Management { Id = kFirstSubManagementId, Name = kFirstSubName, DeviceType = domainType, SuperManagerId = kSuperManagementId });
        api.AddNode(kGlobalNodeId, ProvisioningScopeType.Global, ProvisioningScopePath.GlobalObjectKey, displayName: "Global");
        api.SetValue(kGlobalNodeId, ProvisioningSettingKeys.AddressObjectCreation, ProvisioningObjectCreationMode.Submanager);
        return api;
    }

    private static WorkflowImplTaskTargetController CreateController(InMemoryProvisioningApiConnection api, ClaimsPrincipal user)
    {
        return new WorkflowImplTaskTargetController(new ImplTaskTargetService(api))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static ClaimsPrincipal Planner(int visibleManagementId)
    {
        return Principal(kPlannerRole, $"{{ {visibleManagementId} }}");
    }

    private static ClaimsPrincipal Principal(List<string> roles, string visibleManagements)
    {
        List<Claim> claims = [.. roles.Select(role => new Claim(ClaimTypes.Role, role))];
        claims.Add(new Claim(kVisibleManagementsClaim, visibleManagements));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, ClaimTypes.Role));
    }

    private static string HasMoreHeader(WorkflowImplTaskTargetController controller)
    {
        return controller.Response.Headers[ListPaging.kHasMoreHeader].ToString();
    }

    private static GetImplTaskTargetsResponse OkResponse(ActionResult<GetImplTaskTargetsResponse> result)
    {
        return (GetImplTaskTargetsResponse)((OkObjectResult)result.Result!).Value!;
    }
}
