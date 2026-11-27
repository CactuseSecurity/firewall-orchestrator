using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using FWO.Services.DeviceSpecificSettings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the device specific settings REST endpoints: authorization and rate limiting, the request contracts with
/// defaults and aggregated validation, the import errors below <c>package</c>, dry runs, snapshot paging and conflicts.
/// </summary>
[TestFixture]
internal class DeviceSpecificSettingsControllerTest
{
    private const string kManagementName = "mgmt-a";
    private const string kSnapshotName = "baseline";

    private static readonly List<string> kActions =
    [
        nameof(DeviceSpecificSettingsController.Export),
        nameof(DeviceSpecificSettingsController.Import),
        nameof(DeviceSpecificSettingsController.ListSnapshots),
        nameof(DeviceSpecificSettingsController.GetSnapshot),
        nameof(DeviceSpecificSettingsController.AddSnapshot)
    ];

    private static readonly List<string> kPostActions =
    [
        nameof(DeviceSpecificSettingsController.Import),
        nameof(DeviceSpecificSettingsController.ListSnapshots),
        nameof(DeviceSpecificSettingsController.AddSnapshot)
    ];

    [Test]
    public void EveryEndpointIsAdminOnlyAndUsesTheExpensivePolicy()
    {
        Assert.Multiple(() =>
        {
            Assert.That(typeof(DeviceSpecificSettingsController).GetCustomAttribute<RouteAttribute>()?.Template, Is.EqualTo("api/[controller]"));
            foreach (string action in kActions)
            {
                MethodInfo method = typeof(DeviceSpecificSettingsController).GetMethod(action)!;
                Assert.That(method.GetCustomAttribute<AuthorizeAttribute>()?.Roles, Is.EqualTo(Roles.Admin), action);
                Assert.That(method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName, Is.EqualTo(ApiRateLimiting.kExpensivePolicy), action);
            }
            foreach (string action in kPostActions)
            {
                MethodInfo method = typeof(DeviceSpecificSettingsController).GetMethod(action)!;
                Assert.That(method.GetCustomAttribute<RequestSizeLimitAttribute>(), Is.Not.Null, action);
            }
        });
    }

    [Test]
    public void ImportRequestDefaultsToMergeWithoutDryRun()
    {
        ImportDeviceSpecificSettingsRequest omitted = JsonSerializer.Deserialize<ImportDeviceSpecificSettingsRequest>("{\"package\":{}}")!;
        ImportDeviceSpecificSettingsRequest explicitNull = JsonSerializer.Deserialize<ImportDeviceSpecificSettingsRequest>("{\"package\":{},\"options\":null}")!;

        Assert.Multiple(() =>
        {
            Assert.That(omitted.Options.Mode, Is.EqualTo(DeviceSpecificSettingsValidationSchema.kModeMerge));
            Assert.That(omitted.Options.DryRun, Is.False);
            Assert.That(explicitNull.Options, Is.Not.Null);
            Assert.That(DeviceSpecificSettingsRequestValidator.ParseMode(null), Is.EqualTo(DeviceSpecificSettingsImportMode.Merge));
            Assert.That(DeviceSpecificSettingsRequestValidator.ParseMode("replace"), Is.EqualTo(DeviceSpecificSettingsImportMode.Replace));
        });
    }

    [Test]
    public async Task ImportReportsAllRequestErrorsTogether()
    {
        ImportDeviceSpecificSettingsRequest request = JsonSerializer.Deserialize<ImportDeviceSpecificSettingsRequest>(
            "{\"unknownRoot\":1,\"options\":{\"mode\":\"overwrite\",\"unknownOption\":true}}")!;

        ActionResult<ImportDeviceSpecificSettingsResponse> result = await CreateController(CreateApi()).Import(request);

        Assert.That(ErrorPaths(result.Result), Is.EquivalentTo(["package", "options.mode", "unknownRoot", "options.unknownOption"]));
    }

    [Test]
    public async Task ImportReportsPackageErrorsBelowThePackageKeyAndWritesNothing()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        ImportDeviceSpecificSettingsRequest request = new()
        {
            Package = Package("{\"level\":\"management\",\"management_name\":\"missing\",\"values\":{}}")
        };

        ActionResult<ImportDeviceSpecificSettingsResponse> result = await CreateController(api).Import(request);

