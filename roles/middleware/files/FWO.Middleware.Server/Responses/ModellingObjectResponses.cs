using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents the interfaces an application may use.
/// </summary>
public sealed class AvailableInterfacesResponse
{
    /// <summary>
    /// Gets or sets one page of available interfaces, ordered by name and id.
    /// </summary>
    [JsonPropertyName("interfaces")]
    public List<ModellingInterfaceResponse> Interfaces { get; set; } = [];
}

/// <summary>
/// Represents the app roles of one application.
/// </summary>
public sealed class AppRolesPerAppResponse
{
    /// <summary>
    /// Gets or sets one page of undeleted app roles, ordered by name and id. AppZones are not included.
    /// </summary>
    [JsonPropertyName("appRoles")]
    public List<ModellingAppRoleResponse> AppRoles { get; set; } = [];
}

/// <summary>
/// Represents network areas.
/// </summary>
public sealed class NetworkAreasResponse
{
    /// <summary>
    /// Gets or sets one page of undeleted network areas, ordered by name and id.
    /// </summary>
    [JsonPropertyName("networkAreas")]
    public List<ModellingNetworkAreaResponse> NetworkAreas { get; set; } = [];
}

/// <summary>
/// Represents one published interface with all of its resolved network and service objects.
/// </summary>
public sealed class ModellingInterfaceResponse
{
    /// <summary>
    /// Gets or sets the id of the interface connection.
    /// </summary>
    [JsonPropertyName("connectionId")]
    public int ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the interface name.
    /// </summary>
    [JsonPropertyName("interfaceName")]
    public string InterfaceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the interface description.
    /// </summary>
    [JsonPropertyName("interfaceDescription")]
    public string InterfaceDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the interface permission: <c>public</c>, <c>restricted</c> or <c>private</c>.
    /// </summary>
    [JsonPropertyName("permission")]
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the application providing the interface.
    /// </summary>
    [JsonPropertyName("providingAppName")]
    public string ProvidingAppName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external application id of the application providing the interface.
    /// </summary>
    [JsonPropertyName("providingAppIdExternal")]
    public string? ProvidingAppIdExternal { get; set; }

    /// <summary>
    /// Gets or sets the source network objects of the interface.
    /// </summary>
    [JsonPropertyName("source")]
    public ModellingNetworkSideResponse Source { get; set; } = new();

    /// <summary>
    /// Gets or sets the destination network objects of the interface.
    /// </summary>
    [JsonPropertyName("destination")]
    public ModellingNetworkSideResponse Destination { get; set; } = new();

    /// <summary>
    /// Gets or sets the services of the interface.
    /// </summary>
    [JsonPropertyName("service")]
    public ModellingServiceSideResponse Service { get; set; } = new();
}

/// <summary>
/// Represents the network objects on one side of an interface, grouped by object type. Unused types are empty lists.
/// </summary>
public sealed class ModellingNetworkSideResponse
{
    /// <summary>
    /// Gets or sets the undeleted app servers.
    /// </summary>
    [JsonPropertyName("appServers")]
    public List<ModellingAppServerResponse> AppServers { get; set; } = [];

    /// <summary>
    /// Gets or sets the undeleted app roles with their resolved members.
    /// </summary>
    [JsonPropertyName("appRoles")]
    public List<ModellingAppRoleResponse> AppRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the undeleted network areas with their subnets.
    /// </summary>
    [JsonPropertyName("networkAreas")]
    public List<ModellingNetworkAreaResponse> NetworkAreas { get; set; } = [];

    /// <summary>
    /// Gets or sets the other undeleted network groups with their resolved members.
    /// </summary>
    [JsonPropertyName("networkGroups")]
    public List<ModellingNetworkGroupResponse> NetworkGroups { get; set; } = [];
}

/// <summary>
/// Represents the services of an interface, grouped by object type. Unused types are empty lists.
/// </summary>
public sealed class ModellingServiceSideResponse
{
    /// <summary>
    /// Gets or sets the single services.
    /// </summary>
    [JsonPropertyName("services")]
    public List<ModellingServiceResponse> Services { get; set; } = [];

    /// <summary>
    /// Gets or sets the service groups with their resolved member services.
    /// </summary>
    [JsonPropertyName("serviceGroups")]
    public List<ModellingServiceGroupResponse> ServiceGroups { get; set; } = [];
}

/// <summary>
/// Represents one app server.
/// </summary>
public sealed class ModellingAppServerResponse
{
    /// <summary>
    /// Gets or sets the app server id.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the app server name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the address: a single IP for a host, CIDR notation for a network, or <c>ip1-ip2</c> for a range.
    /// </summary>
    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;
}

/// <summary>
/// Represents one app role with its resolved app servers.
/// </summary>
public sealed class ModellingAppRoleResponse
{
    /// <summary>
    /// Gets or sets the app role id.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the app role id string.
    /// </summary>
    [JsonPropertyName("idString")]
    public string IdString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the app role name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the undeleted member app servers.
    /// </summary>
    [JsonPropertyName("appServers")]
    public List<ModellingAppServerResponse> AppServers { get; set; } = [];

    /// <summary>
    /// Gets or sets the distinct addresses of all member app servers.
    /// </summary>
    [JsonPropertyName("ipAddresses")]
    public List<string> IpAddresses { get; set; } = [];
}

/// <summary>
/// Represents one network area with its subnets.
/// </summary>
public sealed class ModellingNetworkAreaResponse
{
    /// <summary>
    /// Gets or sets the network area id.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the network area id string.
    /// </summary>
    [JsonPropertyName("idString")]
    public string IdString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the network area name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the distinct addresses of all undeleted subnets of the area.
    /// </summary>
    [JsonPropertyName("ipAddresses")]
    public List<string> IpAddresses { get; set; } = [];
}

/// <summary>
/// Represents one network group that is neither an app role nor a network area, with its resolved members.
/// </summary>
public sealed class ModellingNetworkGroupResponse
{
    /// <summary>
    /// Gets or sets the network group id.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the network group id string.
    /// </summary>
    [JsonPropertyName("idString")]
    public string IdString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the network group name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the distinct addresses of all undeleted members of the group.
    /// </summary>
    [JsonPropertyName("ipAddresses")]
    public List<string> IpAddresses { get; set; } = [];
}

/// <summary>
/// Represents one service.
/// </summary>
public sealed class ModellingServiceResponse
{
    /// <summary>
    /// Gets or sets the service id.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the service name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the service notation: <c>port/protocol</c>, <c>portStart-portEnd/protocol</c>, or the protocol
    /// name alone for services without ports.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Represents one service group with its resolved member services.
/// </summary>
public sealed class ModellingServiceGroupResponse
{
    /// <summary>
    /// Gets or sets the service group id.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the service group name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the member services.
    /// </summary>
    [JsonPropertyName("services")]
    public List<ModellingServiceResponse> Services { get; set; } = [];
}
