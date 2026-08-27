using System.Security.Claims;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
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
    private static readonly RequestValidationSchema OwnersSchema = RequestValidationSchema
        .EndpointWithOptions(nameof(Get), options => options
            .OptionalObject("filter", filter => filter
                .OptionalInt("ownerId")
                .OptionalInt("ownerLifecycleStateId")
                .OptionalBool("active")
                .OptionalString("name")
                .OptionalString("appIdExternal"))
            .OptionalBool("showDetails")
            .OptionalBool("showOnlyActiveState"));

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
    /// {"options":{"filter":{"active":true,"ownerLifecycleStateId":1}}}
    /// </code>
    /// <code>
    /// {"options":{"filter":{"ownerId":42}}}
    /// </code>
    /// <code>
    /// {"options":{"filter":{"name":"Finance*","appIdExternal":"APP-?"}}}
    /// </code>
    /// <code>
    /// {"options":{"showDetails":true}}
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
    /// The <c>options</c> root key defaults to <c>{}</c> when omitted. Every field in <c>options.filter</c> is
    /// nullable; omitted or null filter fields do not restrict the result. Set <c>options.showDetails</c> to
    /// <c>true</c> to additionally return all owner fields (responsibles, tenant id,
    /// recertification data, criticality, lifecycle state id, additional info, etc.). By default only the core fields are returned.
    /// By default owners with an inactive lifecycle state are excluded; set <c>options.showOnlyActiveState</c> to
    /// <c>false</c> to also include them. Owners without any lifecycle state are always returned.
    /// The <c>options.filter.name</c> and <c>options.filter.appIdExternal</c> filters are case-insensitive and accept <c>*</c> for any
    /// character sequence and <c>?</c> for a single character. Plain text without wildcards is matched as a contains
    /// search, and literal <c>%</c>, <c>_</c>, and <c>\</c> characters are matched verbatim.
    /// Unknown request properties and semantic validation failures return <see cref="ValidationProblemDetails"/>
    /// with <c>400 Bad Request</c>. Semantic validation rejects non-positive ids and text filters that exceed 256
    /// characters or contain control characters. Malformed JSON and incorrect JSON value types are handled by ASP.NET model binding.
    /// </remarks>
    [HttpPost("get")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(List<GetOwnerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    [Authorize(Roles = $"{Roles.Auditor}, {Roles.Admin}, {Roles.Modeller}")]
    public async Task<ActionResult<List<GetOwnerResponse>>> Get([FromBody] GetOwnersRequest? request)
    {
        try
        {
            if (!RequestValidator.TryValidate(request, OwnersSchema, out ActionResult? errorResult))
            {
                return errorResult!;
            }
            if (TryValidateSemantics(request!, out errorResult))
            {
                return errorResult!;
            }

            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(
                OwnerQueries.getOwnersFiltered,
                BuildQueryVariables(request!, User)) ?? [];

            return Ok(owners.Select(owner => ToResponse(owner, request!.Options?.ShowDetails == true)).ToList());
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Owners", "Error while fetching owners.", exception);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Validates supplied filter values after request shape validation and before they are used to build the query.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="errorResult">The aggregated validation error result when validation fails.</param>
    /// <returns><c>true</c> when validation failed; otherwise <c>false</c>.</returns>
    internal static bool TryValidateSemantics(GetOwnersRequest request, out ActionResult? errorResult)
    {
        RequestValidationErrors errors = new();
        GetOwnersFilter? filter = request.Options?.Filter;
        if (filter?.OwnerId is <= 0)
        {
            errors.Add("options.filter.ownerId", "The owner database id must be a positive integer.");
        }
        if (filter?.OwnerLifeCycleStateId is <= 0)
        {
            errors.Add("options.filter.ownerLifecycleStateId", "The owner lifecycle-state database id must be a positive integer.");
        }
        AddFilterTextError(errors, filter?.Name, "options.filter.name", "owner name");
        AddFilterTextError(errors, filter?.AppIdExternal, "options.filter.appIdExternal", "external application id");

        if (!errors.HasErrors)
        {
            errorResult = null;
            return false;
        }

        errorResult = RequestValidationProblemDetailsFactory.BadRequest(errors);
        return true;
    }

    /// <summary>
    /// Ensures a text filter stays within the allowed length and contains no control characters.
    /// </summary>
    private static void AddFilterTextError(RequestValidationErrors errors, string? value, string fieldPath, string description)
    {
        if (value is null)
        {
            return;
        }
        if (value.Length > kMaxFilterTextLength)
        {
            errors.Add(fieldPath, $"The {description} must not exceed {kMaxFilterTextLength} characters.");
        }
        if (value.Any(char.IsControl))
        {
            errors.Add(fieldPath, $"The {description} must not contain control characters.");
        }
    }

    /// <summary>
    /// Builds GraphQL variables for the owner lookup.
    /// </summary>
    internal static Dictionary<string, object> BuildQueryVariables(GetOwnersRequest request, ClaimsPrincipal user)
    {
        List<Dictionary<string, object>> predicates = BuildFilterPredicates(request.Options?.Filter, request.Options?.ShowOnlyActiveState);
        if (ShouldRestrictToEditableOwners(user))
        {
            predicates.Add(GraphQlFilterBuilder.BuildInExpression("id", JwtClaimParser.ExtractIntClaimValues(user.Claims, "x-hasura-editable-owners")));
        }

        return new Dictionary<string, object> { ["where"] = GraphQlFilterBuilder.CombinePredicates(predicates) };
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

    private static List<Dictionary<string, object>> BuildFilterPredicates(GetOwnersFilter? filter, bool? showOnlyActiveState)
    {
        List<Dictionary<string, object>> predicates = [];
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "id", filter?.OwnerId);
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "owner_lifecycle_state_id", filter?.OwnerLifeCycleStateId);
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "active", filter?.Active);
        GraphQlFilterBuilder.AddWildcardPredicate(predicates, "name", filter?.Name);
        GraphQlFilterBuilder.AddWildcardPredicate(predicates, "app_id_external", filter?.AppIdExternal);
        GraphQlFilterBuilder.AddOwnerActiveStatePredicate(predicates, showOnlyActiveState);
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
