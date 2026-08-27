using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents a request for owners visible to the authenticated caller.
/// </summary>
public sealed class GetOwnersRequest : RequestDto<GetOwnersOptions>
{
}

/// <summary>
/// Represents optional controls for the owner lookup response.
/// </summary>
public sealed class GetOwnersOptions : RequestOptionsDto<GetOwnersFilter>
{
    /// <summary>
    /// Gets or sets a value indicating whether all owner detail fields are returned. When <c>null</c> or
    /// <c>false</c> (the default), only core owner fields are returned.
    /// </summary>
    [JsonPropertyName("showDetails")]
    public bool? ShowDetails { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether owners with inactive lifecycle states are excluded. When <c>null</c>
    /// or <c>true</c> (the default), inactive lifecycle states are excluded while owners without a lifecycle state
    /// remain included. Set to <c>false</c> to include inactive lifecycle states.
    /// </summary>
    [JsonPropertyName("showOnlyActiveState")]
    public bool? ShowOnlyActiveState { get; set; }
}

/// <summary>
/// Represents nullable owner lookup filters. Omitted or <c>null</c> fields do not restrict the result.
/// </summary>
public sealed class GetOwnersFilter : RequestFilterDto
{
    /// <summary>
    /// Gets or sets the optional owner database-id filter.
    /// </summary>
    [JsonPropertyName("ownerId")]
    public int? OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the optional owner lifecycle-state database-id filter.
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
