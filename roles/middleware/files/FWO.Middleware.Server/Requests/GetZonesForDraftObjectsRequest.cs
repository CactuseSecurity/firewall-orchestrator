using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the request body for resolving zones for draft flow network objects.
/// </summary>
public sealed class GetZonesForDraftObjectsRequest : IRequestWithRootAdditionalData
{
    /// <summary>
    /// Gets the root draft objects to evaluate.
    /// </summary>
    [JsonPropertyName("objects")]
    public List<DraftObjectRequest> Objects { get; set; } = [];

    /// <summary>
    /// Gets the additional data payload.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    /// <summary>
    /// Represents one draft object in the zone preview tree.
    /// </summary>
    public sealed class DraftObjectRequest : IRequestWithAdditionalData
    {
        /// <summary>
        /// Gets the display name of the draft object.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the object type.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets the start IP or range value.
        /// </summary>
        [JsonPropertyName("ipStart")]
        public string IpStart { get; set; } = string.Empty;

        /// <summary>
        /// Gets the end IP or range value.
        /// </summary>
        [JsonPropertyName("ipEnd")]
        public string IpEnd { get; set; } = string.Empty;

        /// <summary>
        /// Gets the child members for draft groups.
        /// </summary>
        [JsonPropertyName("members")]
        public List<DraftObjectRequest> Members { get; set; } = [];

        /// <summary>
        /// Gets the additional data payload.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }
}
