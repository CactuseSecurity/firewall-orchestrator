using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Request-side caps of the modelling object endpoints.
/// </summary>
public static class ModellingObjectRequestLimits
{
    /// <summary>
    /// Maximum number of values in one filter list.
    /// </summary>
    public const int kMaxFilterValues = 100;

    /// <summary>
    /// Maximum length of the external application id and of each text filter value.
    /// </summary>
    public const int kMaxTextLength = 256;
}

/// <summary>
/// Represents a request for the interfaces an application may use.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GetAvailableInterfacesRequest
{
    /// <summary>
    /// Gets or sets the required external id of the application using the interfaces. Matching ignores case; at
    /// most 256 characters.
    /// </summary>
    [JsonPropertyName("appIdExternal")]
    public string? AppIdExternal { get; set; }

    /// <summary>
    /// Gets or sets the optional response options. When omitted, this defaults to an empty object.
    /// </summary>
    [JsonPropertyName("options")]
    public ModellingListOptions<AvailableInterfacesFilter>? Options { get; set; } = new();
}

/// <summary>
/// Represents a request for the app roles of an application.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GetAppRolesPerAppRequest
{
    /// <summary>
    /// Gets or sets the required external id of the application owning the app roles. Matching ignores case; at
    /// most 256 characters.
    /// </summary>
    [JsonPropertyName("appIdExternal")]
    public string? AppIdExternal { get; set; }

    /// <summary>
    /// Gets or sets the optional response options. When omitted, this defaults to an empty object.
    /// </summary>
    [JsonPropertyName("options")]
    public ModellingListOptions<ModellingGroupFilter>? Options { get; set; } = new();
}

/// <summary>
/// Represents a request for network areas.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GetNetworkAreasRequest
{
    /// <summary>
    /// Gets or sets the optional response options. When omitted, this defaults to an empty object.
    /// </summary>
    [JsonPropertyName("options")]
    public ModellingListOptions<ModellingGroupFilter>? Options { get; set; } = new();
}

/// <summary>
/// Represents the optional filter and paging options of a modelling list endpoint.
/// </summary>
/// <typeparam name="TFilter">Type of the endpoint's filter.</typeparam>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ModellingListOptions<TFilter> where TFilter : class
{
    /// <summary>
    /// Gets or sets the optional response filter. Null or omitted filter fields do not restrict the result.
    /// </summary>
    [JsonPropertyName("filter")]
    public TFilter? Filter { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items returned, between 1 and the configured
    /// <c>maxRestResponseObjectLimit</c> (default 10000). When omitted or null, a page of that size is returned.
    /// Items are ordered by name and id; the <c>X-Has-More</c> response header tells whether further items follow.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the number of items skipped before the first returned item, at least 0. When omitted or null,
    /// no item is skipped.
    /// </summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; set; }
}

/// <summary>
/// Represents nullable multi-value filters for interfaces. Values of the same field are OR-connected; filters for
/// different fields are AND-connected. Empty or omitted lists do not restrict the result. Each list can contain at
/// most 100 values, and each text value at most 256 characters. Text filters ignore case and support <c>*</c> for
/// any character sequence and <c>?</c> for one character.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AvailableInterfacesFilter
{
    /// <summary>
    /// Gets or sets the optional interface connection id filters.
    /// </summary>
    [JsonPropertyName("connectionId")]
    public List<int>? ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the optional interface name filters; plain text without wildcards is a contains search.
    /// </summary>
    [JsonPropertyName("interfaceName")]
    public List<string>? InterfaceName { get; set; }

    /// <summary>
    /// Gets or sets the optional interface description filters; plain text without wildcards is a contains search.
    /// </summary>
    [JsonPropertyName("interfaceDescription")]
    public List<string>? InterfaceDescription { get; set; }

    /// <summary>
    /// Gets or sets the optional permission filters: <c>public</c>, <c>restricted</c> or <c>private</c>.
    /// </summary>
    [JsonPropertyName("permission")]
    public List<string>? Permission { get; set; }

    /// <summary>
    /// Gets or sets the optional providing application name filters; plain text without wildcards is a contains search.
    /// </summary>
    [JsonPropertyName("providingAppName")]
    public List<string>? ProvidingAppName { get; set; }

    /// <summary>
    /// Gets or sets the optional providing external application id filters; plain text without wildcards must
    /// match exactly.
    /// </summary>
    [JsonPropertyName("providingAppIdExternal")]
    public List<string>? ProvidingAppIdExternal { get; set; }
}

/// <summary>
/// Represents nullable multi-value filters for app roles and network areas. Values of the same field are
/// OR-connected; filters for different fields are AND-connected. Empty or omitted lists do not restrict the result.
/// Each list can contain at most 100 values, and each text value at most 256 characters. Text filters ignore case
/// and support <c>*</c> for any character sequence and <c>?</c> for one character.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ModellingGroupFilter
{
    /// <summary>
    /// Gets or sets the optional id filters.
    /// </summary>
    [JsonPropertyName("id")]
    public List<long>? Id { get; set; }

    /// <summary>
    /// Gets or sets the optional id string filters; plain text without wildcards must match exactly.
    /// </summary>
    [JsonPropertyName("idString")]
    public List<string>? IdString { get; set; }

    /// <summary>
    /// Gets or sets the optional name filters; plain text without wildcards is a contains search.
    /// </summary>
    [JsonPropertyName("name")]
    public List<string>? Name { get; set; }
}
