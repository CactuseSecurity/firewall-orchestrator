using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the AddressGroupResponse type.
/// </summary>
public sealed class AddressGroupResponse
{
    /// <summary>
    /// Gets the Id value.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets the Name value.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the State value.
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Gets the ShowInRequest value.
    /// </summary>
    [JsonPropertyName("showInRequest")]
    public bool ShowInRequest { get; set; }

    /// <summary>
    /// Gets the Members value.
    /// </summary>
    [JsonPropertyName("members")]
    public List<AddressGroupMemberResponse> Members { get; set; } = [];

    /// <summary>
    /// Represents the AddressGroupMemberResponse type.
    /// </summary>
    public sealed class AddressGroupMemberResponse : IOptionalTechnicalDetailsResponse
    {
        /// <summary>
        /// Gets the Id value.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Gets the Name value.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the address type calculated from the IP range.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets the inclusive IP range or an empty list for non-IP objects.
        /// </summary>
        [JsonPropertyName("ipRange")]
        public List<string> IpRange { get; set; } = [];

        /// <summary>
        /// Gets the member state.
        /// </summary>
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        /// <summary>
        /// Gets whether the member is visible in requests.
        /// </summary>
        [JsonPropertyName("showInRequest")]
        public bool ShowInRequest { get; set; }

        /// <inheritdoc />
        [JsonIgnore]
        public bool IncludeTechnicalDetails { get; set; }
    }
}
