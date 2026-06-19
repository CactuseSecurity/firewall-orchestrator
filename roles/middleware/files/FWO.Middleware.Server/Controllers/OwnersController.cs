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
    /// Example response:
    /// <code>
    /// [
    ///   {"id":42,"name":"Finance Portal","appIdExternal":"APP-4711","type":"standard"},
    ///   {"id":43,"name":"Finance Network","appIdExternal":"NET-4712","type":"infrastructure"}
    /// ]
    /// </code>
    /// The <c>name</c> and <c>appIdExternal</c> filters are case-insensitive and accept <c>*</c> for any
    /// character sequence and <c>?</c> for a single character. Plain text without wildcards is matched as a contains search.
    /// </remarks>
    [HttpPost("get")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(List<GetOwnerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    [Authorize(Roles = $"{Roles.Auditor}, {Roles.Admin}, {Roles.Modeller}")]
    public async Task<ActionResult<List<GetOwnerResponse>>> Get([FromBody] GetOwnersRequest? request)
    {
        try
        {
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(
                OwnerQueries.getOwnersFiltered,
                BuildQueryVariables(request ?? new GetOwnersRequest(), User)) ?? [];

            return Ok(owners.Select(ToResponse).ToList());
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Owners", "Error while fetching owners.", exception);
            return StatusCode(500, "Internal server error");
        }
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
    internal static GetOwnerResponse ToResponse(FwoOwner owner)
    {
        return new GetOwnerResponse
        {
            Id = owner.Id,
            Name = owner.Name,
            AppIdExternal = owner.ExtAppId,
            Type = IsStandardOwner(owner.ExtAppId) ? StandardOwnerType : InfrastructureOwnerType
        };
    }

    private static List<Dictionary<string, object>> BuildFilterPredicates(GetOwnersRequest request)
    {
        List<Dictionary<string, object>> predicates = [];
        AddEqualsPredicate(predicates, "id", request.OwnerId);
        AddEqualsPredicate(predicates, "owner_lifecycle_state_id", request.OwnerLifeCycleStateId);
        AddEqualsPredicate(predicates, "active", request.Active);
        AddWildcardPredicate(predicates, "name", request.Name);
        AddWildcardPredicate(predicates, "app_id_external", request.AppIdExternal);
        return predicates;
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

    private static string BuildLikePattern(string value)
    {
        string trimmedValue = value.Trim();
        string pattern = trimmedValue.Replace('*', '%').Replace('?', '_');
        bool hasWildcard = pattern.Contains('%') || pattern.Contains('_');
        return hasWildcard ? pattern : $"%{pattern}%";
    }
}
