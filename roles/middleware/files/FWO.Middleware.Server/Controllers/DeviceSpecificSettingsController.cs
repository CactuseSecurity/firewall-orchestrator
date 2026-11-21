using System.Security.Claims;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data.DeviceSpecificSettings;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using FWO.Services.DeviceSpecificSettings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Exports and imports the device specific settings and keeps named versions (snapshots) of them, e.g. to set up test
/// systems reproducibly or to move the settings to another installation. Levels are addressed by natural keys, so a
/// package fits every installation with the same device types, managements and gateways.
/// </summary>
[Authorize]
[ApiController]
[Tags("Device specific settings")]
[Route("api/[controller]")]
[AggregatedValidationErrors]
public class DeviceSpecificSettingsController(ApiConnection apiConnection, DeviceSpecificSettingsManager manager, GlobalConfig globalConfig) : ControllerBase
{
    private const string kUniqueNameViolation = "device_specific_config_snapshot_name_key";
    private const string kUniquenessViolation = "Uniqueness violation";

    /// <summary>
    /// Exports the stored device specific settings as a package (getDSS).
    /// </summary>
    /// <remarks>
    /// One entry per stored level with at least one override, addressed by device type name and version, management name
    /// or management and gateway name. Settings referencing other objects hold the object's name (<c>scCredential</c> the
    /// credential name); secrets are never exported. Levels whose object no longer exists are left out and logged. The
    /// package holds at most 10000 levels; a larger tree answers 409.
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [ProducesResponseType(typeof(DeviceSpecificSettingsPackage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DeviceSpecificSettingsPackage>> Export()
    {
        try
        {
            return Ok(await ExportCurrentAsync());
        }
        catch (CollectionLimitExceededException exception)
        {
            return Conflict($"The device specific settings have more than {exception.Limit} levels and cannot be exported.");
        }
        catch (Exception exception)
        {
            Log.WriteError("Export Device Specific Settings", "Error while exporting the device specific settings.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Validates a package against this installation and applies it (addDSS).
    /// </summary>
    /// <remarks>
    /// The whole package is validated before anything is written: only known keys, only at the levels they may be set at,
    /// values checked like on the settings page, levels and referenced objects resolved by name. Unknown device types,
    /// managements and gateways are reported, never created. All errors answer 400 with their path below <c>package</c>,
    /// and nothing is written. Otherwise all changes are written in one transaction; with <c>options.dryRun</c> only the
    /// changes are returned.
    /// </remarks>
    [HttpPost("Import")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [ProducesResponseType(typeof(ImportDeviceSpecificSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ImportDeviceSpecificSettingsResponse>> Import([FromBody] ImportDeviceSpecificSettingsRequest request)
    {
        RequestValidationErrorResponse requestErrors = DeviceSpecificSettingsRequestValidator.ValidateImport(request);
        if (requestErrors.Errors.Count > 0)
        {
            return BadRequest(requestErrors);
        }

        DeviceSpecificSettingsImportMode mode = DeviceSpecificSettingsRequestValidator.ParseMode(request.Options.Mode)!.Value;
        bool dryRun = request.Options.DryRun ?? false;
        try
        {
            DeviceSpecificSettingsImportPlan plan = await CreateTransferService().ImportAsync(request.Package, mode, dryRun);
            if (plan.HasErrors)
            {
                return BadRequest(DeviceSpecificSettingsRequestValidator.FromImportIssues(plan.Errors));
            }

            bool applied = !dryRun && plan.HasChanges;
            if (applied)
            {
                Log.WriteAudit("Import Device Specific Settings", $"Imported device specific settings ({mode}): "
                    + $"{plan.Nodes.Sum(node => node.Changes.Count)} change(s) on {plan.Nodes.Count(node => node.Changes.Count > 0)} level(s).",
                    CallerName(), CallerDn());
            }
            return Ok(new ImportDeviceSpecificSettingsResponse { Applied = applied, Mode = mode, Nodes = plan.Nodes });
        }
        catch (Exception exception)
        {
            Log.WriteError("Import Device Specific Settings", "Error while importing device specific settings.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Lists the stored snapshots without their packages, newest first.
    /// </summary>
    /// <remarks>
    /// Snapshots are ordered by creation time and then id, both descending, so <c>options.limit</c> and
    /// <c>options.offset</c> page the result deterministically. Without a limit a page of at most the configured
    /// <c>maxRestResponseObjectLimit</c> snapshots (default 10000) is returned, and the <c>X-Has-More</c> response header
    /// tells whether further snapshots follow.
    /// </remarks>
    [HttpPost("Snapshots/List")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [ProducesResponseType(typeof(List<DeviceSpecificSettingsSnapshotSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<DeviceSpecificSettingsSnapshotSummary>>> ListSnapshots([FromBody] ListDeviceSpecificSettingsSnapshotsRequest request)
    {
        int maxLimit = ListPaging.GetMaxObjectLimit(globalConfig);
        RequestValidationErrorResponse requestErrors = DeviceSpecificSettingsRequestValidator.ValidateListSnapshots(request, maxLimit);
        if (requestErrors.Errors.Count > 0)
        {
            return BadRequest(requestErrors);
        }

        try
        {
            int pageSize = ListPaging.GetPageSize(request.Options.Limit, maxLimit);
            List<DeviceSpecificSettingsSnapshotSummary> fetched = await new DeviceSpecificSettingsSnapshotService(apiConnection)
                .GetSnapshotsAsync(ToFilter(request.Options.Filter), pageSize + 1, request.Options.Offset ?? 0);
            ListPage<DeviceSpecificSettingsSnapshotSummary> page = ListPage<DeviceSpecificSettingsSnapshotSummary>.FromLookahead(fetched, pageSize);
            ListPaging.SetHasMoreHeader(HttpContext, page.HasMore);
            return Ok(page.Items);
        }
        catch (Exception exception)
        {
            Log.WriteError("List Device Specific Settings Snapshots", "Error while listing device specific settings snapshots.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Returns a stored snapshot with its package, ready to be passed to <c>POST /api/DeviceSpecificSettings/Import</c>.
    /// </summary>
    /// <param name="id">Snapshot id.</param>
    [HttpGet("Snapshots/{id:long}")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [ProducesResponseType(typeof(DeviceSpecificSettingsSnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DeviceSpecificSettingsSnapshotResponse>> GetSnapshot(long id)
    {
        try
        {
            DeviceSpecificSettingsSnapshotData? snapshot = await new DeviceSpecificSettingsSnapshotService(apiConnection).GetSnapshotAsync(id);
            if (snapshot is null)
            {
                return NotFound();
            }
            return Ok(new DeviceSpecificSettingsSnapshotResponse
            {
                Id = snapshot.Id,
                Name = snapshot.Name,
                Description = snapshot.Description,
                FwoVersion = snapshot.FwoVersion,
                Created = snapshot.Created,
                Package = DeviceSpecificSettingsPackageJson.FromStorage(snapshot.Content)
            });
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Device Specific Settings Snapshot", $"Error while reading device specific settings snapshot {id}.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Stores a package, or the current settings of this installation, as a new snapshot.
    /// </summary>
    /// <remarks>
    /// A given package is only checked for its structure, so versions of other installations can be kept; it is
    /// validated against this installation when it is imported. A name that is already used answers 409.
    /// </remarks>
    [HttpPost("Snapshots")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [ProducesResponseType(typeof(DeviceSpecificSettingsSnapshotSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RequestValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DeviceSpecificSettingsSnapshotSummary>> AddSnapshot([FromBody] AddDeviceSpecificSettingsSnapshotRequest request)
    {
        RequestValidationErrorResponse requestErrors = DeviceSpecificSettingsRequestValidator.ValidateAddSnapshot(request);
        if (requestErrors.Errors.Count > 0)
        {
            return BadRequest(requestErrors);
        }

        try
        {
            DeviceSpecificSettingsPackage package = request.Package ?? await ExportCurrentAsync();
            DeviceSpecificSettingsSnapshotSummary snapshot = await new DeviceSpecificSettingsSnapshotService(apiConnection)
                .AddSnapshotAsync(request.Name!, request.Description, package);
            Log.WriteAudit("Add Device Specific Settings Snapshot", $"Stored device specific settings snapshot '{snapshot.Name}' (id {snapshot.Id}).",
                CallerName(), CallerDn());
            return Ok(snapshot);
        }
        catch (CollectionLimitExceededException exception)
        {
            return Conflict($"The device specific settings have more than {exception.Limit} levels and cannot be exported.");
        }
        catch (InvalidOperationException exception) when (IsUniqueNameViolation(exception))
        {
            return Conflict($"A snapshot named '{request.Name!.Trim()}' already exists.");
        }
        catch (Exception exception)
        {
            Log.WriteError("Add Device Specific Settings Snapshot", "Error while storing a device specific settings snapshot.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    private DeviceSpecificSettingsTransferService CreateTransferService() =>
        new(apiConnection, manager, globalConfig.ProductVersion);

    private async Task<DeviceSpecificSettingsPackage> ExportCurrentAsync()
    {
        DeviceSpecificSettingsExportResult result = await CreateTransferService().ExportAsync();
        foreach (string warning in result.Warnings)
        {
            Log.WriteWarning("Export Device Specific Settings", warning);
        }
        return result.Package;
    }

    private static DeviceSpecificSettingsSnapshotFilter? ToFilter(DeviceSpecificSettingsSnapshotFilterRequest? filter) => filter is null ? null : new()
    {
        Id = filter.Id,
        Name = filter.Name,
        Description = filter.Description,
        FwoVersion = filter.FwoVersion,
        Created = filter.Created
    };

    private static bool IsUniqueNameViolation(InvalidOperationException exception) =>
        exception.Message.Contains(kUniqueNameViolation, StringComparison.Ordinal)
        || exception.Message.Contains(kUniquenessViolation, StringComparison.OrdinalIgnoreCase);

    private string CallerName() => User.FindFirstValue("unique_name") ?? User.Identity?.Name ?? "";

    private string CallerDn() => User.FindFirstValue("x-hasura-uuid") ?? "";
}
