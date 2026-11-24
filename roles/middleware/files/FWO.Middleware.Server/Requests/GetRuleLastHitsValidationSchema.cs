using FWO.Middleware.Server.Services;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Holds the single authoritative description of every key of the rule last hit request.
/// Validation help text is built from these definitions, so an API documentation change and a validation
/// message cannot contradict one another.
/// </summary>
public static class GetRuleLastHitsValidationSchema
{
    /// <summary>
    /// Name of the endpoint used in validation messages.
    /// </summary>
    public const string kEndpointName = "GetRuleLastHits";

    /// <summary>
    /// JSON path of the root request object.
    /// </summary>
    public const string kRootPath = "";

    /// <summary>
    /// JSON path of the management id key.
    /// </summary>
    public const string kManagementIdPath = "managementId";

    /// <summary>
    /// JSON path of the options object.
    /// </summary>
    public const string kOptionsPath = "options";

    /// <summary>
    /// JSON path of the filter object.
    /// </summary>
    public const string kFilterPath = "options.filter";

    /// <summary>
    /// JSON path of the page size key.
    /// </summary>
    public const string kLimitPath = "options.limit";

    /// <summary>
    /// JSON path of the paging offset key.
    /// </summary>
    public const string kOffsetPath = "options.offset";

    /// <summary>
    /// JSON path of the management id filter key.
    /// </summary>
    public const string kFilterMgmIdPath = "options.filter.mgmId";

    /// <summary>
    /// JSON path of the rule UID filter key.
    /// </summary>
    public const string kFilterRuleUidPath = "options.filter.ruleUid";

    /// <summary>
    /// JSON path of the rule name filter key.
    /// </summary>
    public const string kFilterRuleNamePath = "options.filter.ruleName";

    /// <summary>
    /// JSON path of the last hit upper bound filter key.
    /// </summary>
    public const string kFilterLastHitBeforePath = "options.filter.lastHitBefore";

    /// <summary>
    /// JSON path of the never hit filter key.
    /// </summary>
    public const string kFilterNeverHitPath = "options.filter.neverHit";

    /// <summary>
    /// JSON path of the gateway id filter key.
    /// </summary>
    public const string kFilterDevIdPath = "options.filter.devId";

    /// <summary>
    /// JSON path of the gateway name filter key.
    /// </summary>
    public const string kFilterGatewayNamePath = "options.filter.gatewayName";

    private const string kWildcardHelp = "'*' matches any character sequence and '?' a single character; text without wildcards is a contains search.";

    private static readonly List<RequestKeyDefinition> kRootKeys =
    [
        new RequestKeyDefinition("managementId", "Required. Database id of the management whose rule last hits are returned, greater than 0. A management not visible to the caller answers with an empty list."),
        new RequestKeyDefinition("options", "Optional output options. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kOptionsKeys =
    [
        new RequestKeyDefinition("filter", "Optional response filter; all supplied keys must match. Omitted or null applies no restriction."),
        new RequestKeyDefinition("limit", $"Optional page size. {ListPaging.kLimitDescription}"),
        new RequestKeyDefinition("offset", $"Optional. {ListPaging.kOffsetDescription}")
    ];

    private static readonly List<RequestKeyDefinition> kFilterKeys =
    [
        new RequestKeyDefinition("mgmId", "Optional exact management id filter, greater than 0; a value other than managementId matches no rule. Null applies no restriction."),
        new RequestKeyDefinition("ruleUid", $"Optional list of 1 to {RuleLastHitFilter.kMaxFilterValues} rule UIDs of at most {RuleLastHitFilter.kMaxFilterTextLength} characters each; a rule matches when its UID equals one of them. Null applies no restriction."),
        new RequestKeyDefinition("ruleName", $"Optional case-insensitive rule name filter of at most {RuleLastHitFilter.kMaxFilterTextLength} characters; {kWildcardHelp} Null applies no restriction."),
        new RequestKeyDefinition("lastHitAfter", "Optional inclusive lower bound of the aggregated last hit (ISO 8601 with offset, e.g. 2026-01-31T00:00:00Z). Rules never hit do not match. Null applies no restriction."),
        new RequestKeyDefinition("lastHitBefore", "Optional exclusive upper bound of the aggregated last hit (ISO 8601 with offset), later than lastHitAfter. Rules never hit do not match. Null applies no restriction."),
        new RequestKeyDefinition("neverHit", "Optional. True returns only rules without any hit, false only rules with a hit. Must not be true together with lastHitAfter or lastHitBefore. Null applies no restriction."),
        new RequestKeyDefinition("devId", "Optional exact gateway id filter, greater than 0; selects the rules hit on that gateway and limits 'gateways' to it. Null applies no restriction."),
        new RequestKeyDefinition("gatewayName", $"Optional case-insensitive gateway name filter of at most {RuleLastHitFilter.kMaxFilterTextLength} characters; {kWildcardHelp} Selects the rules hit on a matching gateway and limits 'gateways' to the matching ones. Null applies no restriction.")
    ];

    /// <summary>
    /// Gets the allowed root keys.
    /// </summary>
    public static IReadOnlyList<RequestKeyDefinition> RootKeys => kRootKeys;

    /// <summary>
    /// Gets the allowed keys inside <c>options</c>.
    /// </summary>
    public static IReadOnlyList<RequestKeyDefinition> OptionsKeys => kOptionsKeys;

    /// <summary>
    /// Gets the allowed keys inside <c>options.filter</c>.
    /// </summary>
    public static IReadOnlyList<RequestKeyDefinition> FilterKeys => kFilterKeys;

    /// <summary>
    /// Returns the authoritative description of one filter key.
    /// </summary>
    /// <param name="jsonName">JSON name of the filter key.</param>
    /// <returns>The description, or an empty text for an unknown key.</returns>
    public static string DescribeFilterKey(string jsonName)
    {
        return kFilterKeys.FirstOrDefault(key => key.JsonName == jsonName)?.Description ?? string.Empty;
    }
}
