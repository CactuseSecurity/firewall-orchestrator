using System.Security.Claims;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides owner lookup endpoints.
/// </summary>
[Authorize]
[ApiController]
[Route("api/owners")]
public class OwnersController(ApiConnection apiConnection) : ControllerBase
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
    /// Unknown request properties are rejected with <c>400 Bad Request</c>, as are non-positive ids and text
    /// filters that exceed 256 characters or contain control characters.
    /// </remarks>
    [HttpPost("get")]
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
            if (ValidateRequest(effectiveRequest) is string validationError)
            {
                return BadRequest(validationError);
            }

            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(
                OwnerQueries.getOwnersFiltered,
                BuildQueryVariables(effectiveRequest, User)) ?? [];

            return Ok(owners.Select(owner => ToResponse(owner, effectiveRequest.ShowDetails == true)).ToList());
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
    /// <returns>An error message describing the first invalid value, or <c>null</c> when the request is valid.</returns>
    internal static string? ValidateRequest(GetOwnersRequest request)
    {
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
    /// Builds GraphQL variables for the owner lookup.
    /// </summary>
    internal static Dictionary<string, object> BuildQueryVariables(GetOwnersRequest request, ClaimsPrincipal user)
    {
        List<Dictionary<string, object>> predicates = BuildFilterPredicates(request);
        if (ShouldRestrictToEditableOwners(user))
        {
            predicates.Add(BuildInExpression("id", JwtClaimParser.ExtractIntClaimValues(user.Claims, "x-hasura-editable-owners")));
        }

        Dictionary<string, object> whereClause = predicates.Count switch
        {
            0 => [],
            1 => predicates[0],
            _ => new Dictionary<string, object> { ["_and"] = predicates }
        };
        return new Dictionary<string, object> { ["where"] = whereClause };
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
        AddEqualsPredicate(predicates, "id", request.OwnerId);
        AddEqualsPredicate(predicates, "owner_lifecycle_state_id", request.OwnerLifeCycleStateId);
        AddEqualsPredicate(predicates, "active", request.Active);
        AddWildcardPredicate(predicates, "name", request.Name);
        AddWildcardPredicate(predicates, "app_id_external", request.AppIdExternal);
        AddActiveStatePredicate(predicates, request.ShowOnlyActiveState);
        return predicates;
    }

    /// <summary>
    /// Excludes owners whose lifecycle state is inactive, while keeping owners without a lifecycle state.
    /// Applied by default unless <paramref name="showOnlyActiveState"/> is explicitly <c>false</c>.
    /// </summary>
    private static void AddActiveStatePredicate(List<Dictionary<string, object>> predicates, bool? showOnlyActiveState)
    {
        if (showOnlyActiveState == false)
        {
            return;
        }

        predicates.Add(new Dictionary<string, object>
        {
            ["_or"] = new List<Dictionary<string, object>>
            {
                new() { ["owner_lifecycle_state"] = new Dictionary<string, object> { ["active_state"] = new Dictionary<string, object> { ["_eq"] = true } } },
                new() { ["owner_lifecycle_state_id"] = new Dictionary<string, object> { ["_is_null"] = true } }
            }
        });
    }

    private static void AddEqualsPredicate(List<Dictionary<string, object>> predicates, string fieldName, object? value)
    {
        if (value is not null)
        {
            predicates.Add(new Dictionary<string, object> { [fieldName] = new Dictionary<string, object> { ["_eq"] = value } });
        }
    }

    private static void AddWildcardPredicate(List<Dictionary<string, object>> predicates, string fieldName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            predicates.Add(new Dictionary<string, object> { [fieldName] = new Dictionary<string, object> { ["_ilike"] = BuildLikePattern(value) } });
        }
    }

    private static Dictionary<string, object> BuildInExpression(string fieldName, List<int> values)
    {
        return new Dictionary<string, object> { [fieldName] = new Dictionary<string, object> { ["_in"] = values } };
    }

    private static bool ShouldRestrictToEditableOwners(ClaimsPrincipal user)
    {
        return user.IsInRole(Roles.Modeller) && !user.IsInRole(Roles.Admin) && !user.IsInRole(Roles.Auditor);
    }

    private static bool IsStandardOwner(string? appIdExternal)
    {
        return appIdExternal?.Contains("app", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Builds an <c>_ilike</c> pattern from a user-supplied filter value.
    /// Literal SQL wildcards (<c>\</c>, <c>%</c>, <c>_</c>) in the input are escaped so they are matched verbatim,
    /// while the documented <c>*</c> and <c>?</c> wildcards are translated to <c>%</c> and <c>_</c>.
    /// Plain text without <c>*</c>/<c>?</c> is wrapped for a contains search.
    /// </summary>
    private static string BuildLikePattern(string value)
    {
        string trimmedValue = value.Trim();
        bool hasWildcard = trimmedValue.Contains('*') || trimmedValue.Contains('?');
        string escapedValue = trimmedValue
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        string pattern = escapedValue.Replace('*', '%').Replace('?', '_');
        return hasWildcard ? pattern : $"%{pattern}%";
    }
}
