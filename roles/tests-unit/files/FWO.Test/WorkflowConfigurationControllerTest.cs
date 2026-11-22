using System.Reflection;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using Schema = FWO.Middleware.Server.Requests.WorkflowConfigurationValidationSchema;

namespace FWO.Test;

/// <summary>
/// Covers the workflow configuration REST endpoints: authorization and rate limiting, the request contracts with defaults
/// and aggregated validation, paging of the list, the export with phase selection, and the import with the caller's
/// connection, conflicts and the error answers of the caller lookup.
/// </summary>
[TestFixture]
internal class WorkflowConfigurationControllerTest
{
    private const int kConfigurationId = 5;
    private const string kConfigurationName = "Shared";
    private static readonly List<Ldap> kNoLdaps = [];

    [Test]
    public void EveryEndpointIsRateLimitedAndOnlyImportNeedsAdmin()
    {
        Assert.Multiple(() =>
        {
            Assert.That(typeof(WorkflowConfigurationController).GetCustomAttribute<RouteAttribute>()?.Template, Is.EqualTo("api/[controller]"));
            foreach (string action in new List<string> { nameof(WorkflowConfigurationController.List), nameof(WorkflowConfigurationController.Export), nameof(WorkflowConfigurationController.Import) })
            {
                MethodInfo method = typeof(WorkflowConfigurationController).GetMethod(action)!;
                Assert.That(method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName, Is.EqualTo(ApiRateLimiting.kExpensivePolicy), action);
                Assert.That(method.GetCustomAttribute<RequestSizeLimitAttribute>(), Is.Not.Null, action);
            }
            Assert.That(RolesOf(nameof(WorkflowConfigurationController.List)), Is.EqualTo($"{Roles.Admin}, {Roles.Auditor}"));
            Assert.That(RolesOf(nameof(WorkflowConfigurationController.Export)), Is.EqualTo($"{Roles.Admin}, {Roles.Auditor}"));
            Assert.That(RolesOf(nameof(WorkflowConfigurationController.Import)), Is.EqualTo(Roles.Admin));
        });
    }

    [Test]
    public void OmittedAndNullOptionsTakeTheirDefaults()
    {
        ExportWorkflowConfigurationRequest export = JsonSerializer.Deserialize<ExportWorkflowConfigurationRequest>("{\"configurationId\":5}")!;
        ExportWorkflowConfigurationRequest exportNull = JsonSerializer.Deserialize<ExportWorkflowConfigurationRequest>("{\"configurationId\":5,\"options\":null}")!;
        ImportWorkflowConfigurationRequest import = JsonSerializer.Deserialize<ImportWorkflowConfigurationRequest>("{\"package\":null,\"options\":null}")!;
        ListWorkflowConfigurationsRequest list = JsonSerializer.Deserialize<ListWorkflowConfigurationsRequest>("{}")!;

        Assert.Multiple(() =>
        {
            Assert.That(export.Options.IncludeVisibilityGroups, Is.False);
            Assert.That(export.Options.Phases, Is.Null);
            Assert.That(exportNull.Options, Is.Not.Null);
            Assert.That(import.Options.Name, Is.Null);
            Assert.That(import.Options.TargetConfigurationId, Is.Null);
            Assert.That(list.Options.Filter, Is.Null);
            Assert.That(list.Options.Limit, Is.Null);
            Assert.That(list.Options.Offset, Is.Null);
        });
    }

