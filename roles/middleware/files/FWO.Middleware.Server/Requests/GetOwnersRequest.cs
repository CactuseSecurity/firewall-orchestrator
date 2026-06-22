using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents owner lookup filters for the owners/get endpoint.
/// Unknown properties are rejected so callers are notified of typos or unsupported filters
/// instead of having them silently ignored.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GetOwnersRequest
{
    /// <summary>
    /// Gets or sets the optional owner database id filter.
    /// </summary>
    [JsonPropertyName("ownerId")]
    public int? OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the optional owner lifecycle state id filter.
    /// </summary>
    [JsonPropertyName("ownerLifecycleStateId")]
    public int? OwnerLifeCycleStateId { get; set; }

    /// <summary>
    /// Gets or sets the optional owner active flag filter.
    /// </summary>
    [JsonPropertyName("active")]
    public bool? Active { get; set; }

    /// <summary>
    /// Gets or sets the optional owner name filter.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the optional external application id filter.
    /// </summary>
    [JsonPropertyName("appIdExternal")]
    public string? AppIdExternal { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether all owner fields should be returned.
    /// When <c>null</c> or <c>false</c> (default) only the core fields are returned.
    /// </summary>
    [JsonPropertyName("showDetails")]
    public bool? ShowDetails { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether owners with an inactive lifecycle state should be excluded.
    /// When <c>null</c> or <c>true</c> (default) owners whose lifecycle state is inactive are filtered out;
    /// owners without a lifecycle state are kept. Set to <c>false</c> to also include owners with an inactive state.
    /// </summary>
    [JsonPropertyName("showOnlyActiveState")]
    public bool? ShowOnlyActiveState { get; set; }
}
