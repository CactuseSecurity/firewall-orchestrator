using System.Reflection;
using System.Text.Json;
using FWO.Basics;
using FWO.Data;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using FWO.Services.PathAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the resolvePathDevices endpoint end to end below HTTP: route and authorization, the status codes,
/// and the mapping of the analysis result including filter, algorithm name and zone matrix. The analysis runs
/// against the shared test matrix of <see cref="PathAnalysisTestData"/>.
/// </summary>
[TestFixture]
[Parallelizable]
internal class PathAnalysisControllerTest
{
    private const int kRequestedMatrixId = 3;
    private const int kDesignatedMatrixId = 7;

    private const string kZoneAToZoneB =
        "\"sources\":[{\"ipStart\":\"10.1.0.1\",\"ipEnd\":\"10.1.0.1\"}],\"destinations\":[{\"ipStart\":\"10.2.0.1\",\"ipEnd\":\"10.2.0.1\"}]";

    private static readonly List<string> kAllPathDeviceNames =
    [
        PathAnalysisTestData.kAccessAName,
        PathAnalysisTestData.kCoreName,
        PathAnalysisTestData.kAccessBName
    ];

    /// <summary>Verifies the route, the verb and the roles that may run a path analysis.</summary>
    [Test]
    public void ResolvePathDevices_UsesPathAnalysisRouteAndRoles()
    {
        List<string> routes = [.. typeof(PathAnalysisController).GetCustomAttributes<RouteAttribute>().Select(route => route.Template)];
        MethodInfo endpoint = typeof(PathAnalysisController).GetMethod(nameof(PathAnalysisController.ResolvePathDevices))!;

        Assert.Multiple(() =>
        {
            Assert.That(routes, Is.EqualTo(new List<string> { "api/pathAnalysis" }));
            Assert.That(endpoint.GetCustomAttribute<HttpPostAttribute>()?.Template, Is.EqualTo("resolvePathDevices"));
            Assert.That(endpoint.GetCustomAttribute<AuthorizeAttribute>()?.Roles,
                Is.EqualTo($"{Roles.Admin}, {Roles.Auditor}"));
        });
    }

    /// <summary>Verifies every status code the endpoint can answer is documented.</summary>
    [Test]
    public void ResolvePathDevices_DocumentsEveryStatusCode()
    {
        MethodInfo endpoint = typeof(PathAnalysisController).GetMethod(nameof(PathAnalysisController.ResolvePathDevices))!;
        List<int> documented = [.. endpoint.GetCustomAttributes<ProducesResponseTypeAttribute>().Select(attribute => attribute.StatusCode)];

        Assert.That(documented, Is.EquivalentTo(new List<int>
        {
            StatusCodes.Status200OK,
            StatusCodes.Status400BadRequest,
            StatusCodes.Status401Unauthorized,
            StatusCodes.Status403Forbidden,
            StatusCodes.Status404NotFound,
            StatusCodes.Status500InternalServerError
        }));
    }

