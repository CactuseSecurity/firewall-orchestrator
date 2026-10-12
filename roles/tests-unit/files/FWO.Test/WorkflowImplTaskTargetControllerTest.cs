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

    private static readonly List<string> kControllerRoutes = ["api/workflow"];
    private static readonly List<int> kBothSubManagements = [kFirstSubManagementId, kSecondSubManagementId];
    private static readonly List<int> kSecondSubManagementOnly = [kSecondSubManagementId];
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
        WorkflowImplTaskTargetController controller = CreateController(CreateApi(), Planner(kSuperManagementId));

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
    public async Task GetImplTaskTargetsLetsTheAdminAskForAnyManagement()
    {
        WorkflowImplTaskTargetController controller = CreateController(CreateApi(), Principal(kAdminRole, ""));

        ActionResult<GetImplTaskTargetsResponse> result = await controller.GetImplTaskTargets(new GetImplTaskTargetsRequest { ManagementId = kSuperManagementId });

        Assert.That(OkResponse(result).Targets.Select(target => target.Id), Is.EqualTo(kBothSubManagements));
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
        WorkflowImplTaskTargetController controller = CreateController(CreateApi(), Planner(kSuperManagementId));

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
        string requestJson = JsonSerializer.Serialize(new ImplTaskTargetParameters { ManagementId = kSuperManagementId, IsServiceObject = true });
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

    private static GetImplTaskTargetsResponse OkResponse(ActionResult<GetImplTaskTargetsResponse> result)
    {
        return (GetImplTaskTargetsResponse)((OkObjectResult)result.Result!).Value!;
    }
}
