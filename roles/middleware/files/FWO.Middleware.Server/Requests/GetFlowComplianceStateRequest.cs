using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Request of <c>getFlowComplianceState</c>: one flow, formed by all sources, destinations and services together,
/// checked against each requested compliance policy.
/// </summary>
public sealed class GetFlowComplianceStateRequest : IRequestWithRootAdditionalData
{
    /// <summary>
    /// Gets or sets the source addresses of the flow, 0 to 100 entries, each with exactly one of <c>ipHost</c>,
    /// <c>ipNetwork</c> or <c>ipRange</c>. Omitted means an empty list; <c>null</c> is rejected.
    /// </summary>
    [JsonPropertyName("source")]
    public List<AddressInputRequest?>? Source { get; set; } = [];

    /// <summary>
    /// Gets or sets the destination addresses of the flow, 0 to 100 entries, each with exactly one of <c>ipHost</c>,
    /// <c>ipNetwork</c> or <c>ipRange</c>. Omitted means an empty list; <c>null</c> is rejected.
    /// </summary>
    [JsonPropertyName("destination")]
    public List<AddressInputRequest?>? Destination { get; set; } = [];

    /// <summary>
    /// Gets or sets the services of the flow, 0 to 100 entries, each with <c>protocol</c>, <c>portStart</c> and
    /// <c>portEnd</c>. Omitted means an empty list; <c>null</c> is rejected.
    /// </summary>
    [JsonPropertyName("service")]
    public List<ServiceRangeRequest?>? Service { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the compliance policies to check the flow against, 0 to 100 entries, each greater
    /// than 0. Duplicates are checked once; an empty list returns no result. Omitted means an empty list;
    /// <c>null</c> is rejected.
    /// </summary>
    [JsonPropertyName("policies")]
    public List<int>? Policies { get; set; } = [];

    /// <summary>
    /// Gets or sets the keys the request does not support; each one is reported as a validation error.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    /// <summary>
    /// One service of the flow: a protocol and an inclusive destination port range.
    /// </summary>
    public sealed class ServiceRangeRequest : IRequestWithAdditionalData
    {
        /// <summary>
        /// Gets or sets the first port of the inclusive port range, 0 to 65535, compared with the port range of forbidden
        /// service definitions of the same protocol: the destination port for tcp and udp, the ICMP type for icmp; for
        /// other protocols give the range of the definitions to check. Omitted means 0.
        /// </summary>
        [JsonPropertyName("portStart")]
        public int PortStart { get; set; }

        /// <summary>
        /// Gets or sets the last port of the inclusive port range, 0 to 65535, not lower than <c>portStart</c>,
        /// compared with the port range of forbidden service definitions of the same protocol: the destination port
        /// for tcp and udp, the ICMP type for icmp; for other protocols give the range of the definitions to check.
        /// Omitted means 0.
        /// </summary>
        [JsonPropertyName("portEnd")]
        public int PortEnd { get; set; }

        /// <summary>
        /// Gets or sets the protocol name, for example <c>tcp</c> or <c>icmp</c>, or the numeric protocol id, for
        /// example <c>6</c>. Must not be empty.
        /// </summary>
        [JsonPropertyName("protocol")]
        public string Protocol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the keys the service entry does not support; each one is reported as a validation error.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }
}
