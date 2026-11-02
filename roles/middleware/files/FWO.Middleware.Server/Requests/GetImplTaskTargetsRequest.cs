using System.Text.Json;
using FWO.Middleware.Server.OpenApi;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetImplTaskTargetsRequest type.
/// </summary>
/// <remarks>
/// The authoritative description of every key is kept in <see cref="GetImplTaskTargetsValidationSchema"/> so
/// API documentation and validation help text cannot diverge. The XML documentation below repeats it for the
/// generated OpenAPI document.
/// </remarks>
public sealed class GetImplTaskTargetsRequest : IRequestWithRootAdditionalData
{
    private GetImplTaskTargetsOptions options = new();

    /// <summary>
    /// Gets or sets the database id of the management the request task belongs to. Required and greater than 0.
    /// </summary>
    /// <remarks>
    /// Nullable rather than required for the deserializer, so an omitted key is reported together with every
    /// other error of the request instead of on its own. <see cref="OpenApiRequiredAttribute"/> keeps the key
    /// mandatory in the generated schema.
    /// </remarks>
    [OpenApiRequired]
    [JsonPropertyName("managementId")]
    public int? ManagementId { get; set; }

    /// <summary>
    /// Gets or sets whether the object task orders services (true) or network objects (false). Decides whether
    /// the device specific setting serviceObjectCreation or addressObjectCreation applies. Defaults to false.
    /// </summary>
    [JsonPropertyName("isServiceObject")]
    public bool IsServiceObject { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the object task orders time objects. Selects the device specific setting
    /// timeObjectCreation instead of addressObjectCreation. Must not be true together with isServiceObject.
    /// Defaults to false.
    /// </summary>
    [JsonPropertyName("isTimeObject")]
    public bool IsTimeObject { get; set; } = false;

    /// <summary>
    /// Gets or sets the optional output options. Defaults to an empty object, which applies no restriction. An
    /// explicit <c>null</c> is treated like the default.
    /// </summary>
    [JsonPropertyName("options")]
    public GetImplTaskTargetsOptions Options
    {
        get => options;
        set => options = value ?? new GetImplTaskTargetsOptions();
    }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is reported back to
    /// the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Represents the optional output options of the implementation task target lookup.
/// </summary>
public sealed class GetImplTaskTargetsOptions : IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets the optional response filter. When omitted or <c>null</c> no response field restricts the
    /// result.
    /// </summary>
    [JsonPropertyName("filter")]
    public ImplTaskTargetFilter? Filter { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of target managements to return, between 1 and the configured
    /// <c>maxRestResponseObjectLimit</c> (default 10000). When omitted or <c>null</c>, a page of that size is
    /// returned. Together with <see cref="Offset"/> it pages the ordered result; the <c>X-Has-More</c> response
    /// header tells whether further targets follow.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the optional number of target managements to skip before the first returned one. Must not be
    /// negative. When omitted or <c>null</c>, no target is skipped.
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
/// Represents the response filter of the implementation task target lookup. Every key matches a field of
/// <see cref="FWO.Middleware.Server.Responses.ImplTaskTargetResponse"/> and is nullable; a key that is omitted or
/// <c>null</c> does not restrict the result.
/// </summary>
public sealed class ImplTaskTargetFilter : IRequestWithAdditionalData
{
    /// <summary>
    /// Maximum length of the name filter.
    /// </summary>
    public const int kMaxNameLength = 256;

    /// <summary>
    /// Gets or sets the optional exact management id filter.
    /// </summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>
    /// Gets or sets the optional exact management name filter. Compared case-insensitively. At most
    /// <see cref="kMaxNameLength"/> characters.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is reported back to
    /// the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
