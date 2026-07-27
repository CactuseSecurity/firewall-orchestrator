using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetNetObjectValidityRequest type.
/// </summary>
public sealed class GetNetObjectValidityRequest
{
    /// <summary>
    /// Gets the IpAddress value.
    /// </summary>
    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets the NetMask value.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("netMask")]
    public int NetMask { get; set; }

    /// <summary>
    /// Gets the minimum prefix length. Defaults to 1 to preserve the F1 contract when the value is omitted.
    /// </summary>
    [JsonPropertyName("minPrefixLength")]
    public int MinPrefixLength { get; set; } = 1;
}
