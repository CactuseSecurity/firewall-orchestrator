using System.Security.Authentication;
using System.Security.Claims;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using FWO.Services.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Schema = FWO.Middleware.Server.Requests.WorkflowConfigurationValidationSchema;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Lists, exports and imports workflow configurations as ID-independent packages, e.g. to move a workflow to another
/// installation or to set up test systems reproducibly. The packages are the same as those of the workflow settings page.
/// </summary>
[Authorize]
[ApiController]
[Tags("Workflow configuration")]
[Route("api/[controller]")]
[AggregatedValidationErrors]
public class WorkflowConfigurationController(ApiConnection apiConnection, GlobalConfig globalConfig, JwtWriter jwtWriter, List<Ldap> ldaps)
    : ControllerBase
{
    private const string kUniqueNameViolation = "workflow_configuration_name_key";
    private const string kImportTitle = "Import Workflow Configuration";

    /// <summary>
    /// Lists the workflow configurations without their phases, ordered by name and then id.
    /// </summary>
    /// <remarks>
    /// <c>options.limit</c> and <c>options.offset</c> page the result deterministically. Without a limit a page of at most
    /// the configured <c>maxRestResponseObjectLimit</c> configurations (default 10000) is returned, and the
    /// <c>X-Has-More</c> response header tells whether further configurations follow.
    /// </remarks>
    [HttpPost("List")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [ProducesResponseType(typeof(List<WorkflowConfigurationSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<WorkflowConfigurationSummary>>> List([FromBody] ListWorkflowConfigurationsRequest request)
    {
        int maxLimit = ListPaging.GetMaxObjectLimit(globalConfig);
        RequestValidationErrorResponse requestErrors = WorkflowConfigurationRequestValidator.ValidateList(request, maxLimit);
        if (requestErrors.Errors.Count > 0)
        {
            return BadRequest(requestErrors);
        }
        try
        {
            int pageSize = ListPaging.GetPageSize(request.Options.Limit, maxLimit);
            List<WorkflowConfigurationSummary> fetched = await new WorkflowConfigurationListService(apiConnection)
                .GetConfigurationsAsync(ToFilter(request.Options.Filter), pageSize + 1, request.Options.Offset ?? 0);
            ListPage<WorkflowConfigurationSummary> page = ListPage<WorkflowConfigurationSummary>.FromLookahead(fetched, pageSize);
            ListPaging.SetHasMoreHeader(HttpContext, page.HasMore);
            return Ok(page.Items);
        }
        catch (Exception exception)
        {
            Log.WriteError("List Workflow Configurations", "Error while listing workflow configurations.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Exports a workflow configuration as package.
    /// </summary>
    /// <remarks>
    /// The package addresses transition groups and visibility groups by name and holds the definitions of all states it
    /// references, so it fits every installation. It contains the selected phases (all by default), the transition groups
    /// linked to them and, with <c>options.includeVisibilityGroups</c>, their visibility groups with the member DNs. An
    /// unknown configuration answers 404, a configuration without phases or with more than 10000 package entries 409.
    /// </remarks>
    [HttpPost("Export")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [ProducesResponseType(typeof(WorkflowConfigurationTransferPackage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<WorkflowConfigurationTransferPackage>> Export([FromBody] ExportWorkflowConfigurationRequest request)
    {
        RequestValidationErrorResponse requestErrors = WorkflowConfigurationRequestValidator.ValidateExport(request);
        if (requestErrors.Errors.Count > 0)
        {
            return BadRequest(requestErrors);
        }
        int configurationId = request.ConfigurationId!.Value;
        try
        {
            if (await FindConfigurationAsync(configurationId) is null)
            {
                return NotFound($"There is no workflow configuration with id {configurationId}.");
            }
            WorkflowConfigurationTransferService service = new(apiConnection);
            if (request.Options.Phases is not null)
            {
                RequestValidationErrorResponse phaseErrors = WorkflowConfigurationRequestValidator.ValidateSelectedPhasesExist(
                    request.Options.Phases, await service.GetPhaseMappings(configurationId));
                if (phaseErrors.Errors.Count > 0)
                {
                    return BadRequest(phaseErrors);
                }
            }
            WorkflowConfigurationTransferPackage package = await service.Export(configurationId,
                request.Options.IncludeVisibilityGroups ?? false, WorkflowConfigurationRequestValidator.SelectedPhaseKeys(request.Options.Phases));
            if (CountEntries(package) > Schema.kMaxExportEntries)
            {
                return Conflict($"The workflow configuration has more than {Schema.kMaxExportEntries} package entries and cannot be exported.");
            }
            return Ok(package);
        }
        catch (InvalidDataException exception)
        {
            return Conflict(exception.Message);
        }
        catch (Exception exception)
        {
            Log.WriteError("Export Workflow Configuration", $"Error while exporting workflow configuration {configurationId}.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Imports a package as a new, inactive workflow configuration or merges it into an existing one.
    /// </summary>
    /// <remarks>
    /// The package is validated completely before anything is written: its structure, the state definitions against the
    /// states of this installation, existing transition and visibility groups of the same name against the package, and
    /// for a merge the derived states of existing phases. Missing states, transition groups and visibility groups are
    /// created. If a write fails, everything written by the import is rolled back. The import runs with the caller's own
    /// admin rights, so the caller is looked up in the directories first. A package that does not fit answers 400 at
    /// <c>package</c>; a name that is already used answers 409.
    /// </remarks>
    [HttpPost("Import")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [ProducesResponseType(typeof(ImportWorkflowConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ImportWorkflowConfigurationResponse>> Import([FromBody] ImportWorkflowConfigurationRequest request)
    {
        RequestValidationErrorResponse requestErrors = WorkflowConfigurationRequestValidator.ValidateImport(request);
        if (requestErrors.Errors.Count > 0)
        {
            return BadRequest(requestErrors);
        }
        int? targetId = request.Options.TargetConfigurationId;
        string newName = targetId is null ? WorkflowConfigurationRequestValidator.NewConfigurationName(request) : "";
        try
        {
            ActionResult? targetError = await CheckImportTargetAsync(targetId, newName);
            if (targetError is not null)
            {
                return targetError;
            }
            using ApiConnection callerConnection = await CreateCallerApiConnectionAsync();
            WorkflowConfigurationTransferService service = new(callerConnection);
            int configurationId = await service.Import(request.Package!, newName, targetId);
            Log.WriteAudit(kImportTitle, $"Imported workflow configuration package '{request.Package!.Configuration.Name}' "
                + (targetId is null ? $"as new configuration '{newName}'" : "into configuration") + $" (id {configurationId}).",
                CallerName(), CallerDn());
            return Ok(new ImportWorkflowConfigurationResponse
            {
                ConfigurationId = configurationId,
                Created = targetId is null,
                Statistics = service.LastImportStatistics
            });
        }
        catch (Exception exception)
        {
            return ImportErrorResult(exception, newName);
        }
    }

    /// <summary>
    /// Opens an API connection with the caller's own admin rights. The caller is rebuilt from the directories, so a
    /// caller who lost the admin role in the meantime cannot write.
    /// </summary>
    internal virtual async Task<ApiConnection> CreateCallerApiConnectionAsync()
    {
        AuthManager authManager = new(jwtWriter, ldaps, apiConnection);
        // the local user id keeps the rebuilt user in the caller's directory, as the dn alone may exist in several (SEC-11)
        UiUser caller = new()
        {
            Name = CallerName(),
            Dn = CallerDn(),
            DbId = int.TryParse(User.FindFirstValue("x-hasura-user-id"), out int callerDbId) ? callerDbId : 0
        };
        string jwt = await authManager.AuthorizeUserAsync(caller, validatePassword: false);
        ApiConnection callerConnection = new GraphQlApiConnection(ConfigFile.ApiServerUri, jwt);
        callerConnection.SetRole(Roles.Admin);
        return callerConnection;
    }

    /// <summary>
    /// Answers a failed import: package conflicts with 400 at <c>package</c>, a used name with 409, a busy or failed
    /// directory lookup of the caller with its status, everything else with 500.
    /// </summary>
    internal ObjectResult ImportErrorResult(Exception exception, string newName)
    {
        switch (exception)
        {
            case InvalidDataException invalidData:
                RequestValidationErrorResponse errors = new();
                errors.Errors.Add(AggregatedRequestKeyValidation.BuildError(Schema.kPackagePath, invalidData.Message));
                return BadRequest(errors);
            case LoginCapacityException capacityException:
                if (HttpContext != null)
                {
                    HttpContext.Response.Headers.RetryAfter = LoginThrottle.kRetryAfterSeconds.ToString();
                }
                return StatusCode(capacityException.StatusCode, capacityException.Message);
            case AuthenticationException authenticationException:
                Log.WriteWarning(kImportTitle, $"Caller could not be rebuilt from the directories: {authenticationException.Message}");
                return StatusCode(StatusCodes.Status401Unauthorized, authenticationException.Message);
            case Exception when exception.Message.Contains(kUniqueNameViolation, StringComparison.Ordinal):
                return Conflict($"A workflow configuration named '{newName}' already exists.");
            default:
                Log.WriteError(kImportTitle, "Error while importing a workflow configuration.", exception);
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Checks before the caller lookup that a merge target exists and that a new name is not used yet.
    /// </summary>
    private async Task<ActionResult?> CheckImportTargetAsync(int? targetId, string newName)
    {
        if (targetId is int id)
        {
            if (await FindConfigurationAsync(id) is not null)
            {
                return null;
            }
            RequestValidationErrorResponse errors = new();
            errors.Errors.Add(AggregatedRequestKeyValidation.BuildError(Schema.kTargetConfigurationIdPath,
                $"There is no workflow configuration with id {id}."));
            return BadRequest(errors);
        }
        List<WorkflowConfiguration> configurations = await apiConnection.SendQueryAsync<List<WorkflowConfiguration>>(
            RequestQueries.getWorkflowConfigurations);
        return configurations.Any(configuration => string.Equals(configuration.Name, newName, StringComparison.OrdinalIgnoreCase))
            ? Conflict($"A workflow configuration named '{newName}' already exists.")
            : null;
    }

    private async Task<WorkflowConfigurationSummary?> FindConfigurationAsync(int configurationId)
    {
        List<WorkflowConfigurationSummary> found = await new WorkflowConfigurationListService(apiConnection)
            .GetConfigurationsAsync(new WorkflowConfigurationFilter { Id = configurationId }, 1, 0);
        return found.FirstOrDefault();
    }

    private static int CountEntries(WorkflowConfigurationTransferPackage package) =>
        package.Configuration.Phases.Count + package.States.Count + package.TransitionGroups.Count
        + package.TransitionGroups.Sum(group => group.Transitions.Count) + (package.VisibilityGroups?.Count ?? 0);

    private static WorkflowConfigurationFilter? ToFilter(WorkflowConfigurationFilterRequest? filter) => filter is null ? null : new()
    {
        Id = filter.Id,
        Name = filter.Name,
        Description = filter.Description,
        IsActive = filter.IsActive
    };

    private string CallerName() => User.FindFirstValue("unique_name") ?? User.Identity?.Name ?? "";

    private string CallerDn() => User.FindFirstValue("x-hasura-uuid") ?? "";
}
