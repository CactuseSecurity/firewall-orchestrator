using System.Text.Json;
using FWO.Middleware.Server.OpenApi;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the request body of the path device resolution. It names the firewall devices that lie on
/// the paths between every source and every destination address block.
/// </summary>
/// <remarks>
/// The authoritative description of every key is kept in <c>ResolvePathDevicesValidationSchema</c> so
/// API documentation and validation help text cannot diverge. The XML documentation below repeats it for
/// the generated OpenAPI document.
/// </remarks>
public sealed class ResolvePathDevicesRequest : IRequestWithRootAdditionalData
{
    private ResolvePathDevicesOptions options = new();

    /// <summary>
    /// Gets or sets the address blocks the paths start at. Required, must contain at least one entry.
    /// Every source is combined with every destination.
    /// </summary>
    /// <remarks>
    /// Nullable rather than marked with <see cref="JsonRequiredAttribute"/> or <c>required</c>: both make
    /// the deserializer throw on a missing key before validation runs, so the caller would get that one
    /// error instead of every error of the request together. Null tells an omitted key apart from an
    /// empty list. <see cref="OpenApiRequiredAttribute"/> restores the required marker in the generated schema.
    /// </remarks>
    [OpenApiRequired]
    [JsonPropertyName("sources")]
    public List<IpRangeOrNetworkRequest?>? Sources { get; set; }

    /// <summary>
    /// Gets or sets the address blocks the paths end at. Required, must contain at least one entry.
    /// Every destination is combined with every source.
    /// </summary>
    /// <remarks>Nullable for the same reason as <see cref="Sources"/>.</remarks>
    [OpenApiRequired]
    [JsonPropertyName("destinations")]
    public List<IpRangeOrNetworkRequest?>? Destinations { get; set; }

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
