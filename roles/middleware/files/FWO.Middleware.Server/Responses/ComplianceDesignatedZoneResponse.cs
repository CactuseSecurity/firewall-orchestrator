using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents a designated compliance zone returned by the middleware.
/// </summary>
public sealed class ComplianceDesignatedZoneResponse
{
    /// <summary>
    /// Gets the Id value.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets the Name value.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the Description value.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets the IpRanges value.
    /// </summary>
    [JsonPropertyName("ipRanges")]
    public List<ComplianceDesignatedZoneIpRangeResponse> IpRanges { get; set; } = [];
}

/// <summary>
/// Represents one IP range in a designated compliance zone.
/// </summary>
public sealed class ComplianceDesignatedZoneIpRangeResponse
{
    /// <summary>
    /// Gets the inclusive IP range as its first and last address.
    /// </summary>
    [JsonPropertyName("ipRange")]
    public List<string> IpRange { get; set; } = [];
}
