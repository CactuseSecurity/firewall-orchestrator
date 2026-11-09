using System.Text.Json;
using FWO.Middleware.Server.OpenApi;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the request body of the path device resolution. It asks for the firewall devices that lie on
/// the paths between every source and every destination address block. Each device is reported once,
/// in no particular order.
/// </summary>
/// <remarks>
/// The key descriptions used in validation errors are kept in <see cref="ResolvePathDevicesRequestValidationSchema"/>.
/// The XML documentation below feeds the generated OpenAPI document and must be kept in line with it by hand.
/// </remarks>
public sealed class ResolvePathDevicesRequest : IRequestWithRootAdditionalData
{
    private ResolvePathDevicesOptions options = new();

    /// <summary>
    /// Gets or sets the address blocks the paths start at. Required, 1 to 500 entries. Each entry gives
    /// exactly one of <c>ipHost</c>, <c>ipNetwork</c> or <c>ipRange</c>; null entries are rejected.
    /// Every source is combined with every destination.
    /// </summary>
    /// <remarks>
    /// Nullable rather than marked with <see cref="JsonRequiredAttribute"/> or <c>required</c>: both make
    /// the deserializer throw on a missing key before validation runs, so the caller would get that one
    /// error instead of every error of the request together. Null tells an omitted key apart from an
    /// empty list. <see cref="OpenApiRequiredAttribute"/> restores the required marker in the generated schema.
    /// The entry limit is <see cref="ResolvePathDevicesRequestValidationSchema.kMaximumIpBlocksPerList"/>.
    /// </remarks>
    [OpenApiRequired]
    [JsonPropertyName("sources")]
    public List<AddressInputRequest?>? Sources { get; set; }

    /// <summary>
    /// Gets or sets the address blocks the paths end at. Required, 1 to 500 entries. Each entry gives
    /// exactly one of <c>ipHost</c>, <c>ipNetwork</c> or <c>ipRange</c>; null entries are rejected.
    /// Every destination is combined with every source.
    /// </summary>
    /// <remarks>Nullable and limited for the same reasons as <see cref="Sources"/>.</remarks>
    [OpenApiRequired]
    [JsonPropertyName("destinations")]
    public List<AddressInputRequest?>? Destinations { get; set; }

    /// <summary>
    /// Gets or sets the optional analysis and output options. Defaults to an empty object, which uses the
    /// configured algorithm and zone matrix and returns every device found. An explicit <c>null</c> is
    /// treated like the default.
    /// </summary>
    [JsonPropertyName("options")]
    public ResolvePathDevicesOptions Options
    {
        get => options;
        set => options = value ?? new ResolvePathDevicesOptions();
    }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is
    /// reported back to the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Represents the optional analysis and output options of the path device resolution.
/// </summary>
public sealed class ResolvePathDevicesOptions : IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets the name of the path analysis algorithm to run, for example <c>networkZoneTree</c>.
    /// When omitted or <c>null</c> the algorithm configured in the settings is used. A configured
    /// algorithm of <c>none</c> is rejected, because it cannot resolve any device.
    /// </summary>
    [JsonPropertyName("algorithm")]
    public string? Algorithm { get; set; }

    /// <summary>
    /// Gets or sets the id of the zone matrix the network zone tree algorithm works on. Greater than 0
    /// and only valid when that algorithm runs, whether requested or configured. When omitted or
    /// <c>null</c> the configured designated zone matrix is used.
    /// </summary>
    [JsonPropertyName("matrixId")]
    public int? MatrixId { get; set; }

    /// <summary>
    /// Gets or sets the optional response filter. When omitted or <c>null</c> no response field
    /// restricts the result.
    /// </summary>
    [JsonPropertyName("filter")]
    public ResolvePathDevicesFilter? Filter { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is
    /// reported back to the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Represents the response filter of the path device resolution. Every key matches a field of a returned
/// device and is nullable; a key that is omitted or <c>null</c> does not restrict the result. Given keys
/// are combined with AND. The filter is applied after the analysis, so it narrows the result but never
/// changes which paths are analysed.
/// </summary>
public sealed class ResolvePathDevicesFilter : IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets the optional exact device id filter.
    /// </summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>
    /// Gets or sets the optional exact device name filter. Compared case-insensitively.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the additional request data. Any key captured here is unsupported and is
    /// reported back to the caller.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
