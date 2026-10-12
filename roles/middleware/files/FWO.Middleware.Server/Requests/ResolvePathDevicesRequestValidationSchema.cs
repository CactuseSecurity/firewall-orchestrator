namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Holds the single authoritative description of every key of the resolve path device request.
/// Validation help text is built from these definitions, so an API documentation change and a
/// validation message cannot contradict one another.
/// </summary>
public static class ResolvePathDevicesRequestValidationSchema
{
    /// <summary>
    /// Name of the endpoint used in validation messages.
    /// </summary>
    public const string kEndpointName = "resolvePathDevices";

    /// <summary>
    /// JSON path of the root request object.
    /// </summary>
    public const string kRootPath = "";

    /// <summary>
    /// JSON path of the sources key.
    /// </summary>
    public const string kSourcesPath = "sources";

    /// <summary>
    /// JSON path of the destinations key.
    /// </summary>
    public const string kDestinationsPath = "destinations";

    /// <summary>
    /// JSON path of the options object.
    /// </summary>
    public const string kOptionsPath = "options";

    /// <summary>
    /// JSON path of the filter object.
    /// </summary>
    public const string kFilterPath = "options.filter";

    /// <summary>
    /// Maximum list length for sources and destinations.
    /// </summary>
    public const int kMaximumIpBlocksPerList = 100;

    private static readonly List<RequestKeyDefinition> kRootKeys =
    [
        new RequestKeyDefinition("sources", "List of Address blocks the paths start at. Required and not empty"),
        new RequestKeyDefinition("destinations", "List of Address blocks the paths ends at. Required and not empty"),
        new RequestKeyDefinition("options", "Optional output options. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kOptionsKeys =
    [
        new RequestKeyDefinition("algorithm", "Optional algorithm name. Defaults to the algorithm choosen in the settings."),
        new RequestKeyDefinition("matrixId", "Optional matrix database id, only used for the network zone tree algorithm. Defaults to the matrix choosen in the settings"),
        new RequestKeyDefinition("filter", "Optional response filter. Omitted or null applies no restriction.")
    ];

    private static readonly List<RequestKeyDefinition> kFilterKeys =
    [
        new RequestKeyDefinition("id", "Optional exact device ID filter. Null applies no restriction."),
        new RequestKeyDefinition("name", "Optional exact device name filter. Null applies no restriction."),
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
