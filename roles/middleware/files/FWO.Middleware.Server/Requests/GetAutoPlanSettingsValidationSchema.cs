using FWO.Data.Middleware;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Holds the single authoritative description of every key of the auto plan settings request. Validation help
/// text is built from these definitions, so an API documentation change and a validation message cannot
/// contradict one another.
/// </summary>
public static class GetAutoPlanSettingsValidationSchema
{
    /// <summary>
    /// Name of the endpoint used in validation messages.
    /// </summary>
    public const string kEndpointName = "getAutoPlanSettings";

    /// <summary>
    /// JSON path of the root request object.
    /// </summary>
    public const string kRootPath = "";

    /// <summary>
    /// JSON path of the gateway id list.
    /// </summary>
    public const string kDeviceIdsPath = "deviceIds";

    /// <summary>
    /// JSON path of the management id list.
    /// </summary>
    public const string kManagementIdsPath = "managementIds";

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
    /// JSON path of the scope type filter key.
    /// </summary>
    public const string kFilterScopeTypePath = "options.filter.scopeType";

    /// <summary>
    /// Scope types accepted by the scope type filter.
    /// </summary>
    public static readonly IReadOnlyList<string> ScopeTypes = [AutoPlanScopeTypes.kGlobal, AutoPlanScopeTypes.kManagement, AutoPlanScopeTypes.kGateway];

    private static readonly List<RequestKeyDefinition> kRootKeys =
    [
        new RequestKeyDefinition("deviceIds", $"Database ids of the gateways whose effective settings are requested, each greater than 0, at most {GetAutoPlanSettingsRequest.kMaxIds}. Defaults to []; null is treated like the default."),
        new RequestKeyDefinition("managementIds", $"Database ids of the managements whose effective settings are requested, each greater than 0, at most {GetAutoPlanSettingsRequest.kMaxIds}. Defaults to []; null is treated like the default."),
        new RequestKeyDefinition("options", "Optional output options. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kOptionsKeys =
    [
        new RequestKeyDefinition("filter", "Optional response filter. Omitted or null applies no restriction."),
        new RequestKeyDefinition("limit", $"Optional page size between 1 and {GetAutoPlanSettingsOptions.kMaxLimit}. Omitted or null returns a page of {GetAutoPlanSettingsOptions.kMaxLimit} settings; the X-Has-More response header tells whether further settings follow."),
        new RequestKeyDefinition("offset", "Optional number of settings to skip, not negative. Omitted or null skips no setting.")
    ];

    private static readonly List<RequestKeyDefinition> kFilterKeys =
    [
        new RequestKeyDefinition("scopeType", $"Optional exact scope type filter, one of {string.Join(", ", ScopeTypes)}. Null applies no restriction."),
        new RequestKeyDefinition("id", "Optional exact management or gateway id filter. Null applies no restriction."),
        new RequestKeyDefinition("autoPlanImplementation", "Optional filter on the effective autoPlanImplementation value. Null applies no restriction."),
        new RequestKeyDefinition("autoPlanImplementationForRules", "Optional filter on the effective autoPlanImplementationForRules value. Null applies no restriction.")
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
    /// Builds the help text listing the valid keys of one request object.
    /// </summary>
    /// <param name="allowedKeys">Keys the object accepts.</param>
    /// <returns>Help text naming every allowed key and its description.</returns>
    public static string DescribeKeys(IReadOnlyList<RequestKeyDefinition> allowedKeys)
    {
        return AggregatedRequestKeyValidation.DescribeKeys(allowedKeys);
    }
}
