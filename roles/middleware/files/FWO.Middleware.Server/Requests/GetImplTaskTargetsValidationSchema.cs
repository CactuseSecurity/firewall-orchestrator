namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Holds the single authoritative description of every key of the implementation task target request.
/// Validation help text is built from these definitions, so an API documentation change and a validation
/// message cannot contradict one another.
/// </summary>
public static class GetImplTaskTargetsValidationSchema
{
    /// <summary>
    /// Name of the endpoint used in validation messages.
    /// </summary>
    public const string kEndpointName = "getImplTaskTargets";

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

    private static readonly List<RequestKeyDefinition> kRootKeys =
    [
        new RequestKeyDefinition("managementId", "Database id of the management the request task belongs to. Required and greater than 0."),
        new RequestKeyDefinition("isServiceObject", "True for services, false for network objects; selects serviceObjectCreation or addressObjectCreation. Defaults to false."),
        new RequestKeyDefinition("options", "Optional output options. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kOptionsKeys =
    [
        new RequestKeyDefinition("filter", "Optional response filter. Omitted or null applies no restriction.")
    ];

    private static readonly List<RequestKeyDefinition> kFilterKeys =
    [
        new RequestKeyDefinition("id", "Optional exact management id filter. Null applies no restriction."),
        new RequestKeyDefinition("name", "Optional exact, case-insensitive management name filter. Null applies no restriction.")
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
