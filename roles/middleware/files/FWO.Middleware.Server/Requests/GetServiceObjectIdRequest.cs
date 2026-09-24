using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetServiceObjectIdRequest type.
/// </summary>
public sealed class GetServiceObjectIdRequest : IVisibleInRequestFilterRequest
{
    /// <summary>
    /// Gets the Filter value.
    /// </summary>
    [JsonPropertyName("filter")]
    public VisibleInRequestFilter? Filter { get; set; }

    /// <summary>
    /// Gets the required inclusive starting port for the service object lookup.
    /// Send this property and <see cref="PortEnd"/> as <see langword="null"/> for an unambiguous portless service;
    /// otherwise both must contain a port, including the valid port value zero. This endpoint should not be used to identify custom
    /// protocol-only services because their shared protocol and null ports are technically ambiguous.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("portStart")]
    public int? PortStart { get; set; }

    /// <summary>
    /// Gets the required inclusive ending port for the service object lookup.
    /// Send this property and <see cref="PortStart"/> as <see langword="null"/> for an unambiguous portless service;
    /// otherwise both must contain a port, including the valid port value zero. This endpoint should not be used to identify custom
    /// protocol-only services because their shared protocol and null ports are technically ambiguous.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("portEnd")]
    public int? PortEnd { get; set; }

    /// <summary>
    /// Gets the Protocol value.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    /// <summary>
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
