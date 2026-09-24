namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Holds the single authoritative description of every key of the ticket lookup request. Validation
/// help text is built from these definitions, so an API documentation change and a validation message
/// cannot contradict one another.
/// </summary>
public static class GetTicketValidationSchema
{
    /// <summary>
    /// Name of the endpoint used in validation messages.
    /// </summary>
    public const string kEndpointName = "getTicket";

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

    private const string kNoRestriction = "Null applies no restriction.";

    private static readonly List<RequestKeyDefinition> kRootKeys =
    [
        new RequestKeyDefinition("ticketId", "Database id of the workflow ticket. Required and greater than 0."),
        new RequestKeyDefinition("options", "Optional output options. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kOptionsKeys =
    [
        new RequestKeyDefinition("filter", "Optional filter on the request tasks of the ticket. Omitted or null returns every task.")
    ];

    private static readonly List<RequestKeyDefinition> kFilterKeys =
    [
        new RequestKeyDefinition("id", $"Optional exact task id filter. {kNoRestriction}"),
        new RequestKeyDefinition("taskNumber", $"Optional exact task number filter. {kNoRestriction}"),
        new RequestKeyDefinition("title", $"Optional exact, case-insensitive task title filter. {kNoRestriction}"),
        new RequestKeyDefinition("taskType", $"Optional exact, case-insensitive task type filter, e.g. access or group_create. {kNoRestriction}"),
        new RequestKeyDefinition("stateId", $"Optional exact workflow state id filter. {kNoRestriction}"),
        new RequestKeyDefinition("state", $"Optional exact, case-insensitive workflow state name filter. {kNoRestriction}"),
        new RequestKeyDefinition("requestAction", $"Optional exact, case-insensitive request action filter, e.g. create or delete. {kNoRestriction}"),
        new RequestKeyDefinition("ruleActionId", $"Optional exact rule action id filter. {kNoRestriction}"),
        new RequestKeyDefinition("trackingId", $"Optional exact rule tracking id filter. {kNoRestriction}"),
        new RequestKeyDefinition("reason", $"Optional exact, case-insensitive task reason filter. {kNoRestriction}"),
        new RequestKeyDefinition("additionalInfo", $"Optional exact, case-insensitive filter on the stored additional info JSON text. {kNoRestriction}"),
        new RequestKeyDefinition("freeText", $"Optional exact, case-insensitive free text filter. {kNoRestriction}"),
        new RequestKeyDefinition("start", $"Optional exact rule validity start filter on the wall clock of the installation. {kNoRestriction}"),
        new RequestKeyDefinition("stop", $"Optional exact rule validity end filter on the wall clock of the installation. {kNoRestriction}"),
        new RequestKeyDefinition("targetBeginDate", $"Optional exact target begin date filter on the wall clock of the installation. {kNoRestriction}"),
        new RequestKeyDefinition("targetEndDate", $"Optional exact target end date filter on the wall clock of the installation. {kNoRestriction}"),
        new RequestKeyDefinition("lastRecertDate", $"Optional exact last recertification date filter on the wall clock of the installation. {kNoRestriction}"),
        new RequestKeyDefinition("managementId", $"Optional exact management id filter. {kNoRestriction}"),
        new RequestKeyDefinition("managementName", $"Optional exact, case-insensitive management name filter. {kNoRestriction}"),
        new RequestKeyDefinition("assignedGroup", $"Optional exact, case-insensitive assigned group (LDAP DN) filter. {kNoRestriction}"),
        new RequestKeyDefinition("currentHandlerName", $"Optional exact, case-insensitive current handler user name filter. {kNoRestriction}"),
        new RequestKeyDefinition("flowAccessId", $"Optional exact flow access id filter. {kNoRestriction}"),
        new RequestKeyDefinition("locked", $"Optional locked flag filter. {kNoRestriction}")
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
}
