using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents a request for application-zone objects.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GetApplicationZonesRequest
{
    /// <summary>
    /// Gets or sets the optional response options. When omitted, this defaults to an empty object.
    /// </summary>
    [JsonPropertyName("options")]
    public GetApplicationZonesOptions? Options { get; set; } = new();
}

/// <summary>
/// Represents optional application-zone response options.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GetApplicationZonesOptions
{
    /// <summary>
    /// Gets or sets the level of detail returned for every application. This defaults to <c>full</c>, which returns
    /// the complete application-zone response. Set it to <c>ip-only</c> to return only the external application id
    /// and compact IP addresses for each application. A null value has the same behavior as the default.
    /// </summary>
    [JsonPropertyName("details-level")]
    public string? DetailsLevel { get; set; } = "full";

    /// <summary>
    /// Gets or sets the optional response filter. Null or omitted filter fields do not restrict the result.
    /// </summary>
    [JsonPropertyName("filter")]
    public ApplicationZoneFilter? Filter { get; set; }

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
/// Represents nullable filters for every top-level application-zone response field. Application fields select
/// applications before zones are loaded. Zone fields apply only to concrete zones, so they exclude placeholders for
/// applications without a zone. Deleted zones and member addresses are excluded by default. String filters are
/// case-insensitive and support <c>*</c> for any character sequence and <c>?</c> for one character; plain text
/// without wildcards is matched as a contains search, matching the owner endpoint.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ApplicationZoneFilter
{
    /// <summary>
    /// Gets or sets the optional application id filter. This selects the matching visible application, including one
    /// without an application-zone.
    /// </summary>
    [JsonPropertyName("applicationId")]
    public int? ApplicationId { get; set; }

    /// <summary>
    /// Gets or sets the optional case-insensitive application name filter with <c>*</c> and <c>?</c> wildcards.
    /// This selects matching visible applications, including applications without an application-zone.
    /// </summary>
    [JsonPropertyName("applicationName")]
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets the optional case-insensitive external application-id filter with <c>*</c> and <c>?</c> wildcards.
    /// This selects matching visible applications, including applications without an application-zone.
    /// </summary>
    [JsonPropertyName("appIdExternal")]
    public string? AppIdExternal { get; set; }

    /// <summary>
    /// Gets or sets the optional application-zone database id filter.
    /// </summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>
    /// Gets or sets the optional case-insensitive application-zone name filter with <c>*</c> and <c>?</c> wildcards.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the optional case-insensitive application-zone identifier filter with <c>*</c> and <c>?</c> wildcards.
    /// </summary>
    [JsonPropertyName("idString")]
    public string? IdString { get; set; }

    /// <summary>
    /// Gets or sets the optional deleted-state filter for concrete zones. Deleted zones are excluded from this
    /// endpoint, so <c>true</c> returns no results and <c>false</c> excludes applications without a zone.
    /// </summary>
    [JsonPropertyName("isDeleted")]
    public bool? IsDeleted { get; set; }
}
