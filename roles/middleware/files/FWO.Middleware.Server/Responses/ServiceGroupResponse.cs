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
    public sealed class ServiceGroupMemberResponse
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
    }
}
