using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the optional output options of the address group lookup.
/// </summary>
public sealed class AddressGroupsOption : IRequestWithAdditionalData
{
    /// <summary>
    /// Gets or sets a value indicating whether zone groups are returned in a separate list.
    /// </summary>
    [JsonPropertyName("separateZoneGroups")]
    public bool? SeparateZoneGroups { get; set; }

    /// <summary>
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
