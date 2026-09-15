using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetAuditProofCriticalChangesRequest type.
/// </summary>
/// <remarks>
/// The authoritative description of every key is kept in
/// <see cref="GetAuditProofCriticalChangesValidationSchema"/> so API documentation and validation help text
/// cannot diverge. The XML documentation below repeats it for the generated OpenAPI document.
/// </remarks>
public sealed class GetAuditProofCriticalChangesRequest : IRequestWithRootAdditionalData
{
    private GetAuditProofCriticalChangesOptions options = new();

    /// <summary>
    /// Gets or sets the database id of the workflow ticket whose audit proof critical changes are returned.
    /// Required and greater than 0.
    /// </summary>
    /// <remarks>
    /// The key is deliberately not marked with <see cref="JsonRequiredAttribute"/>: a deserializer
    /// that rejects the missing key throws before validation runs, which would report it on its own
    /// instead of together with every other error of the same request. An omitted key deserializes
    /// to 0, which the validator reports as the required-key error.
    /// </remarks>
    [JsonPropertyName("ticketId")]
    public long TicketId { get; set; }

    /// <summary>
    /// Gets or sets the optional output options. Defaults to an empty object, which applies no
    /// restriction beyond the ticket. An explicit <c>null</c> is treated like the default.
    /// </summary>
    [JsonPropertyName("options")]
    public GetAuditProofCriticalChangesOptions Options
    {
        get => options;
        set => options = value ?? new GetAuditProofCriticalChangesOptions();
    }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is
    /// reported back to the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Represents the optional output options of the audit proof critical changes lookup.
/// </summary>
public sealed class GetAuditProofCriticalChangesOptions : IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets the optional response filter. When omitted or <c>null</c> no response field
    /// restricts the result.
    /// </summary>
    [JsonPropertyName("filter")]
    public AuditProofCriticalChangeFilter? Filter { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is
    /// reported back to the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Represents the response filter of the audit proof critical changes lookup. Every key matches a field of
/// <see cref="FWO.Middleware.Server.Responses.AuditProofCriticalChangeResponse"/> and is nullable; a key
/// that is omitted or <c>null</c> does not restrict the result.
/// </summary>
public sealed class AuditProofCriticalChangeFilter : IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets the optional exact change timestamp filter.
    /// </summary>
    [JsonPropertyName("changeTime")]
    public DateTime? ChangeTime { get; set; }

    /// <summary>
    /// Gets or sets the optional exact change user name filter. Compared case-insensitively.
    /// </summary>
    [JsonPropertyName("changeUserName")]
    public string? ChangeUserName { get; set; }

    /// <summary>
    /// Gets or sets the optional exact change content filter. Compared case-insensitively.
    /// </summary>
    [JsonPropertyName("changeContent")]
    public string? ChangeContent { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is
    /// reported back to the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
