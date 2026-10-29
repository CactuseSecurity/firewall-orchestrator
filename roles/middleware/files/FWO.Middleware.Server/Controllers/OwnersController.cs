using System.Security.Claims;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides owner lookup endpoints.
/// </summary>
[Authorize]
[ApiController]
[Route("api/owners")]
public class OwnersController(ApiConnection apiConnection, GlobalConfig globalConfig) : ControllerBase
{
    private const string StandardOwnerType = "standard";
    private const string InfrastructureOwnerType = "infrastructure";
    internal const int kMaxFilterTextLength = 256;

    /// <summary>
    /// Returns all owners visible to the caller with optional AND-combined filters.
    /// </summary>
    /// <remarks>
    /// Requires one of the roles <c>admin</c>, <c>auditor</c>, or <c>modeller</c>.
    /// Modeller callers only receive owners listed in their <c>x-hasura-editable-owners</c> JWT claim.
    ///
    /// Example request bodies:
    /// <code>
    /// {}
    /// </code>
    /// <code>
    /// {"active":true,"ownerLifecycleStateId":1}
    /// </code>
    /// <code>
    /// {"ownerId":42}
    /// </code>
    /// <code>
    /// {"name":"Finance*","appIdExternal":"APP-?"}
    /// </code>
    /// <code>
    /// {"showDetails":true}
    /// </code>
    /// Example response:
    /// <code>
    /// [
    ///   {"id":42,"name":"Finance Portal","appIdExternal":"APP-4711","type":"standard","ownerLifecycleState":{"id":1,"name":"Active"}},
    ///   {"id":43,"name":"Finance Network","appIdExternal":"NET-4712","type":"infrastructure","ownerLifecycleState":null}
    /// ]
    /// </code>
    /// The <c>type</c> field is derived from the owner's <c>appIdExternal</c>: it is <c>standard</c> when the
    /// external app id contains <c>app</c> (case-insensitive), and <c>infrastructure</c> otherwise (including
    /// owners without an external app id).
    /// Set <c>showDetails</c> to <c>true</c> to additionally return all owner fields (responsibles, tenant id,
    /// recertification data, criticality, lifecycle state id, additional info, etc.). By default only the core fields are returned.
    /// By default owners with an inactive lifecycle state are excluded; set <c>showOnlyActiveState</c> to
    /// <c>false</c> to also include them. Owners without any lifecycle state are always returned.
    /// The <c>name</c> and <c>appIdExternal</c> filters are case-insensitive and accept <c>*</c> for any
    /// character sequence and <c>?</c> for a single character. Plain text without wildcards is matched as a contains
    /// search, and literal <c>%</c>, <c>_</c>, and <c>\</c> characters are matched verbatim.
    /// Owners are ordered by name and id, so <c>limit</c> and <c>offset</c> page the result deterministically.
    /// Without a limit a page of at most the configured <c>maxRestResponseObjectLimit</c> owners (default 10000) is
    /// returned, and the <c>X-Has-More</c> response header tells whether further owners follow.
    /// Unknown request properties are rejected with <c>400 Bad Request</c>, as are non-positive ids, text
    /// filters that exceed 256 characters or contain control characters, and a <c>limit</c> or <c>offset</c>
    /// out of range.
    /// </remarks>
    [HttpPost("get")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(List<GetOwnerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    [Authorize(Roles = $"{Roles.Auditor}, {Roles.Admin}, {Roles.Modeller}")]
    public async Task<ActionResult<List<GetOwnerResponse>>> Get([FromBody] GetOwnersRequest? request)
    {
        try
        {
            GetOwnersRequest effectiveRequest = request ?? new GetOwnersRequest();
            int maxLimit = ListPaging.GetMaxObjectLimit(globalConfig);
            if (ValidateRequest(effectiveRequest, maxLimit) is string validationError)
            {
                return BadRequest(validationError);
            }

            int pageSize = ListPaging.GetPageSize(effectiveRequest.Limit, maxLimit);
            List<FwoOwner> fetchedOwners = await apiConnection.SendQueryAsync<List<FwoOwner>>(
                OwnerQueries.getOwnersFiltered,
                BuildQueryVariables(effectiveRequest, User, pageSize)) ?? [];
            ListPage<FwoOwner> owners = ListPage<FwoOwner>.FromLookahead(fetchedOwners, pageSize);

            ListPaging.SetHasMoreHeader(HttpContext, owners.HasMore);
            return Ok(owners.Items.Select(owner => ToResponse(owner, effectiveRequest.ShowDetails == true)).ToList());
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Owners", "Error while fetching owners.", exception);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Validates and sanitizes the supplied filter values before they are used to build the query.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    /// <returns>An error message describing every invalid paging key or the first invalid filter value, or
    /// <c>null</c> when the request is valid.</returns>
    internal static string? ValidateRequest(GetOwnersRequest request, int maxLimit)
    {
        Dictionary<string, string[]> pagingErrors = ListPaging.ValidatePaging(request.Limit, request.Offset, maxLimit);
        if (pagingErrors.Count > 0)
        {
            return $"Invalid paging: {string.Join(" ", pagingErrors.Values.SelectMany(errors => errors))}";
        }
        if (request.OwnerId is <= 0)
        {
            return "ownerId must be a positive integer.";
        }
        if (request.OwnerLifeCycleStateId is <= 0)
        {
            return "ownerLifecycleStateId must be a positive integer.";
        }
        return ValidateFilterText(request.Name, "name") ?? ValidateFilterText(request.AppIdExternal, "appIdExternal");
    }

    /// <summary>
    /// Ensures a text filter stays within the allowed length and contains no control characters.
    /// </summary>
    private static string? ValidateFilterText(string? value, string fieldName)
    {
        if (value is null)
        {
            return null;
        }
        if (value.Length > kMaxFilterTextLength)
        {
            return $"{fieldName} must not exceed {kMaxFilterTextLength} characters.";
        }
        if (value.Any(char.IsControl))
        {
            return $"{fieldName} must not contain control characters.";
        }
        return null;
    }

    /// <summary>
    /// Builds GraphQL variables for the owner lookup, including paging. One owner more than the page size is
    /// requested, so the caller can tell whether further owners follow.
    /// </summary>
    internal static Dictionary<string, object> BuildQueryVariables(GetOwnersRequest request, ClaimsPrincipal user, int pageSize)
    {
        List<Dictionary<string, object>> predicates = BuildFilterPredicates(request);
        if (ShouldRestrictToEditableOwners(user))
        {
            predicates.Add(GraphQlFilterBuilder.BuildInExpression("id", JwtClaimParser.ExtractIntClaimValues(user.Claims, "x-hasura-editable-owners")));
        }

        Dictionary<string, object> variables = new() { ["where"] = GraphQlFilterBuilder.CombinePredicates(predicates) };
        ListPaging.AddLookaheadPagingVariables(variables, pageSize, request.Offset);
        return variables;
    }

    /// <summary>
    /// Converts an owner to the REST response shape.
    /// </summary>
    /// <param name="owner">The owner to convert.</param>
    /// <param name="showDetails">Whether to include all owner detail fields.</param>
    internal static GetOwnerResponse ToResponse(FwoOwner owner, bool showDetails)
    {
        GetOwnerResponse response = new()
        {
            Id = owner.Id,
            Name = owner.Name,
            AppIdExternal = owner.ExtAppId,
            Type = IsStandardOwner(owner.ExtAppId) ? StandardOwnerType : InfrastructureOwnerType,
            OwnerLifecycleState = owner.OwnerLifeCycleState is null
                ? null
                : new OwnerLifecycleStateResponse
                {
                    Id = owner.OwnerLifeCycleState.Id,
                    Name = owner.OwnerLifeCycleState.Name
                }
        };

        if (showDetails)
        {
            AddDetails(response, owner);
        }

        return response;
    }

    /// <summary>
    /// Populates the full set of owner fields on the response.
    /// </summary>
    private static void AddDetails(GetOwnerResponse response, FwoOwner owner)
    {
        response.OwnerResponsibles = owner.OwnerResponsibles
            .Select(responsible => new OwnerResponsibleResponse
            {
                Dn = responsible.Dn,
                ResponsibleType = responsible.ResponsibleTypeId
            })
            .ToList();
        response.IsDefault = owner.IsDefault;
        response.TenantId = owner.TenantId;
        response.RecertInterval = owner.RecertInterval;
        response.LastRecertCheck = owner.LastRecertCheck;
        response.RecertCheckParams = owner.RecertCheckParamString;
        response.Criticality = owner.Criticality;
        response.OwnerLifecycleStateId = owner.OwnerLifeCycleStateId;
        response.Active = owner.Active;
        response.ImportSource = owner.ImportSource;
        response.CommonServicePossible = owner.CommSvcPossible;
        response.LastRecertified = owner.LastRecertified;
        response.LastRecertifier = owner.LastRecertifierId;
        response.LastRecertifierDn = owner.LastRecertifierDn;
        response.NextRecertDate = owner.NextRecertDate;
        response.RecertActive = owner.RecertActive;
        response.DecommDate = owner.DecommDate;
        response.AdditionalInfo = owner.AdditionalInfo;
    }

    private static List<Dictionary<string, object>> BuildFilterPredicates(GetOwnersRequest request)
    {
        List<Dictionary<string, object>> predicates = [];
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "id", request.OwnerId);
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "owner_lifecycle_state_id", request.OwnerLifeCycleStateId);
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "active", request.Active);
        GraphQlFilterBuilder.AddWildcardPredicate(predicates, "name", request.Name);
        GraphQlFilterBuilder.AddWildcardPredicate(predicates, "app_id_external", request.AppIdExternal);
        GraphQlFilterBuilder.AddOwnerActiveStatePredicate(predicates, request.ShowOnlyActiveState);
        return predicates;
    }

    private static bool ShouldRestrictToEditableOwners(ClaimsPrincipal user)
    {
        return user.IsInRole(Roles.Modeller) && !user.IsInRole(Roles.Admin) && !user.IsInRole(Roles.Auditor);
    }

    private static bool IsStandardOwner(string? appIdExternal)
    {
        return appIdExternal?.Contains("app", StringComparison.OrdinalIgnoreCase) == true;
    }
}
