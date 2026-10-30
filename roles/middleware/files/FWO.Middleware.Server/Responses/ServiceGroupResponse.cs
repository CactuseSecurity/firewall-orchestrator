using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the ServiceGroupResponse type.
/// </summary>
public sealed class ServiceGroupResponse
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
    public List<ServiceGroupMemberResponse> Members { get; set; } = [];

    /// <summary>
    /// Represents the ServiceGroupMemberResponse type.
    /// </summary>
    public sealed class ServiceGroupMemberResponse : IOptionalTechnicalDetailsResponse
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
        /// Gets the inclusive starting port, or null for a protocol-only service.
        /// </summary>
        [JsonPropertyName("portStart")]
        public int? PortStart { get; set; }

        /// <summary>
        /// Gets the inclusive ending port, or null for a protocol-only service.
        /// </summary>
        [JsonPropertyName("portEnd")]
        public int? PortEnd { get; set; }

        /// <summary>
        /// Gets the service protocol name or identifier.
        /// </summary>
        [JsonPropertyName("protocol")]
        public string Protocol { get; set; } = string.Empty;

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
