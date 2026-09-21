namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Holds the single authoritative description of every key of the audit proof critical changes request.
/// Validation help text is built from these definitions, so an API documentation change and a
/// validation message cannot contradict one another.
/// </summary>
public static class GetAuditProofCriticalChangesValidationSchema
{
    /// <summary>
    /// Name of the endpoint used in validation messages.
    /// </summary>
    public const string kEndpointName = "getAuditProofCriticalChanges";

    /// <summary>
    /// JSON path of the root request object.
    /// </summary>
    public const string kRootPath = "";

    /// <summary>
    /// JSON path of the ticket id key.
    /// </summary>
    public const string kTicketIdPath = "ticketId";

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
        new RequestKeyDefinition("ticketId", "Database id of the workflow ticket. Required and greater than 0."),
        new RequestKeyDefinition("options", "Optional output options. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kOptionsKeys =
    [
        new RequestKeyDefinition("filter", "Optional response filter. Omitted or null applies no restriction.")
    ];

    private static readonly List<RequestKeyDefinition> kFilterKeys =
    [
        new RequestKeyDefinition("changeTime", "Optional exact change timestamp filter, matched on the wall clock of the installation; an offset or trailing Z is converted to it first. Null applies no restriction."),
        new RequestKeyDefinition("changeUserName", "Optional exact, case-insensitive change user name filter. Null applies no restriction."),
        new RequestKeyDefinition("changeContent", "Optional exact, case-insensitive change content filter. Null applies no restriction.")
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
    /// Builds the message reported for a syntactically valid ticket id that names no workflow ticket.
    /// </summary>
    /// <param name="ticketId">Ticket id the caller supplied.</param>
    /// <returns>Message naming the id and stating that no such ticket exists.</returns>
    public static string DescribeUnknownTicket(long ticketId)
    {
        return $"Workflow ticket with '{kTicketIdPath}' {ticketId} does not exist.";
    }

    /// <summary>
    /// Builds the help text listing the valid keys of one request object.
    /// </summary>
    /// <param name="allowedKeys">Keys the object accepts.</param>
    /// <returns>Help text naming every allowed key and its description.</returns>
    public static string DescribeKeys(IReadOnlyList<RequestKeyDefinition> allowedKeys)
    {
        return string.Join(" ", allowedKeys.Select(key => $"'{key.JsonName}': {key.Description}"));
    }
}
