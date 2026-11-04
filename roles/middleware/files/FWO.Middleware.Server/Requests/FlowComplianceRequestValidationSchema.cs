using FWO.Basics;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Holds the description of every key of the flow compliance requests (<c>getFlowComplianceState</c> and
/// <c>getPolicyIds</c>) together with their request paths and size limits.
/// </summary>
/// <remarks>
/// These descriptions are the ones reported in validation errors. The XML documentation of the request types
/// feeds the generated OpenAPI document and must be kept in line with them by hand.
/// </remarks>
public static class FlowComplianceRequestValidationSchema
{
    /// <summary>
    /// Name of the flow compliance state endpoint, used in validation messages.
    /// </summary>
    public const string kFlowComplianceStateEndpointName = "getFlowComplianceState";

    /// <summary>
    /// Name of the policy id endpoint, used in validation messages.
    /// </summary>
    public const string kPolicyIdsEndpointName = "getPolicyIds";

    /// <summary>
    /// JSON path of the root request object.
    /// </summary>
    public const string kRootPath = "";

    /// <summary>
    /// JSON path of the source key.
    /// </summary>
    public const string kSourcePath = "source";

    /// <summary>
    /// JSON path of the destination key.
    /// </summary>
    public const string kDestinationPath = "destination";

    /// <summary>
    /// JSON path of the service key.
    /// </summary>
    public const string kServicePath = "service";

    /// <summary>
    /// JSON path of the policies key.
    /// </summary>
    public const string kPoliciesPath = "policies";

    /// <summary>Maximum number of entries in each of 'source', 'destination' and 'service'.</summary>
    public const int kMaxRangeEntries = 100;

    /// <summary>Maximum number of entries in 'policies'.</summary>
    public const int kMaxPolicies = 100;

    /// <summary>
    /// Lowest port a service entry accepts.
    /// </summary>
    public const int kMinPort = 0;

    /// <summary>
    /// Highest port a service entry accepts.
    /// </summary>
    public const int kMaxPort = GlobalConst.kMaxPortNumber;

    private static readonly List<RequestKeyDefinition> kRootKeys =
    [
        new RequestKeyDefinition(kSourcePath,
            $"Source addresses of the flow, 0 to {kMaxRangeEntries} entries, each with exactly one of 'ipHost', 'ipNetwork' or 'ipRange'. Omitted means an empty list."),
        new RequestKeyDefinition(kDestinationPath,
            $"Destination addresses of the flow, 0 to {kMaxRangeEntries} entries, each with exactly one of 'ipHost', 'ipNetwork' or 'ipRange'. Omitted means an empty list."),
        new RequestKeyDefinition(kServicePath,
            $"Services of the flow, 0 to {kMaxRangeEntries} entries, each with 'protocol', 'portStart' and 'portEnd'. Omitted means an empty list."),
        new RequestKeyDefinition(kPoliciesPath,
            $"Ids of the compliance policies to check the flow against, 0 to {kMaxPolicies} entries, each greater than 0. Duplicates are checked once; an empty list returns no result. Omitted means an empty list.")
    ];

    private static readonly List<RequestKeyDefinition> kServiceKeys =
    [
        new RequestKeyDefinition("protocol",
            "Protocol name, for example 'tcp', 'udp' or 'icmp', or numeric protocol id, for example '6'. Must not be empty."),
        new RequestKeyDefinition("portStart",
            $"First port of the inclusive port range, {kMinPort} to {kMaxPort}, compared with the port range of forbidden service definitions of the same protocol: destination port for tcp and udp, ICMP type for icmp; for other protocols give the range of the definitions to check. Omitted means {kMinPort}."),
        new RequestKeyDefinition("portEnd",
            $"Last port of the inclusive port range, {kMinPort} to {kMaxPort}, not lower than 'portStart', compared with the port range of forbidden service definitions of the same protocol: destination port for tcp and udp, ICMP type for icmp; for other protocols give the range of the definitions to check. Omitted means {kMinPort}.")
    ];

    /// <summary>
    /// Gets the allowed root keys of <c>getFlowComplianceState</c>. All sources, destinations and services
    /// together form one flow that is checked against each requested policy.
    /// </summary>
    public static IReadOnlyList<RequestKeyDefinition> RootKeys => kRootKeys;

    /// <summary>
    /// Gets the allowed keys of one <c>service</c> entry.
    /// </summary>
    public static IReadOnlyList<RequestKeyDefinition> ServiceKeys => kServiceKeys;

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
