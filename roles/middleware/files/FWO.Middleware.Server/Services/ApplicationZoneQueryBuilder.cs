using FWO.Basics;
using FWO.Data;
using FWO.Middleware.Server.Requests;
using System.Security.Claims;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Builds the GraphQL variables of the application-zone lookup so that every filter is applied by the API
/// instead of the middleware.
/// </summary>
public static class ApplicationZoneQueryBuilder
{
    private const string kEditableOwnersClaim = "x-hasura-editable-owners";

    /// <summary>
    /// Builds the variables selecting the applications visible to the caller, including optional paging.
    /// </summary>
    public static Dictionary<string, object> BuildApplicationVariables(
        GetApplicationZonesOptions options, ClaimsPrincipal user)
    {
        ApplicationZoneFilter? filter = options.Filter;
        List<Dictionary<string, object>> predicates = [];
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "is_default", false);
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "id", filter?.ApplicationId);
        GraphQlFilterBuilder.AddWildcardPredicate(predicates, "name", filter?.ApplicationName);
        GraphQlFilterBuilder.AddWildcardPredicate(predicates, "app_id_external", filter?.AppIdExternal);
        GraphQlFilterBuilder.AddOwnerActiveStatePredicate(predicates, options.ShowOnlyActiveState);
        if (ShouldRestrictToEditableApplications(user))
        {
            predicates.Add(GraphQlFilterBuilder.BuildInExpression(
                "id", JwtClaimParser.ExtractIntClaimValues(user.Claims, kEditableOwnersClaim)));
        }

        Dictionary<string, object> variables = new()
        {
            ["where"] = GraphQlFilterBuilder.CombinePredicates(predicates)
        };
        AddPagingValue(variables, "limit", options.Limit);
        AddPagingValue(variables, "offset", options.Offset);
        return variables;
    }

    /// <summary>
    /// Builds the variables selecting the application zones of the supplied applications.
    /// </summary>
    public static Dictionary<string, object> BuildApplicationZoneVariables(
        List<int> applicationIds, ApplicationZoneFilter? filter)
    {
        List<Dictionary<string, object>> predicates = [];
        GraphQlFilterBuilder.AddInPredicate(predicates, "app_id", applicationIds);
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "id", filter?.Id);
        GraphQlFilterBuilder.AddWildcardPredicate(predicates, "name", filter?.Name);
        GraphQlFilterBuilder.AddWildcardPredicate(predicates, "id_string", filter?.IdString);
        return new Dictionary<string, object> { ["where"] = GraphQlFilterBuilder.CombinePredicates(predicates) };
    }

    /// <summary>
    /// Indicates whether the filter restricts concrete application zones, which excludes the placeholders
    /// returned for applications without an application zone.
    /// </summary>
    public static bool HasZoneFilter(ApplicationZoneFilter? filter)
    {
        return filter is not null &&
            (filter.Id is not null || filter.Name is not null || filter.IdString is not null || filter.IsDeleted is not null);
    }

    /// <summary>
    /// Indicates whether the caller only sees the applications listed in the editable-owners claim.
    /// </summary>
    public static bool ShouldRestrictToEditableApplications(ClaimsPrincipal user)
    {
        return user.IsInRole(Roles.Modeller) && !user.IsInRole(Roles.Admin) && !user.IsInRole(Roles.Auditor);
    }

    private static void AddPagingValue(Dictionary<string, object> variables, string fieldName, int? value)
    {
        if (value is not null)
        {
            variables[fieldName] = value.Value;
        }
    }
}
