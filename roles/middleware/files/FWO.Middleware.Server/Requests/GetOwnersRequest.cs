using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents owner lookup filters for the owners/get endpoint.
/// </summary>
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
}
