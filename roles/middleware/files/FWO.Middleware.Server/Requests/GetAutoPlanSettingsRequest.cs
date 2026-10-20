using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetAutoPlanSettingsRequest type.
/// </summary>
/// <remarks>
/// The authoritative description of every key is kept in <see cref="GetAutoPlanSettingsValidationSchema"/> so
/// API documentation and validation help text cannot diverge. The XML documentation below repeats it for the
/// generated OpenAPI document.
/// </remarks>
public sealed class GetAutoPlanSettingsRequest : IRequestWithRootAdditionalData
{
    /// <summary>
    /// Maximum number of ids in <see cref="DeviceIds"/> and in <see cref="ManagementIds"/> each.
    /// </summary>
    public const int kMaxIds = 1000;

    private GetAutoPlanSettingsOptions options = new();
    private List<int> deviceIds = [];
    private List<int> managementIds = [];

    /// <summary>
    /// Gets or sets the database ids of the gateways whose effective settings are requested. Each greater than 0, at
    /// most <see cref="kMaxIds"/>. Defaults to an empty list; an explicit <c>null</c> is treated like the default.
    /// </summary>
    [JsonPropertyName("deviceIds")]
    public List<int> DeviceIds
    {
        get => deviceIds;
        set => deviceIds = value ?? [];
    }

    /// <summary>
    /// Gets or sets the database ids of the managements whose effective settings are requested. Each greater than 0,
    /// at most <see cref="kMaxIds"/>. Defaults to an empty list; an explicit <c>null</c> is treated like the default.
    /// </summary>
    [JsonPropertyName("managementIds")]
    public List<int> ManagementIds
    {
        get => managementIds;
        set => managementIds = value ?? [];
    }

    /// <summary>
    /// Gets or sets the optional output options. Defaults to an empty object, which applies no restriction. An
    /// explicit <c>null</c> is treated like the default.
    /// </summary>
    [JsonPropertyName("options")]
    public GetAutoPlanSettingsOptions Options
    {
        get => options;
        set => options = value ?? new GetAutoPlanSettingsOptions();
    }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is reported back to
    /// the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Represents the optional output options of the auto plan settings lookup.
/// </summary>
public sealed class GetAutoPlanSettingsOptions : IRequestWithAdditionalData
{
    /// <summary>
    /// Maximum and default number of settings returned per request.
    /// </summary>
    public const int kMaxLimit = 1000;

    /// <summary>
    /// Gets or sets the optional response filter. When omitted or <c>null</c> no response field restricts the
    /// result.
    /// </summary>
    [JsonPropertyName("filter")]
    public AutoPlanSettingsFilter? Filter { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of settings to return, between 1 and <see cref="kMaxLimit"/>. When omitted
    /// or <c>null</c>, a page of <see cref="kMaxLimit"/> settings is returned. Together with <see cref="Offset"/>
    /// it pages the ordered result; the <c>X-Has-More</c> response header tells whether further settings follow.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; } = kMaxLimit;

    /// <summary>
    /// Gets or sets the optional number of settings to skip before the first returned one. Must not be negative.
    /// When omitted or <c>null</c>, no setting is skipped.
    /// </summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is reported back to
    /// the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Represents the response filter of the auto plan settings lookup. Every key matches a field of
/// <see cref="FWO.Middleware.Server.Responses.AutoPlanSettingResponse"/> and is nullable; a key that is omitted or
/// <c>null</c> does not restrict the result.
/// </summary>
public sealed class AutoPlanSettingsFilter : IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets the optional exact scope type filter: global, management or gateway.
    /// </summary>
    [JsonPropertyName("scopeType")]
    public string? ScopeType { get; set; }

    /// <summary>
    /// Gets or sets the optional exact id filter of a management or gateway.
    /// </summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>
    /// Gets or sets the optional filter on the effective autoPlanImplementation value.
    /// </summary>
    [JsonPropertyName("autoPlanImplementation")]
    public bool? AutoPlanImplementation { get; set; }

    /// <summary>
    /// Gets or sets the optional filter on the effective autoPlanImplementationForRules value.
    /// </summary>
    [JsonPropertyName("autoPlanImplementationForRules")]
    public bool? AutoPlanImplementationForRules { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is reported back to
    /// the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
