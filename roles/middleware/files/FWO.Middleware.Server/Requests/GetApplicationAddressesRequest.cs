using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents a request for application addresses.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GetApplicationAddressesRequest
{
    /// <summary>
    /// Gets or sets the optional response options. When omitted, this defaults to an empty object.
    /// </summary>
    [JsonPropertyName("options")]
    public GetApplicationAddressesOptions? Options { get; set; } = new();
}

/// <summary>
/// Represents optional application-address response options.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GetApplicationAddressesOptions
{
    /// <summary>
    /// Gets or sets the optional response filter. Null or omitted filter fields do not restrict the result.
    /// </summary>
    [JsonPropertyName("filter")]
    public ApplicationAddressFilter? Filter { get; set; }

    /// <summary>
    /// Gets or sets whether applications with an inactive lifecycle state are excluded. This defaults to
    /// <c>true</c>; set it to <c>false</c> to also return them. Applications without any lifecycle state are
    /// always returned.
    /// </summary>
    [JsonPropertyName("showOnlyActiveState")]
    public bool? ShowOnlyActiveState { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum number of applications to read. When omitted, every matching
    /// application is returned. Applications are ordered by name, so this pages the result deterministically
    /// together with <see cref="Offset"/>.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the optional number of applications to skip before the first returned application.
    /// </summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; set; }
}

/// <summary>
/// Represents nullable multi-value filters for application identity fields. Values of the same field are OR-connected;
/// filters for different fields are AND-connected. Empty or omitted lists do not restrict the result. String filters
/// are case-insensitive and support <c>*</c> for any character sequence and <c>?</c> for one character; plain text
/// without wildcards is matched as a contains search, matching the owner endpoint. Each filter list can contain at
/// most 100 values. String filter values must not be blank.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ApplicationAddressFilter
{
    /// <summary>
    /// Gets or sets the optional application id filters.
    /// </summary>
    [JsonPropertyName("applicationId")]
    public List<int>? ApplicationId { get; set; }

    /// <summary>
    /// Gets or sets the optional case-insensitive application name filters with <c>*</c> and <c>?</c> wildcards.
    /// </summary>
    [JsonPropertyName("applicationName")]
    public List<string>? ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets the optional case-insensitive external application-id filters with <c>*</c> and <c>?</c> wildcards.
    /// </summary>
    [JsonPropertyName("appIdExternal")]
    public List<string>? AppIdExternal { get; set; }
}
