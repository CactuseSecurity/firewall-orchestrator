using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the GetAddressGroupsRequest type.
/// </summary>
public sealed class GetAddressGroupsRequest : IFlowCatalogGroupFilterRequest, IPagedListRequest
{
    /// <summary>
    /// Gets the Filter value.
    /// </summary>
    [JsonPropertyName("filter")]
    public FlowCatalogGroupFilter? Filter { get; set; }

    /// <summary>
    /// Gets the Option value.
    /// </summary>
    [JsonPropertyName("option")]
    public AddressGroupsOption? Option { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items returned, between 1 and the configured <c>maxRestResponseObjectLimit</c>
    /// (default 10000). When omitted or null, a page of that size is returned. Items are ordered by name and id, so <c>limit</c>
    /// and <c>offset</c> page the result deterministically; the <c>X-Has-More</c> response header tells whether further items follow.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the number of items skipped before the first returned item, at least 0. When omitted or null, no
    /// item is skipped.
    /// </summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; set; }

    /// <summary>
    /// Gets the AdditionalData value.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
