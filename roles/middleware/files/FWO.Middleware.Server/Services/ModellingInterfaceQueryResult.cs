using System.Text.Json.Serialization;
using FWO.Data;
using FWO.Data.Modelling;
using Newtonsoft.Json;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// One interface as read by the <c>getAvailableInterfacesForApp</c> query, with its network objects split by type.
/// </summary>
public sealed class ModellingInterfaceQueryResult
{
    /// <summary>Gets or sets the connection id.</summary>
    [JsonProperty("id"), JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Gets or sets the interface name.</summary>
    [JsonProperty("name"), JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the interface description.</summary>
    [JsonProperty("reason"), JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>Gets or sets the stored interface permission; null means public.</summary>
    [JsonProperty("interface_permission"), JsonPropertyName("interface_permission")]
    public string? InterfacePermission { get; set; }

    /// <summary>Gets or sets the providing application.</summary>
    [JsonProperty("owner"), JsonPropertyName("owner")]
    public FwoOwner? Owner { get; set; }

    /// <summary>Gets or sets the source app servers.</summary>
    [JsonProperty("source_nwobjects"), JsonPropertyName("source_nwobjects")]
    public List<ModellingAppServerWrapper> SourceAppServers { get; set; } = [];

    /// <summary>Gets or sets the source app roles.</summary>
    [JsonProperty("source_approles"), JsonPropertyName("source_approles")]
    public List<ModellingAppRoleWrapper> SourceAppRoles { get; set; } = [];

    /// <summary>Gets or sets the source network areas.</summary>
    [JsonProperty("source_areas"), JsonPropertyName("source_areas")]
    public List<ModellingNetworkAreaWrapper> SourceAreas { get; set; } = [];

    /// <summary>Gets or sets the other source network groups; members are read like app role members.</summary>
    [JsonProperty("source_other_groups"), JsonPropertyName("source_other_groups")]
    public List<ModellingAppRoleWrapper> SourceOtherGroups { get; set; } = [];

    /// <summary>Gets or sets the destination app servers.</summary>
    [JsonProperty("destination_nwobjects"), JsonPropertyName("destination_nwobjects")]
    public List<ModellingAppServerWrapper> DestinationAppServers { get; set; } = [];

    /// <summary>Gets or sets the destination app roles.</summary>
    [JsonProperty("destination_approles"), JsonPropertyName("destination_approles")]
    public List<ModellingAppRoleWrapper> DestinationAppRoles { get; set; } = [];

    /// <summary>Gets or sets the destination network areas.</summary>
    [JsonProperty("destination_areas"), JsonPropertyName("destination_areas")]
    public List<ModellingNetworkAreaWrapper> DestinationAreas { get; set; } = [];

    /// <summary>Gets or sets the other destination network groups; members are read like app role members.</summary>
    [JsonProperty("destination_other_groups"), JsonPropertyName("destination_other_groups")]
    public List<ModellingAppRoleWrapper> DestinationOtherGroups { get; set; } = [];

    /// <summary>Gets or sets the single services.</summary>
    [JsonProperty("services"), JsonPropertyName("services")]
    public List<ModellingServiceWrapper> Services { get; set; } = [];

    /// <summary>Gets or sets the service groups.</summary>
    [JsonProperty("service_groups"), JsonPropertyName("service_groups")]
    public List<ModellingServiceGroupWrapper> ServiceGroups { get; set; } = [];
}
