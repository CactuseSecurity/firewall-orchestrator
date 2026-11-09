using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides read-only modelling object endpoints: interfaces available to an application, app roles of an
/// application, and network areas.
/// </summary>
[Authorize]
[ApiController]
[Route("api/modelling")]
public class ModellingObjectsController(ApiConnection apiConnection, GlobalConfig globalConfig) : ControllerBase
{
    /// <summary>
    /// Returns the published interfaces an application may use.
    /// </summary>
    /// <remarks>
    /// Requires the <c>admin</c>, <c>auditor</c>, or <c>modeller</c> role. A caller with only the modeller role may
    /// only ask for applications in the <c>x-hasura-editable-owners</c> JWT claim; for any other application, as for
    /// an unknown one, the response is <c>404</c>. The application is selected by the required <c>appIdExternal</c>,
    /// ignoring case. Only published interfaces are returned; requested, rejected and decommissioned interfaces are
    /// not. Public interfaces are always returned, restricted interfaces only when the application is one of their
    /// permitted owners, and private interfaces only when the application provides them. The application's own
    /// interfaces are always returned. Source and destination list app servers, app roles, network areas and other
    /// network groups separately, each with resolved members; deleted objects are left out. Addresses use compact
    /// notation: a single IP for a host, CIDR notation for a network and <c>ip1-ip2</c> for a range. Services use
    /// <c>port/protocol</c>, <c>portStart-portEnd/protocol</c>, or the protocol alone. Interfaces are ordered by name
    /// and id; <c>options.limit</c> (at most the configured <c>maxRestResponseObjectLimit</c>, default 10000) and
    /// <c>options.offset</c> page them, and the <c>X-Has-More</c> response header tells whether further interfaces follow.
    /// The members of an interface are not paged but always returned complete, so a page holds every object of its
    /// interfaces; clients receiving too large pages send a smaller <c>options.limit</c>.
    /// </remarks>
    [HttpPost("getAvailableInterfacesForApp")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AvailableInterfacesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Modeller}")]
    public async Task<ActionResult<AvailableInterfacesResponse>> GetAvailableInterfacesForApp(
        [FromBody] GetAvailableInterfacesRequest request)
    {
        int maxLimit = ListPaging.GetMaxObjectLimit(globalConfig);
        Dictionary<string, string[]> validationErrors = ModellingObjectRequestValidator.Validate(request, maxLimit);
        if (validationErrors.Count > 0)
        {
            return BuildValidationProblem(validationErrors);
        }

        try
        {
            FwoOwner? application = await GetVisibleApplicationAsync(request.AppIdExternal!);
            if (application is null)
            {
                return NotFound();
            }

            ModellingListOptions<AvailableInterfacesFilter> options = request.Options!;
            List<ModellingInterfaceQueryResult> interfaces = await apiConnection.SendQueryAsync<List<ModellingInterfaceQueryResult>>(
                ModellingQueries.getAvailableInterfacesForApp,
                ModellingObjectQueryBuilder.BuildInterfaceVariables(application.Id, options, maxLimit)) ?? [];
            ListPage<ModellingInterfaceQueryResult> page = ListPage<ModellingInterfaceQueryResult>.FromLookahead(
                interfaces, ListPaging.GetPageSize(options.Limit, maxLimit));
            ListPaging.SetHasMoreHeader(HttpContext, page.HasMore);
            return Ok(new AvailableInterfacesResponse
            {
                Interfaces = page.Items.Select(ModellingObjectResponseMapper.ToInterfaceResponse).ToList()
            });
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Available Interfaces", "Error while fetching available interfaces.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Returns the app roles of an application.
    /// </summary>
    /// <remarks>
    /// Requires the <c>admin</c>, <c>auditor</c>, or <c>modeller</c> role. A caller with only the modeller role may
    /// only ask for applications in the <c>x-hasura-editable-owners</c> JWT claim; for any other application, as for
    /// an unknown one, the response is <c>404</c>. The application is selected by the required <c>appIdExternal</c>,
    /// ignoring case. Deleted app roles, deleted app servers and AppZones are not returned. Each app role lists its
    /// app servers and the distinct addresses of all of them in compact notation. App roles are ordered by name and
    /// id; <c>options.limit</c> (at most the configured <c>maxRestResponseObjectLimit</c>, default 10000) and
    /// <c>options.offset</c> page them, and the <c>X-Has-More</c> response header tells whether further app roles follow.
    /// The app servers of an app role are not paged but always returned complete.
    /// </remarks>
    [HttpPost("getAppRolesPerApp")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AppRolesPerAppResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Modeller}")]
    public async Task<ActionResult<AppRolesPerAppResponse>> GetAppRolesPerApp([FromBody] GetAppRolesPerAppRequest request)
    {
        int maxLimit = ListPaging.GetMaxObjectLimit(globalConfig);
        Dictionary<string, string[]> validationErrors = ModellingObjectRequestValidator.Validate(request, maxLimit);
        if (validationErrors.Count > 0)
        {
            return BuildValidationProblem(validationErrors);
        }

        try
        {
            FwoOwner? application = await GetVisibleApplicationAsync(request.AppIdExternal!);
            if (application is null)
            {
                return NotFound();
            }

            ModellingListOptions<ModellingGroupFilter> options = request.Options!;
            List<ModellingAppRole> appRoles = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(
                ModellingQueries.getAppRolesPerApp,
                ModellingObjectQueryBuilder.BuildAppRoleVariables(application.Id, options, maxLimit)) ?? [];
            ListPage<ModellingAppRole> page = ListPage<ModellingAppRole>.FromLookahead(
                appRoles, ListPaging.GetPageSize(options.Limit, maxLimit));
            ListPaging.SetHasMoreHeader(HttpContext, page.HasMore);
            return Ok(new AppRolesPerAppResponse
            {
                AppRoles = page.Items.Select(ModellingObjectResponseMapper.ToAppRoleResponse).ToList()
            });
        }
        catch (Exception exception)
        {
            Log.WriteError("Get App Roles", "Error while fetching app roles.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Returns network areas with their subnets.
    /// </summary>
    /// <remarks>
    /// Requires the <c>admin</c>, <c>auditor</c>, or <c>modeller</c> role. A caller with only the modeller role is
    /// served only if at least one application in the <c>x-hasura-editable-owners</c> JWT claim may use common
    /// services; otherwise the response is <c>403</c>. The body may be omitted. Deleted areas and subnets are not
    /// returned. <c>options.filter.idString</c> matches the area id string ignoring case, with <c>*</c> and <c>?</c>
    /// wildcards; without wildcards it must match exactly. Each area lists the distinct addresses of its subnets in
    /// compact notation. Areas are ordered by name and id; <c>options.limit</c> (at most the configured
    /// <c>maxRestResponseObjectLimit</c>, default 10000) and <c>options.offset</c> page them, and the
    /// <c>X-Has-More</c> response header tells whether further areas follow. The subnets of an area are not paged but
    /// always returned complete; clients receiving too large pages send a smaller <c>options.limit</c>.
    /// </remarks>
    [HttpPost("getNetworkAreas")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(NetworkAreasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Modeller}")]
    public async Task<ActionResult<NetworkAreasResponse>> GetNetworkAreas(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] GetNetworkAreasRequest? request)
    {
        GetNetworkAreasRequest effectiveRequest = request ?? new GetNetworkAreasRequest();
        int maxLimit = ListPaging.GetMaxObjectLimit(globalConfig);
        Dictionary<string, string[]> validationErrors = ModellingObjectRequestValidator.Validate(effectiveRequest, maxLimit);
        if (validationErrors.Count > 0)
        {
            return BuildValidationProblem(validationErrors);
        }

        try
        {
            if (!await MayReadNetworkAreasAsync())
            {
                return Forbid();
            }

            ModellingListOptions<ModellingGroupFilter> options = effectiveRequest.Options!;
            List<ModellingNetworkArea> areas = await apiConnection.SendQueryAsync<List<ModellingNetworkArea>>(
                ModellingQueries.getNetworkAreas,
                ModellingObjectQueryBuilder.BuildNetworkAreaVariables(options, maxLimit)) ?? [];
            ListPage<ModellingNetworkArea> page = ListPage<ModellingNetworkArea>.FromLookahead(
                areas, ListPaging.GetPageSize(options.Limit, maxLimit));
            ListPaging.SetHasMoreHeader(HttpContext, page.HasMore);
            return Ok(new NetworkAreasResponse
            {
                NetworkAreas = page.Items.Select(ModellingObjectResponseMapper.ToNetworkAreaResponse).ToList()
            });
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Network Areas", "Error while fetching network areas.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Selects the application with the supplied external id, ignoring case; an exact match wins over matches that
    /// differ only in casing. Returns null if no single application matches or a modeller may not see it.
    /// </summary>
    /// <param name="appIdExternal">External application id from the request.</param>
    internal async Task<FwoOwner?> GetVisibleApplicationAsync(string appIdExternal)
    {
        List<FwoOwner> candidates = await apiConnection.SendQueryAsync<List<FwoOwner>>(
            OwnerQueries.getApplicationIdentifiers,
            ModellingObjectQueryBuilder.BuildApplicationLookupVariables(appIdExternal)) ?? [];
        string trimmedAppIdExternal = appIdExternal.Trim();
        FwoOwner? application = candidates.FirstOrDefault(candidate => candidate.ExtAppId == trimmedAppIdExternal)
            ?? (candidates.Count == 1 ? candidates[0] : null);
        if (application is null || !ApplicationAddressQueryBuilder.ShouldRestrictToEditableApplications(User))
        {
            return application;
        }

        List<int> editableApplicationIds = JwtClaimParser.ExtractIntClaimValues(
            User.Claims, ApplicationAddressQueryBuilder.kEditableOwnersClaim);
        return editableApplicationIds.Contains(application.Id) ? application : null;
    }

    /// <summary>
    /// Tells whether the caller may read network areas: admins and auditors always, modellers only if one of their
    /// applications may use common services.
    /// </summary>
    internal async Task<bool> MayReadNetworkAreasAsync()
    {
        if (!ApplicationAddressQueryBuilder.ShouldRestrictToEditableApplications(User))
        {
            return true;
        }

        List<int> editableApplicationIds = JwtClaimParser.ExtractIntClaimValues(
            User.Claims, ApplicationAddressQueryBuilder.kEditableOwnersClaim);
        if (editableApplicationIds.Count == 0)
        {
            return false;
        }

        List<FwoOwner> commonServiceApplications = await apiConnection.SendQueryAsync<List<FwoOwner>>(
            OwnerQueries.getApplicationIdentifiers,
            ModellingObjectQueryBuilder.BuildCommonServiceApplicationVariables(editableApplicationIds)) ?? [];
        return commonServiceApplications.Count > 0;
    }

    private static BadRequestObjectResult BuildValidationProblem(Dictionary<string, string[]> validationErrors)
    {
        return new BadRequestObjectResult(new ValidationProblemDetails(validationErrors)
        {
            Status = StatusCodes.Status400BadRequest
        });
    }
}
