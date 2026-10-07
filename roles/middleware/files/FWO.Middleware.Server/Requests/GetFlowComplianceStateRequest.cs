using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetFlowComplianceStateRequest type.
/// </summary>
public sealed class GetFlowComplianceStateRequest : IRequestWithRootAdditionalData
{
    /// <summary>
    /// Gets the Source value.
    /// </summary>
    [JsonPropertyName("source")]
    public List<IpRangeRequest> Source { get; set; } = [];

    /// <summary>
    /// Gets the Destination value.
    /// </summary>
    [JsonPropertyName("destination")]
    public List<IpRangeRequest> Destination { get; set; } = [];

    /// <summary>
    /// Gets the Service value.
    /// </summary>
    [JsonPropertyName("service")]
    public List<ServiceRangeRequest> Service { get; set; } = [];

    /// <summary>
    /// Gets the Policies value.
    /// </summary>
    [JsonPropertyName("policies")]
    public List<int> Policies { get; set; } = [];

    /// <summary>
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    /// <summary>
    /// Represents the IpRangeRequest type.
    /// </summary>
    public sealed class IpRangeRequest : IRequestWithAdditionalData, IAddressInput
    {
        /// <summary>
        /// Gets one maskless IPv4 or IPv6 address.
        /// </summary>
        [JsonPropertyName("ipHost")]
        public string IpHost { get; set; } = string.Empty;

        /// <summary>
        /// Gets one canonical IPv4 or IPv6 CIDR network.
        /// </summary>
        [JsonPropertyName("ipNetwork")]
        public string IpNetwork { get; set; } = string.Empty;

        /// <summary>
        /// Gets an inclusive range containing exactly two maskless addresses.
        /// </summary>
        [JsonPropertyName("ipRange")]
        public List<string>? IpRange { get; set; }

        internal string NormalizedIpStart { get; set; } = string.Empty;

        internal string NormalizedIpEnd { get; set; } = string.Empty;

        /// <summary>
        /// Gets the AdditionalData value.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    /// <summary>
    /// Represents the ServiceRangeRequest type.
    /// </summary>
    public sealed class ServiceRangeRequest : IRequestWithAdditionalData
    {
        /// <summary>
        /// Gets the PortStart value.
        /// </summary>
        [JsonPropertyName("portStart")]
        public int PortStart { get; set; }

        /// <summary>
        /// Gets the PortEnd value.
        /// </summary>
        [JsonPropertyName("portEnd")]
        public int PortEnd { get; set; }

        /// <summary>
        /// Gets the Protocol value.
        /// </summary>
        [JsonPropertyName("protocol")]
        public string Protocol { get; set; } = string.Empty;

        /// <summary>
        /// Gets the AdditionalData value.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }
}
