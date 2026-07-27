using FWO.Basics;
using FWO.Data;
using FWO.Middleware.Server.Requests;
using System.Security.Claims;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Builds the GraphQL variables of the application-address lookup so that every filter is applied by the API
/// instead of the middleware.
/// </summary>
public static class ApplicationAddressQueryBuilder
{
    private const string kEditableOwnersClaim = "x-hasura-editable-owners";

    /// <summary>
    /// Builds the variables selecting the applications visible to the caller, including optional paging.
    /// </summary>
    public static Dictionary<string, object> BuildApplicationVariables(
        GetApplicationAddressesOptions options, ClaimsPrincipal user)
    {
        ApplicationAddressFilter? filter = options.Filter;
        List<Dictionary<string, object>> predicates = [];
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "is_default", false);
        AddApplicationIdPredicate(predicates, filter?.ApplicationId);
        GraphQlFilterBuilder.AddWildcardPredicates(predicates, "name", filter?.ApplicationName);
        GraphQlFilterBuilder.AddWildcardPredicates(predicates, "app_id_external", filter?.AppIdExternal);
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
    /// Builds the variables selecting the app-server addresses of the supplied applications.
    /// </summary>
    public static Dictionary<string, object> BuildApplicationAddressVariables(List<int> applicationIds)
    {
        List<Dictionary<string, object>> predicates = [];
        GraphQlFilterBuilder.AddInPredicate(predicates, "owner_id", applicationIds);
        return new Dictionary<string, object> { ["where"] = GraphQlFilterBuilder.CombinePredicates(predicates) };
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

    private static void AddApplicationIdPredicate(List<Dictionary<string, object>> predicates, List<int>? applicationIds)
    {
        if (applicationIds is { Count: > 0 })
        {
            GraphQlFilterBuilder.AddInPredicate(predicates, "id", applicationIds.Distinct().ToList());
        }
    }
}