    [Test]
    public void AMissingBodyIsReportedWithTheValidRootKeys()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WorkflowConfigurationRequestValidator.ValidateList(null, 10).Errors.Single().Message, Does.Contain("'options'"));
            Assert.That(WorkflowConfigurationRequestValidator.ValidateExport(null).Errors.Single().Message, Does.Contain("'configurationId'"));
            Assert.That(WorkflowConfigurationRequestValidator.ValidateImport(null).Errors.Single().Message, Does.Contain("'package'"));
        });
    }

    [Test]
    public void AnInvalidListRequestReportsEveryError()
    {
        ListWorkflowConfigurationsRequest request = JsonSerializer.Deserialize<ListWorkflowConfigurationsRequest>(
            $"{{\"options\":{{\"limit\":11,\"offset\":-1,\"filter\":{{\"name\":\"{new string('n', Schema.kMaxTextLength + 1)}\",\"description\":\"{new string('d', Schema.kMaxTextLength + 1)}\",\"phase\":1}},\"sort\":1}},\"extra\":1}}")!;

        List<string> paths = ErrorPaths(WorkflowConfigurationRequestValidator.ValidateList(request, 10));

        Assert.That(paths, Is.EquivalentTo(
        [
            Schema.kLimitPath, Schema.kOffsetPath, Schema.kFilterNamePath, Schema.kFilterDescriptionPath, "extra", "options.sort", "options.filter.phase"
        ]));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(10)]
    public void TheLimitBoundsAreInclusive(int limit)
    {
        ListWorkflowConfigurationsRequest request = new() { Options = new() { Limit = limit } };

        List<string> paths = ErrorPaths(WorkflowConfigurationRequestValidator.ValidateList(request, 10));

        Assert.That(paths, limit == 0 ? Is.EqualTo(new List<string> { Schema.kLimitPath }) : Is.Empty);
    }

    [Test]
    public void AnInvalidExportRequestReportsEveryError()
    {
        ExportWorkflowConfigurationRequest request = JsonSerializer.Deserialize<ExportWorkflowConfigurationRequest>(
            "{\"configurationId\":0,\"options\":{\"includeVisibilityGroups\":null,\"phases\":[null,{\"task_type\":\" \",\"phase\":\"approval\",\"x\":1}],\"y\":2},\"z\":3}")!;

        List<string> paths = ErrorPaths(WorkflowConfigurationRequestValidator.ValidateExport(request));

        Assert.That(paths, Is.EquivalentTo(
        [
            Schema.kConfigurationIdPath, "options.phases[0]", "options.phases[1].task_type", "options.phases[1].x", "options.y", "z"
        ]));
    }

    [TestCase(0)]
    [TestCase(Schema.kMaxPhaseSelections + 1)]
    public void APhaseSelectionMustNotBeEmptyOrTooLong(int count)
    {
        ExportWorkflowConfigurationRequest request = new()
        {
            ConfigurationId = kConfigurationId,
            Options = new() { Phases = [.. Enumerable.Range(0, count).Select(_ => new WorkflowPhaseSelectionRequest { TaskType = "access", Phase = "approval" })] }
        };

        Assert.That(ErrorPaths(WorkflowConfigurationRequestValidator.ValidateExport(request)), Is.EqualTo(new List<string> { Schema.kPhasesPath }));
    }

    [Test]
    public void AnInvalidImportRequestReportsEveryError()
    {
        WorkflowConfigurationTransferPackage package = Package();
        package.Version = 99;
        ImportWorkflowConfigurationRequest request = new()
        {
            Package = package,
            Options = new() { Name = "new", TargetConfigurationId = 0 }
        };

        List<string> paths = ErrorPaths(WorkflowConfigurationRequestValidator.ValidateImport(request));

        Assert.That(paths, Is.EquivalentTo([Schema.kPackagePath, Schema.kTargetConfigurationIdPath, Schema.kNamePath]));
    }

    [TestCase(null, " ", Schema.kPackageConfigurationNamePath)]
    [TestCase(null, null, Schema.kPackageConfigurationNamePath)]
    [TestCase(" ", kConfigurationName, Schema.kNamePath)]
    [TestCase("x", null, null)]
    public void TheNewNameMustNotBeEmptyOrTooLong(string? name, string? packageName, string? errorPath)
    {
        WorkflowConfigurationTransferPackage package = Package();
        package.Configuration.Name = packageName!;
        ImportWorkflowConfigurationRequest request = new() { Package = package, Options = new() { Name = name } };

        List<string> paths = ErrorPaths(WorkflowConfigurationRequestValidator.ValidateImport(request));

        Assert.That(paths, errorPath is null ? Is.Empty : Is.EqualTo(new List<string> { errorPath }));
    }

    [Test]
    public void ATooLongNewNameIsRejected()
    {
        ImportWorkflowConfigurationRequest request = new() { Package = Package(), Options = new() { Name = new string('n', Schema.kMaxNameLength + 1) } };

        Assert.That(ErrorPaths(WorkflowConfigurationRequestValidator.ValidateImport(request)), Is.EqualTo(new List<string> { Schema.kNamePath }));
    }

    [Test]
    public async Task TheListIsPagedWithLookaheadAndSignalsFurtherPages()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurationPage, new List<WorkflowConfigurationSummary>
        {
            new() { Id = 1, Name = "a" }, new() { Id = 2, Name = "b" }, new() { Id = 3, Name = "c" }
        });
        WorkflowConfigurationController controller = CreateController(api);

        ActionResult<List<WorkflowConfigurationSummary>> result = await controller.List(new ListWorkflowConfigurationsRequest
        {
            Options = new() { Limit = 2, Offset = 4, Filter = new() { Name = "x", IsActive = true } }
        });

        Newtonsoft.Json.Linq.JObject variables = Newtonsoft.Json.Linq.JObject.FromObject(api.Calls.Single().Variables!);
        Assert.Multiple(() =>
        {
            Assert.That(Ok(result).Select(configuration => configuration.Id), Is.EqualTo(new List<int> { 1, 2 }));
            Assert.That(controller.HttpContext.Response.Headers[ListPaging.kHasMoreHeader].ToString(), Is.EqualTo("true"));
            Assert.That((int?)variables["limit"], Is.EqualTo(3));
            Assert.That((int?)variables["offset"], Is.EqualTo(4));
            Assert.That((bool?)variables["where"]?["is_active"]?["_eq"], Is.True);
        });
    }

    [Test]
    public async Task AnInvalidListRequestAnswersBadRequestWithoutQuerying()
    {
        RecordingWorkflowApiConnection api = new();
        WorkflowConfigurationController controller = CreateController(api);

        ActionResult<List<WorkflowConfigurationSummary>> result = await controller.List(new ListWorkflowConfigurationsRequest { Options = new() { Offset = -1 } });

        Assert.Multiple(() =>
        {
            Assert.That(ErrorPaths(result.Result), Is.EqualTo(new List<string> { Schema.kOffsetPath }));
            Assert.That(api.Calls, Is.Empty);
        });
    }

    [Test]
    public async Task AFailingListAnswersServerError()
    {
        WorkflowConfigurationController controller = CreateController(new RecordingWorkflowApiConnection());

        ActionResult<List<WorkflowConfigurationSummary>> result = await controller.List(new ListWorkflowConfigurationsRequest());

        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    [Test]
    public async Task TheExportReturnsThePackageOfTheSelectedPhases()
    {
        RecordingWorkflowApiConnection api = ExportApi();
        WorkflowConfigurationController controller = CreateController(api);

        ActionResult<WorkflowConfigurationTransferPackage> result = await controller.Export(new ExportWorkflowConfigurationRequest
        {
            ConfigurationId = kConfigurationId,
            Options = new() { IncludeVisibilityGroups = true, Phases = [new() { TaskType = " MASTER ", Phase = "Request" }] }
        });

        WorkflowConfigurationTransferPackage package = Ok(result);
        Assert.Multiple(() =>
        {
            Assert.That(package.Configuration.Name, Is.EqualTo(kConfigurationName));
            Assert.That(package.Configuration.Phases.Single().Phase, Is.EqualTo("request"));
            Assert.That(package.VisibilityGroups?.Single().Members, Is.EqualTo(new List<string> { "cn=operators" }));
        });
    }

    [Test]
    public async Task AnUnknownConfigurationAnswersNotFound()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurationPage, new List<WorkflowConfigurationSummary>());

        ActionResult<WorkflowConfigurationTransferPackage> result = await CreateController(api).Export(new ExportWorkflowConfigurationRequest { ConfigurationId = 9 });

        Assert.That(result.Result, Is.InstanceOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task AnUnknownPhaseIsReportedAtItsPath()
    {
        ActionResult<WorkflowConfigurationTransferPackage> result = await CreateController(ExportApi()).Export(new ExportWorkflowConfigurationRequest
        {
            ConfigurationId = kConfigurationId,
            Options = new() { Phases = [new() { TaskType = "master", Phase = "request" }, new() { TaskType = "access", Phase = "approval" }] }
        });

        Assert.Multiple(() =>
        {
            Assert.That(ErrorPaths(result.Result), Is.EqualTo(new List<string> { "options.phases[1]" }));
            Assert.That(((RequestValidationErrorResponse)((BadRequestObjectResult)result.Result!).Value!).Errors.Single().Message, Does.Contain("master/request"));
        });
    }

    [Test]
    public async Task AnInvalidExportRequestAnswersBadRequest()
    {
        ActionResult<WorkflowConfigurationTransferPackage> result = await CreateController(new RecordingWorkflowApiConnection())
            .Export(new ExportWorkflowConfigurationRequest());

        Assert.That(ErrorPaths(result.Result), Is.EqualTo(new List<string> { Schema.kConfigurationIdPath }));
    }

    [Test]
    public async Task AConfigurationWithoutPhasesAnswersConflict()
    {
        RecordingWorkflowApiConnection api = ExportApi();
        api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase>());

        ActionResult<WorkflowConfigurationTransferPackage> result = await CreateController(api).Export(new ExportWorkflowConfigurationRequest { ConfigurationId = kConfigurationId });

        Assert.That(result.Result, Is.InstanceOf<ConflictObjectResult>());
    }

    [Test]
    public async Task APackageWithTooManyEntriesAnswersConflict()
    {
        RecordingWorkflowApiConnection api = ExportApi();
        int groupCount = Schema.kMaxExportEntries / 2;
        List<StateMatrixTransitionGroup> groups = [.. Enumerable.Range(1, groupCount).Select(id =>
        {
            StateMatrixTransitionGroup group = TransitionGroup();
            group.Id = id;
            group.Name = $"group-{id}";
            return group;
        })];
        WorkflowConfigurationPhase phase = PhaseMapping();
        phase.PhaseMatrix.TransitionGroups = [.. groups.Select(group => new StateMatrixPhaseTransitionGroup { TransitionGroupId = group.Id })];
        api.Respond(RequestQueries.getStateMatrixTransitionGroups, groups);
        api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase> { phase });

        ActionResult<WorkflowConfigurationTransferPackage> result = await CreateController(api).Export(new ExportWorkflowConfigurationRequest { ConfigurationId = kConfigurationId });

        Assert.That(((ConflictObjectResult)result.Result!).Value, Does.Contain(Schema.kMaxExportEntries.ToString()));
    }

    [Test]
    public async Task AFailingExportAnswersServerError()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurationPage, new List<WorkflowConfigurationSummary> { new() { Id = kConfigurationId, Name = kConfigurationName } });

        ActionResult<WorkflowConfigurationTransferPackage> result = await CreateController(api).Export(new ExportWorkflowConfigurationRequest { ConfigurationId = kConfigurationId });

        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    [Test]
    public async Task TheImportCreatesANewConfigurationWithTheCallersConnection()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration> { new() { Id = 1, Name = "Default" } });
        RecordingWorkflowApiConnection callerApi = ImportApi();
        TestableWorkflowConfigurationController controller = CreateController(api, callerApi);

        ActionResult<ImportWorkflowConfigurationResponse> result = await controller.Import(new ImportWorkflowConfigurationRequest
        {
            Package = Package(),
            Options = new() { Name = " Imported " }
        });

        ImportWorkflowConfigurationResponse response = Ok(result);
        Newtonsoft.Json.Linq.JObject variables = Newtonsoft.Json.Linq.JObject.FromObject(
            callerApi.Calls.Single(call => call.Query == RequestQueries.createWorkflowConfiguration).Variables!);
        Assert.Multiple(() =>
        {
            Assert.That(response.ConfigurationId, Is.EqualTo(40));
            Assert.That(response.Created, Is.True);
            Assert.That(response.Statistics.CreatedTransitionGroups, Is.EqualTo(1));
            Assert.That(response.Statistics.AddedPhases, Is.EqualTo(1));
            Assert.That((string?)variables["name"], Is.EqualTo("Imported"));
            Assert.That(api.Calls.Any(call => call.Query == RequestQueries.createWorkflowConfiguration), Is.False, "nothing is written with the middleware connection");
            Assert.That(controller.CallerConnectionsCreated, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TheImportMergesIntoAnExistingConfiguration()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurationPage, new List<WorkflowConfigurationSummary> { new() { Id = kConfigurationId, Name = kConfigurationName } });
        RecordingWorkflowApiConnection callerApi = ImportApi();
        callerApi.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration> { new() { Id = kConfigurationId, Name = kConfigurationName } });
        callerApi.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase>());
        callerApi.Respond(RequestQueries.addWorkflowConfigurationPhase, new ReturnId { NewId = 7 });

        ActionResult<ImportWorkflowConfigurationResponse> result = await CreateController(api, callerApi).Import(new ImportWorkflowConfigurationRequest
        {
            Package = Package(),
            Options = new() { TargetConfigurationId = kConfigurationId }
        });

        ImportWorkflowConfigurationResponse response = Ok(result);
        Assert.Multiple(() =>
        {
            Assert.That(response.ConfigurationId, Is.EqualTo(kConfigurationId));
            Assert.That(response.Created, Is.False);
            Assert.That(response.Statistics.AddedPhases, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AnUnknownMergeTargetIsReportedBeforeTheCallerLookup()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurationPage, new List<WorkflowConfigurationSummary>());
        TestableWorkflowConfigurationController controller = CreateController(api, ImportApi());

        ActionResult<ImportWorkflowConfigurationResponse> result = await controller.Import(new ImportWorkflowConfigurationRequest
        {
            Package = Package(),
            Options = new() { TargetConfigurationId = 9 }
        });

        Assert.Multiple(() =>
        {
            Assert.That(ErrorPaths(result.Result), Is.EqualTo(new List<string> { Schema.kTargetConfigurationIdPath }));
            Assert.That(controller.CallerConnectionsCreated, Is.Zero);
        });
    }

    [Test]
    public async Task AUsedNameAnswersConflictBeforeTheCallerLookup()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration> { new() { Id = 1, Name = "SHARED" } });
        TestableWorkflowConfigurationController controller = CreateController(api, ImportApi());

        ActionResult<ImportWorkflowConfigurationResponse> result = await controller.Import(new ImportWorkflowConfigurationRequest { Package = Package() });

        Assert.Multiple(() =>
        {
            Assert.That(((ConflictObjectResult)result.Result!).Value, Does.Contain(kConfigurationName));
            Assert.That(controller.CallerConnectionsCreated, Is.Zero);
        });
    }

    [Test]
    public async Task AnInvalidImportRequestAnswersBadRequest()
    {
        ActionResult<ImportWorkflowConfigurationResponse> result = await CreateController(new RecordingWorkflowApiConnection())
            .Import(new ImportWorkflowConfigurationRequest());

        Assert.That(ErrorPaths(result.Result), Is.EqualTo(new List<string> { Schema.kPackagePath }));
    }

    [Test]
    public async Task APackageThatDoesNotFitIsReportedAtPackage()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>());
        RecordingWorkflowApiConnection callerApi = ImportApi();
        callerApi.Respond(RequestQueries.getStates, new List<WfState> { new() { Id = 1, Name = "Renamed" } });

        ActionResult<ImportWorkflowConfigurationResponse> result = await CreateController(api, callerApi).Import(new ImportWorkflowConfigurationRequest { Package = Package() });

        Assert.That(ErrorPaths(result.Result), Is.EqualTo(new List<string> { Schema.kPackagePath }));
    }

    [Test]
    public void ImportErrorsAnswerWithMatchingStatusCodes()
    {
        WorkflowConfigurationController controller = CreateController(new RecordingWorkflowApiConnection());

        ObjectResult capacity = controller.ImportErrorResult(new LoginCapacityException("busy", StatusCodes.Status503ServiceUnavailable), "x");
        ObjectResult unknownCaller = controller.ImportErrorResult(new AuthenticationException("gone"), "x");
        ObjectResult usedName = controller.ImportErrorResult(new InvalidOperationException("Uniqueness violation: workflow_configuration_name_key"), "x");
        ObjectResult other = controller.ImportErrorResult(new InvalidOperationException("boom"), "x");

        Assert.Multiple(() =>
        {
            Assert.That(capacity.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
            Assert.That(controller.HttpContext.Response.Headers.RetryAfter.ToString(), Is.EqualTo(LoginThrottle.kRetryAfterSeconds.ToString()));
            Assert.That(unknownCaller.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(usedName.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
            Assert.That(other.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        });
    }

    [Test]
    public async Task AFailedCallerLookupAnswersUnauthorized()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>());
        TestableWorkflowConfigurationController controller = CreateController(api, null);

        ActionResult<ImportWorkflowConfigurationResponse> result = await controller.Import(new ImportWorkflowConfigurationRequest { Package = Package() });

        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    private sealed class TestableWorkflowConfigurationController(ApiConnection apiConnection, ApiConnection? callerConnection)
        : WorkflowConfigurationController(apiConnection, new SimulatedGlobalConfig(), new JwtWriter(new RsaSecurityKey(RSA.Create(2048))), kNoLdaps)
    {
        public int CallerConnectionsCreated { get; private set; }

        internal override Task<ApiConnection> CreateCallerApiConnectionAsync()
        {
            CallerConnectionsCreated++;
            return callerConnection is null
                ? Task.FromException<ApiConnection>(new AuthenticationException("The caller no longer exists."))
                : Task.FromResult(callerConnection);
        }
    }

    private static string? RolesOf(string action) =>
        typeof(WorkflowConfigurationController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

    private static TestableWorkflowConfigurationController CreateController(ApiConnection api, ApiConnection? callerApi = null)
    {
        List<Claim> claims = [new Claim(ClaimTypes.Role, Roles.Admin), new Claim("unique_name", "admin")];
        return new TestableWorkflowConfigurationController(api, callerApi)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
    }

    private static RecordingWorkflowApiConnection ExportApi()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurationPage, new List<WorkflowConfigurationSummary> { new() { Id = kConfigurationId, Name = kConfigurationName } });
        api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration> { new() { Id = kConfigurationId, Name = kConfigurationName } });
        api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase> { PhaseMapping() });
        api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup> { TransitionGroup() });
        api.Respond(RequestQueries.getStates, States());
        api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup>
        {
            new() { Id = 20, Name = "Operators", Members = [new() { VisibilityGroupId = 20, MemberDn = "cn=operators" }] }
        });
        return api;
    }

    private static RecordingWorkflowApiConnection ImportApi()
    {
        RecordingWorkflowApiConnection api = new();
        api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>());
        api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());
        api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup>());
        api.Respond(RequestQueries.getStates, States());
        api.Respond(RequestQueries.createWorkflowVisibilityGroup, new ReturnId { NewId = 20 });
        api.Respond(RequestQueries.createStateMatrixTransitionGroup, new ReturnId { NewId = 30 });
        api.Respond(RequestQueries.createWorkflowConfiguration, new ReturnId { NewId = 40 });
        return api;
    }

    private static WorkflowConfigurationTransferPackage Package() => new()
    {
        Configuration = new()
        {
            Name = kConfigurationName,
            Phases =
            [
                new()
                {
                    TaskType = WfTaskType.master.ToString(),
                    Phase = WorkflowPhases.request.ToString(),
                    Active = true,
                    LowestInputState = 1,
                    LowestStartState = 2,
                    LowestEndState = 3,
                    TransitionGroups = ["Reviewers"]
                }
            ]
        },
        States = [new() { Id = 1, Name = "Open" }, new() { Id = 2, Name = "Automatic approval", AutomaticOnly = true }, new() { Id = 3, Name = "Closed" }],
        TransitionGroups =
        [
            new()
            {
                Name = "Reviewers",
                Phase = WorkflowPhases.request.ToString(),
                Exclusive = true,
                VisibilityGroup = "Operators",
                Transitions = [new() { FromStateId = 1, ToStateId = 2, SortOrder = 0 }]
            }
        ],
        VisibilityGroups = [new() { Name = "Operators", Members = ["cn=operators"] }]
    };

    private static WorkflowConfigurationPhase PhaseMapping() => new()
    {
        TaskType = WfTaskType.master.ToString(),
        Phase = WorkflowPhases.request.ToString(),
        PhaseMatrix = new()
        {
            Active = true,
            LowestInputState = 1,
            LowestStartState = 2,
            LowestEndState = 3,
            TransitionGroups = [new() { TransitionGroupId = 10, SortOrder = 0 }]
        }
    };

    private static StateMatrixTransitionGroup TransitionGroup() => new()
    {
        Id = 10,
        Name = "Reviewers",
        Phase = WorkflowPhases.request.ToString(),
        VisibilityGroupId = 20,
        VisibilityGroup = new() { Id = 20, Name = "Operators" },
        Exclusive = true,
        Transitions = [new() { FromStateId = 1, ToStateId = 2, SortOrder = 0 }]
    };

    private static List<WfState> States() =>
    [
        new() { Id = 1, Name = "Open" },
        new() { Id = 2, Name = "Automatic approval", AutomaticOnly = true },
        new() { Id = 3, Name = "Closed" }
    ];

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