    /// <summary>Verifies an invalid request is answered with every validation error and never analysed.</summary>
    [Test]
    public async Task ResolvePathDevices_WithInvalidRequest_ReturnsBadRequestWithoutAnalysis()
    {
        PathAnalysisMatrixApiConnection apiConnection = PathAnalysisTestData.CreateApiConnection();

        ActionResult<ResolvePathDevicesResponse> result = await CreateController(apiConnection)
            .ResolvePathDevices(Deserialize("""{"sources":[],"options":{"algorithm":"routing"}}"""));

        BadRequestObjectResult badRequest = (BadRequestObjectResult)result.Result!;
        RequestValidationErrorResponse errors = (RequestValidationErrorResponse)badRequest.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(errors.Errors.Select(error => error.Path),
                Is.EquivalentTo(new List<string> { "sources", "destinations", "options.algorithm" }));
            Assert.That(apiConnection.QueriedVariables, Is.Empty);
        });
    }

    /// <summary>Verifies a configured default of none stops the request with a validation error.</summary>
    [Test]
    public async Task ResolvePathDevices_WithDefaultNone_ReturnsBadRequest()
    {
        ActionResult<ResolvePathDevicesResponse> result = await CreateController(
                PathAnalysisTestData.CreateApiConnection(), GlobalConst.kPathAnalysisAlgorithmNone)
            .ResolvePathDevices(Deserialize(Request()));

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    /// <summary>
    /// Verifies a valid request returns the devices on the path together with the algorithm and the matrix the
    /// defaults resolved to.
    /// </summary>
    [Test]
    public async Task ResolvePathDevices_WithDefaults_ReturnsDevicesAlgorithmAndDesignatedMatrix()
    {
        ResolvePathDevicesResponse response = await RunAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(response.Devices.Select(device => device.Name), Is.EquivalentTo(kAllPathDeviceNames));
            Assert.That(response.Algorithm, Is.EqualTo(PathAnalysisAlgorithmNames.kNetworkZoneTree));
            Assert.That(response.MatrixId, Is.EqualTo(kDesignatedMatrixId));
        });
    }

    /// <summary>Verifies a requested matrix is the one reported back.</summary>
    [Test]
    public async Task ResolvePathDevices_WithRequestedMatrix_ReportsRequestedMatrix()
    {
        ResolvePathDevicesResponse response = await RunAsync(
            Request(",\"options\":{\"algorithm\":\"networkZoneTree\",\"matrixId\":3}"));

        Assert.That(response.MatrixId, Is.EqualTo(kRequestedMatrixId));
    }

    /// <summary>Verifies a device keeps both its id and its name in the response.</summary>
    [Test]
    public async Task ResolvePathDevices_MapsDeviceIdAndName()
    {
        ResolvePathDevicesResponse response = await RunAsync(Request());

        PathDeviceResponse core = response.Devices.Single(device => device.Id == PathAnalysisTestData.kCoreDeviceId);
        Assert.That(core.Name, Is.EqualTo(PathAnalysisTestData.kCoreName));
    }

    /// <summary>Verifies a source given as a network is analysed like one given as a range.</summary>
    [Test]
    public async Task ResolvePathDevices_WithNetworkSource_ReturnsSameDevicesAsRange()
    {
        ResolvePathDevicesResponse response = await RunAsync(
            """{"sources":[{"ipNetwork":"10.1.0.0/24"}],"destinations":[{"ipNetwork":"10.2.0.0/24"}]}""");

        Assert.That(response.Devices.Select(device => device.Name), Is.EquivalentTo(kAllPathDeviceNames));
    }

    /// <summary>Verifies the id filter keeps only the matching device.</summary>
    [Test]
    public async Task ResolvePathDevices_WithIdFilter_ReturnsOnlyMatchingDevice()
    {
        ResolvePathDevicesResponse response = await RunAsync(
            Request($",\"options\":{{\"filter\":{{\"id\":{PathAnalysisTestData.kCoreDeviceId}}}}}"));

        Assert.That(response.Devices.Select(device => device.Id), Is.EqualTo(new List<int> { PathAnalysisTestData.kCoreDeviceId }));
    }

    /// <summary>Verifies the name filter keeps only the matching device, compared case-insensitively.</summary>
    [Test]
    public async Task ResolvePathDevices_WithNameFilter_ReturnsOnlyMatchingDeviceIgnoringCase()
    {
        ResolvePathDevicesResponse response = await RunAsync(Request(",\"options\":{\"filter\":{\"name\":\"FW-CORE\"}}"));

        Assert.That(response.Devices.Select(device => device.Name), Is.EqualTo(new List<string> { PathAnalysisTestData.kCoreName }));
    }

    /// <summary>Verifies id and name filters must both match.</summary>
    [Test]
    public async Task ResolvePathDevices_WithContradictingFilters_ReturnsNoDevice()
    {
        ResolvePathDevicesResponse response = await RunAsync(
            Request($",\"options\":{{\"filter\":{{\"id\":{PathAnalysisTestData.kCoreDeviceId},\"name\":\"fw-access-a\"}}}}"));

        Assert.That(response.Devices, Is.Empty);
    }

    /// <summary>Verifies a requested matrix that cannot be used is reported as not found under the matrix option.</summary>
    [Test]
    public async Task ResolvePathDevices_WithUnusableRequestedMatrix_ReturnsNotFound()
    {
        ActionResult<ResolvePathDevicesResponse> result = await CreateController(PathAnalysisTestData.CreateEmptyApiConnection())
            .ResolvePathDevices(Deserialize(Request(",\"options\":{\"algorithm\":\"networkZoneTree\",\"matrixId\":3}")));

        NotFoundObjectResult notFound = (NotFoundObjectResult)result.Result!;
        RequestValidationErrorResponse errors = (RequestValidationErrorResponse)notFound.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(errors.Errors.Single().Path, Is.EqualTo("options.matrixId"));
            Assert.That(errors.Errors.Single().Message, Does.Contain(kRequestedMatrixId.ToString()));
        });
    }

    /// <summary>
    /// Verifies an unusable designated matrix is a server failure, not the caller's fault, since the caller
    /// named no matrix.
    /// </summary>
    [Test]
    public async Task ResolvePathDevices_WithUnusableDesignatedMatrix_ReturnsServerError()
    {
        ActionResult<ResolvePathDevicesResponse> result = await CreateController(PathAnalysisTestData.CreateAutoCalculatedOnlyApiConnection())
            .ResolvePathDevices(Deserialize(Request()));

        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    /// <summary>Verifies a failing api is answered with a server error instead of an unhandled exception.</summary>
    [Test]
    public async Task ResolvePathDevices_WithFailingApi_ReturnsServerError()
    {
        PathAnalysisMatrixApiConnection apiConnection = PathAnalysisTestData.CreateApiConnection();
        apiConnection.FailEveryQuery = true;

        ActionResult<ResolvePathDevicesResponse> result = await CreateController(apiConnection).ResolvePathDevices(Deserialize(Request()));

        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    /// <summary>
    /// Verifies the service refuses a request that bypassed validation instead of analysing a partial list.
    /// Reaching this is a programming error, so it surfaces as an exception rather than a client error.
    /// </summary>
    [Test]
    public void Service_WithUnvalidatedNullEntry_Throws()
    {
        ResolvePathDevicesRequest request = Deserialize("""{"sources":[null],"destinations":[{"ipNetwork":"10.2.0.0/24"}]}""");

        Assert.That(async () => await CreateService(PathAnalysisTestData.CreateApiConnection()).ResolvePathDevicesAsync(request),
            Throws.InstanceOf<InvalidOperationException>());
    }

    /// <summary>Verifies the service refuses an unvalidated block that cannot be parsed.</summary>
    [Test]
    public void Service_WithUnvalidatedInvalidEntry_Throws()
    {
        ResolvePathDevicesRequest request = Deserialize("""{"sources":[{"ipStart":"10.1.0.1"}],"destinations":[{"ipNetwork":"10.2.0.0/24"}]}""");

        Assert.That(async () => await CreateService(PathAnalysisTestData.CreateApiConnection()).ResolvePathDevicesAsync(request),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("requires both"));
    }

    private static async Task<ResolvePathDevicesResponse> RunAsync(string json)
    {
        ActionResult<ResolvePathDevicesResponse> result = await CreateController(PathAnalysisTestData.CreateApiConnection())
            .ResolvePathDevices(Deserialize(json));
        return (ResolvePathDevicesResponse)((OkObjectResult)result.Result!).Value!;
    }

    private static PathAnalysisController CreateController(PathAnalysisMatrixApiConnection apiConnection,
        long configuredAlgorithmId = GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree)
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig(configuredAlgorithmId);
        return new PathAnalysisController(new PathAnalysisService(new PathAnalyzerFactory(apiConnection, globalConfig)), globalConfig);
    }

    private static PathAnalysisService CreateService(PathAnalysisMatrixApiConnection apiConnection)
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig(GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree);
        return new PathAnalysisService(new PathAnalyzerFactory(apiConnection, globalConfig));
    }

    private static SimulatedGlobalConfig CreateGlobalConfig(long configuredAlgorithmId)
    {
        return new SimulatedGlobalConfig
        {
            PathAnalysisAlgorithm = configuredAlgorithmId,
            DesignatedZoneMatrixId = kDesignatedMatrixId
        };
    }

    private static string Request(string additionalRootJson = "")
    {
        return "{" + kZoneAToZoneB + additionalRootJson + "}";
    }

    private static ResolvePathDevicesRequest Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ResolvePathDevicesRequest>(json)!;
    }
}