        Assert.Multiple(() =>
        {
            Assert.That(ErrorPaths(result.Result), Is.EqualTo(new List<string> { "package.nodes[0]" }));
            Assert.That(api.BulkPatchCallCount + api.UpsertCallCount, Is.Zero);
        });
    }

    [Test]
    public async Task ImportAppliesThePackageUnlessItIsADryRun()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        string node = $"{{\"level\":\"management\",\"management_name\":\"{kManagementName}\",\"values\":{{\"importerHostName\":\"importer-2\"}}}}";

        ImportDeviceSpecificSettingsResponse dryRun = Ok(await CreateController(api).Import(new ImportDeviceSpecificSettingsRequest
        {
            Package = Package(node),
            Options = new ImportDeviceSpecificSettingsOptions { DryRun = true }
        }));
        int nodesAfterDryRun = api.Nodes.Count;
        ImportDeviceSpecificSettingsResponse applied = Ok(await CreateController(api).Import(new ImportDeviceSpecificSettingsRequest { Package = Package(node) }));

        Assert.Multiple(() =>
        {
            Assert.That(dryRun.Applied, Is.False);
            Assert.That(dryRun.Nodes.Single().Changes.Single().Kind, Is.EqualTo(DeviceSpecificSettingChangeKind.Added));
            Assert.That(nodesAfterDryRun, Is.EqualTo(1));
            Assert.That(applied.Applied, Is.True);
            Assert.That(applied.Mode, Is.EqualTo(DeviceSpecificSettingsImportMode.Merge));
            Assert.That(api.Nodes.Single(item => item.NodeType == "management").Values.Single().ConfigKey, Is.EqualTo("importerHostName"));
        });
    }

    [Test]
    public async Task ImportReportsNumbersOutOfRangeAsPackageErrorsAndWritesNothing()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        ImportDeviceSpecificSettingsRequest request = new()
        {
            Package = Package("{\"level\":\"global\",\"values\":{\"requestMaxAttempts\":99999999999999999999,\"requestResponseTimeout\":1e400}}")
        };

        ActionResult result = (await CreateController(api).Import(request)).Result!;

        Assert.Multiple(() =>
        {
            Assert.That(ErrorPaths(result), Is.EquivalentTo(
                ["package.nodes[0].values.requestMaxAttempts", "package.nodes[0].values.requestResponseTimeout"]));
            Assert.That(api.BulkPatchCallCount, Is.Zero);
            Assert.That(api.ReadValue(1, DeviceSpecificSettingKeys.RequestMaxAttempts)?.Value<int>(), Is.EqualTo(4));
        });
    }

    [Test]
    public async Task SettingsLargerThanAnExportFileAnswerConflictAndStoreNoSnapshot()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        api.SetValue(1, DeviceSpecificSettingKeys.ScUrl, new string('u', (int)DeviceSpecificSettingsPackage.kMaxPackageBytes));
        DeviceSpecificSettingsController controller = CreateController(api);

        ActionResult export = (await controller.Export()).Result!;
        ActionResult snapshot = (await controller.AddSnapshot(new AddDeviceSpecificSettingsSnapshotRequest { Name = kSnapshotName })).Result!;

        Assert.Multiple(() =>
        {
            Assert.That(export, Is.InstanceOf<ConflictObjectResult>());
            Assert.That(((ConflictObjectResult)export).Value, Does.Contain("bytes"));
            Assert.That(snapshot, Is.InstanceOf<ConflictObjectResult>());
            Assert.That(api.Snapshots, Is.Empty);
        });
    }

    [Test]
    public async Task ExportReturnsThePackage()
    {
        DeviceSpecificSettingsPackage package = Ok(await CreateController(CreateApi()).Export());

        Assert.That(package.Nodes.Single().Values.Keys, Is.EquivalentTo(["requestMaxAttempts"]));
    }

    [Test]
    public async Task SnapshotsAreStoredPagedAndRead()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
        DeviceSpecificSettingsController controller = CreateController(api);

        DeviceSpecificSettingsSnapshotSummary stored = Ok(await controller.AddSnapshot(new AddDeviceSpecificSettingsSnapshotRequest { Name = kSnapshotName }));
        Ok(await controller.AddSnapshot(new AddDeviceSpecificSettingsSnapshotRequest { Name = "second", Package = new DeviceSpecificSettingsPackage() }));
        ActionResult duplicate = (await controller.AddSnapshot(new AddDeviceSpecificSettingsSnapshotRequest { Name = kSnapshotName })).Result!;
        List<DeviceSpecificSettingsSnapshotSummary> firstPage = Ok(await controller.ListSnapshots(ListRequest(limit: 1, offset: 0)));
        string? firstHasMore = controller.HttpContext.Response.Headers[ListPaging.kHasMoreHeader];
        List<DeviceSpecificSettingsSnapshotSummary> secondPage = Ok(await controller.ListSnapshots(ListRequest(limit: 1, offset: 1)));
        string? secondHasMore = controller.HttpContext.Response.Headers[ListPaging.kHasMoreHeader];
        DeviceSpecificSettingsSnapshotResponse read = Ok(await controller.GetSnapshot(stored.Id));
        ActionResult missing = (await controller.GetSnapshot(99)).Result!;

        Assert.Multiple(() =>
        {
            Assert.That(duplicate, Is.InstanceOf<ConflictObjectResult>());
            Assert.That(firstPage.Single().Name, Is.EqualTo("second"));
            Assert.That(firstHasMore, Is.EqualTo("true"));
            Assert.That(secondPage.Single().Name, Is.EqualTo(kSnapshotName));
            Assert.That(secondHasMore, Is.EqualTo("false"));
            Assert.That(read.Package.Nodes.Single().Values.Keys, Is.EquivalentTo(["requestMaxAttempts"]),
                "without package the current settings are stored");
            Assert.That(missing, Is.InstanceOf<NotFoundResult>());
        });
    }

    [Test]
    public async Task SnapshotRequestsReportAllErrorsTogether()
    {
        DeviceSpecificSettingsController controller = CreateController(CreateApi());
        AddDeviceSpecificSettingsSnapshotRequest add = JsonSerializer.Deserialize<AddDeviceSpecificSettingsSnapshotRequest>(
            "{\"name\":\" \",\"package\":{\"version\":2},\"options\":{\"x\":1}}")!;
        ListDeviceSpecificSettingsSnapshotsRequest list = JsonSerializer.Deserialize<ListDeviceSpecificSettingsSnapshotsRequest>(
            $"{{\"options\":{{\"limit\":0,\"offset\":-1,\"filter\":{{\"name\":\"{new string('n', DeviceSpecificSettingsValidationSchema.kMaxFilterTextLength + 1)}\",\"y\":1}}}}}}")!;

        ActionResult addResult = (await controller.AddSnapshot(add)).Result!;
        ActionResult listResult = (await controller.ListSnapshots(list)).Result!;

        Assert.Multiple(() =>
        {
            Assert.That(ErrorPaths(addResult), Is.EquivalentTo(["name", "package.version", "options.x"]));
            Assert.That(ErrorPaths(listResult), Is.EquivalentTo(["options.limit", "options.offset", "options.filter.name", "options.filter.y"]));
            Assert.That(ErrorPaths(DeviceSpecificSettingsRequestValidator.ValidateImport(null)), Is.EqualTo(new List<string> { "" }));
        });
    }

    private static ListDeviceSpecificSettingsSnapshotsRequest ListRequest(int limit, int offset) =>
        new() { Options = new ListDeviceSpecificSettingsSnapshotsOptions { Limit = limit, Offset = offset } };

    private static DeviceSpecificSettingsPackage Package(string node) =>
        DeviceSpecificSettingsPackageJson.Deserialize($"{{\"format\":\"{DeviceSpecificSettingsPackage.kFormat}\",\"version\":1,\"nodes\":[{node}]}}");

    private static InMemoryDeviceSpecificSettingsApiConnection CreateApi()
    {
        InMemoryDeviceSpecificSettingsApiConnection api = new();
        DeviceType checkPoint = new() { Id = 9, Name = "Check Point", Version = "R8x" };
        api.DeviceTypes.Add(checkPoint);
        api.Managements.Add(new Management { Id = 1, Name = kManagementName, DeviceType = checkPoint });
        api.AddNode(1, DeviceSpecificScopeType.Global, "global", displayName: "Global");
        api.SetValue(1, DeviceSpecificSettingKeys.RequestMaxAttempts, 4);
        return api;
    }

    private static DeviceSpecificSettingsController CreateController(InMemoryDeviceSpecificSettingsApiConnection api)
    {
        List<Claim> claims = [new Claim(ClaimTypes.Role, Roles.Admin), new Claim("unique_name", "admin")];
        return new DeviceSpecificSettingsController(api, new DeviceSpecificSettingsManager(api), new SimulatedGlobalConfig())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
    }

    private static T Ok<T>(ActionResult<T> result)
    {
        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        return (T)((OkObjectResult)result.Result!).Value!;
    }

    private static List<string> ErrorPaths(IActionResult? result)
    {
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        return ErrorPaths((RequestValidationErrorResponse)((BadRequestObjectResult)result!).Value!);
    }

    private static List<string> ErrorPaths(RequestValidationErrorResponse response) => [.. response.Errors.Select(error => error.Path)];
}
