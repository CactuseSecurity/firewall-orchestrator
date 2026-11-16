using FWO.Data.Modelling;
using FWO.Middleware.Server.Requests;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Builds the GraphQL variables of the modelling object endpoints so that filters and paging are applied by the API
/// instead of the middleware.
/// </summary>
public static class ModellingObjectQueryBuilder
{
    /// <summary>
    /// Number of applications read when resolving an external application id. Ids differing only in casing can
    /// exist; the exact match is preferred among them.
    /// </summary>
    public const int kMaxApplicationCandidates = 10;

    private const string kInterfacePermissionField = "interface_permission";

    /// <summary>
    /// Builds the variables selecting the applications whose external id matches the supplied id, ignoring case.
    /// </summary>
    public static Dictionary<string, object> BuildApplicationLookupVariables(string appIdExternal)
    {
        return new Dictionary<string, object>
        {
            ["where"] = new Dictionary<string, object>
            {
                ["app_id_external"] = new Dictionary<string, object>
                {
                    ["_ilike"] = GraphQlFilterBuilder.BuildCaseInsensitiveEqualsPattern(appIdExternal)
                }
            },
            ["limit"] = kMaxApplicationCandidates,
            ["offset"] = 0
        };
    }

    /// <summary>
    /// Builds the variables selecting at most one of the supplied applications that may use common services.
    /// </summary>
    public static Dictionary<string, object> BuildCommonServiceApplicationVariables(List<int> applicationIds)
    {
        List<Dictionary<string, object>> predicates = [];
        GraphQlFilterBuilder.AddInPredicate(predicates, "id", applicationIds);
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "common_service_possible", true);
        return new Dictionary<string, object>
        {
            ["where"] = GraphQlFilterBuilder.CombinePredicates(predicates),
            ["limit"] = 1,
            ["offset"] = 0
        };
    }

    /// <summary>
    /// Builds the variables selecting one page of the interfaces available to an application. The visibility rules
    /// are part of the query itself; the variables only add the caller's filters and paging.
    /// </summary>
    public static Dictionary<string, object> BuildInterfaceVariables(int applicationId,
        ModellingListOptions<AvailableInterfacesFilter> options, int maxLimit)
    {
        AvailableInterfacesFilter? filter = options.Filter;
        List<Dictionary<string, object>> predicates = [];
        AddIdPredicate(predicates, "id", filter?.ConnectionId);
        GraphQlFilterBuilder.AddWildcardPredicates(predicates, "name", filter?.InterfaceName);
        GraphQlFilterBuilder.AddWildcardPredicates(predicates, "reason", filter?.InterfaceDescription);
        AddPermissionPredicate(predicates, filter?.Permission);
        AddOwnerPredicate(predicates, "name", filter?.ProvidingAppName, false);
        AddOwnerPredicate(predicates, "app_id_external", filter?.ProvidingAppIdExternal, true);

        Dictionary<string, object> variables = new()
        {
            ["appId"] = applicationId,
            ["where"] = GraphQlFilterBuilder.CombinePredicates(predicates)
        };
        ListPaging.AddLookaheadPagingVariables(variables, ListPaging.GetPageSize(options.Limit, maxLimit), options.Offset);
        return variables;
    }

    /// <summary>
    /// Builds the variables selecting one page of the app roles of an application.
    /// </summary>
    public static Dictionary<string, object> BuildAppRoleVariables(int applicationId,
        ModellingListOptions<ModellingGroupFilter> options, int maxLimit)
    {
        List<Dictionary<string, object>> predicates = [];
        GraphQlFilterBuilder.AddEqualsPredicate(predicates, "app_id", applicationId);
        return BuildGroupVariables(predicates, options, maxLimit);
    }

    /// <summary>
    /// Builds the variables selecting one page of network areas.
    /// </summary>
    public static Dictionary<string, object> BuildNetworkAreaVariables(ModellingListOptions<ModellingGroupFilter> options, int maxLimit)
    {
        List<Dictionary<string, object>> predicates = [];
        return BuildGroupVariables(predicates, options, maxLimit);
    }

    private static Dictionary<string, object> BuildGroupVariables(List<Dictionary<string, object>> predicates,
        ModellingListOptions<ModellingGroupFilter> options, int maxLimit)
    {
        ModellingGroupFilter? filter = options.Filter;
        AddIdPredicate(predicates, "id", filter?.Id);
        GraphQlFilterBuilder.AddWildcardPredicates(predicates, "id_string", filter?.IdString, matchPlainTextExactly: true);
        GraphQlFilterBuilder.AddWildcardPredicates(predicates, "name", filter?.Name);

        Dictionary<string, object> variables = new()
        {
            ["where"] = GraphQlFilterBuilder.CombinePredicates(predicates)
        };
        ListPaging.AddLookaheadPagingVariables(variables, ListPaging.GetPageSize(options.Limit, maxLimit), options.Offset);
        return variables;
    }

    private static void AddIdPredicate<T>(List<Dictionary<string, object>> predicates, string fieldName, List<T>? ids)
    {
        if (ids is { Count: > 0 })
        {
            GraphQlFilterBuilder.AddInPredicate(predicates, fieldName, ids.Distinct().ToList());
        }
    }

    private static void AddOwnerPredicate(List<Dictionary<string, object>> predicates, string ownerField,
        List<string>? values, bool matchPlainTextExactly)
    {
        List<Dictionary<string, object>> ownerPredicates = [];
        GraphQlFilterBuilder.AddWildcardPredicates(ownerPredicates, ownerField, values, matchPlainTextExactly);
        if (ownerPredicates.Count > 0)
        {
            predicates.Add(new Dictionary<string, object> { ["owner"] = ownerPredicates[0] });
        }
    }

    private static void AddPermissionPredicate(List<Dictionary<string, object>> predicates, List<string>? permissions)
    {
        if (permissions is not { Count: > 0 })
        {
            return;
        }

        string publicPermission = InterfacePermissions.Public.ToString();
        List<Dictionary<string, object>> alternatives = [];
        foreach (string permission in permissions.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            alternatives.Add(BuildPermissionExpression("_ilike", GraphQlFilterBuilder.BuildCaseInsensitiveEqualsPattern(permission)));
            if (permission.Equals(publicPermission, StringComparison.OrdinalIgnoreCase))
            {
                alternatives.Add(BuildPermissionExpression("_is_null", true));
            }
        }
        predicates.Add(new Dictionary<string, object> { ["_or"] = alternatives });
    }

    private static Dictionary<string, object> BuildPermissionExpression(string comparison, object value)
    {
        return new Dictionary<string, object>
        {
            [kInterfacePermissionField] = new Dictionary<string, object> { [comparison] = value }
        };
    }
}
