using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents filters and response-shaping flags accepted by Flow Catalog group endpoints.
/// </summary>
public sealed class FlowCatalogGroupFilter
{
    /// <summary>
    /// Gets or sets whether only objects visible in the current request are returned.
    /// </summary>
    [JsonPropertyName("visibleInRequest")]
    public bool? VisibleInRequest { get; set; }

    /// <summary>
    /// Gets or sets whether group members include their technical object details.
    /// </summary>
    [JsonPropertyName("includeTechnicalDetails")]
    public bool? IncludeTechnicalDetails { get; set; }

    /// <summary>
    /// Gets additional JSON properties for strict request validation.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
