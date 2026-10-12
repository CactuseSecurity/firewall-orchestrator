using FWO.Middleware.Server.OpenApi;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetTicketRequest type.
/// </summary>
/// <remarks>
/// The authoritative description of every key is kept in <see cref="GetTicketValidationSchema"/> so API
/// documentation and validation help text cannot diverge. The XML documentation below repeats it for
/// the generated OpenAPI document.
/// </remarks>
public sealed class GetTicketRequest : IRequestWithRootAdditionalData
{
    private GetTicketOptions options = new();

    /// <summary>
    /// Gets or sets the database id of the workflow ticket to return. Required and greater than 0.
    /// </summary>
    /// <remarks>
    /// Nullable rather than marked as required for the deserializer, so that an omitted key is
    /// reported by the aggregating validator together with every other error of the request.
    /// <see cref="OpenApiRequiredAttribute"/> restores the required marker in the generated schema.
    /// </remarks>
    [OpenApiRequired]
    [JsonPropertyName("ticketId")]
    public long? TicketId { get; set; }

    /// <summary>
    /// Gets or sets the optional output options. Defaults to an empty object, which returns the ticket
    /// with all of its tasks. An explicit <c>null</c> is treated like the default.
    /// </summary>
    [JsonPropertyName("options")]
    public GetTicketOptions Options
    {
        get => options;
        set => options = value ?? new GetTicketOptions();
    }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is
    /// reported back to the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Represents the optional output options of the ticket lookup.
/// </summary>
public sealed class GetTicketOptions : IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets the optional filter on the request tasks of the ticket. When omitted or
    /// <c>null</c> every task of the ticket is returned.
    /// </summary>
    [JsonPropertyName("filter")]
    public TicketTaskFilter? Filter { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is
    /// reported back to the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Represents the filter on the request tasks of the ticket lookup. Every key matches a scalar field
/// of <see cref="FWO.Middleware.Server.Responses.TicketTaskResponse"/> and is nullable; a key that is
/// omitted or <c>null</c> does not restrict the result. Supplied keys are combined with AND. Text keys
/// match exactly and case-insensitively; timestamp keys match exactly on the wall clock of the
/// installation, an offset or trailing Z being converted to it first.
/// </summary>
public sealed class TicketTaskFilter : IRequestWithAdditionalData
{
    /// <summary>Gets or sets the optional task id filter.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Gets or sets the optional task number filter.</summary>
    [JsonPropertyName("taskNumber")]
    public int? TaskNumber { get; set; }

    /// <summary>Gets or sets the optional task title filter.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Gets or sets the optional task type filter, e.g. access or group_create.</summary>
    [JsonPropertyName("taskType")]
    public string? TaskType { get; set; }

    /// <summary>Gets or sets the optional workflow state id filter.</summary>
    [JsonPropertyName("stateId")]
    public int? StateId { get; set; }

    /// <summary>Gets or sets the optional workflow state name filter.</summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>Gets or sets the optional request action filter, e.g. create or delete.</summary>
    [JsonPropertyName("requestAction")]
    public string? RequestAction { get; set; }

    /// <summary>Gets or sets the optional rule action id filter.</summary>
    [JsonPropertyName("ruleActionId")]
    public int? RuleActionId { get; set; }

    /// <summary>Gets or sets the optional rule tracking id filter.</summary>
    [JsonPropertyName("trackingId")]
    public int? TrackingId { get; set; }

    /// <summary>Gets or sets the optional task reason filter.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>Gets or sets the optional additional info filter, compared against the stored JSON text.</summary>
    [JsonPropertyName("additionalInfo")]
    public string? AdditionalInfo { get; set; }

    /// <summary>Gets or sets the optional free text filter.</summary>
    [JsonPropertyName("freeText")]
    public string? FreeText { get; set; }

    /// <summary>Gets or sets the optional rule validity start filter.</summary>
    [JsonPropertyName("start")]
    public DateTime? Start { get; set; }

    /// <summary>Gets or sets the optional rule validity end filter.</summary>
    [JsonPropertyName("stop")]
    public DateTime? Stop { get; set; }

    /// <summary>Gets or sets the optional target begin date filter.</summary>
    [JsonPropertyName("targetBeginDate")]
    public DateTime? TargetBeginDate { get; set; }

    /// <summary>Gets or sets the optional target end date filter.</summary>
    [JsonPropertyName("targetEndDate")]
    public DateTime? TargetEndDate { get; set; }

    /// <summary>Gets or sets the optional last recertification date filter.</summary>
    [JsonPropertyName("lastRecertDate")]
    public DateTime? LastRecertDate { get; set; }

    /// <summary>Gets or sets the optional management id filter.</summary>
    [JsonPropertyName("managementId")]
    public int? ManagementId { get; set; }

    /// <summary>Gets or sets the optional management name filter.</summary>
    [JsonPropertyName("managementName")]
    public string? ManagementName { get; set; }

    /// <summary>Gets or sets the optional assigned group filter (LDAP DN).</summary>
    [JsonPropertyName("assignedGroup")]
    public string? AssignedGroup { get; set; }

    /// <summary>Gets or sets the optional current handler user name filter.</summary>
    [JsonPropertyName("currentHandlerName")]
    public string? CurrentHandlerName { get; set; }

    /// <summary>Gets or sets the optional flow access id filter.</summary>
    [JsonPropertyName("flowAccessId")]
    public long? FlowAccessId { get; set; }

    /// <summary>Gets or sets the optional locked flag filter.</summary>
    [JsonPropertyName("locked")]
    public bool? Locked { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is
    /// reported back to the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
