using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the address group lookup result split into standard groups and zone groups.
/// </summary>
public sealed class SeparatedAddressGroupsResponse
{
    /// <summary>
    /// Gets the group objects that are not zones.
    /// </summary>
    [JsonPropertyName("standardGroups")]
    public List<AddressGroupResponse> StandardGroups { get; set; } = [];

    /// <summary>
    /// Gets the group objects that are zones.
    /// </summary>
    [JsonPropertyName("zoneGroups")]
    public List<AddressGroupResponse> ZoneGroups { get; set; } = [];
}
