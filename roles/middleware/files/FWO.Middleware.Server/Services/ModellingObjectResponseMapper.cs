using FWO.Basics;
using FWO.Data.Modelling;
using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Maps modelling objects read from the API to the public response shapes of the modelling object endpoints.
/// Addresses use compact notation: a single IP for a host, CIDR notation for a network and <c>ip1-ip2</c> for a range.
/// </summary>
public static class ModellingObjectResponseMapper
{
    /// <summary>
    /// Maps one interface with all of its network and service objects.
    /// </summary>
    public static ModellingInterfaceResponse ToInterfaceResponse(ModellingInterfaceQueryResult interf)
    {
        return new ModellingInterfaceResponse
        {
            ConnectionId = interf.Id,
            InterfaceName = interf.Name ?? string.Empty,
            InterfaceDescription = interf.Reason ?? string.Empty,
            Permission = ToPermissionValue(interf.InterfacePermission),
            ProvidingAppName = interf.Owner?.Name ?? string.Empty,
            ProvidingAppIdExternal = interf.Owner?.ExtAppId,
            Source = ToNetworkSide(interf.SourceAppServers, interf.SourceAppRoles, interf.SourceAreas, interf.SourceOtherGroups),
            Destination = ToNetworkSide(interf.DestinationAppServers, interf.DestinationAppRoles, interf.DestinationAreas,
                interf.DestinationOtherGroups),
            Service = new ModellingServiceSideResponse
            {
                Services = interf.Services.Select(wrapper => ToServiceResponse(wrapper.Content)).ToList(),
                ServiceGroups = interf.ServiceGroups.Select(wrapper => ToServiceGroupResponse(wrapper.Content)).ToList()
            }
        };
    }

    /// <summary>
    /// Maps one app role with its member app servers and their distinct addresses.
    /// </summary>
    public static ModellingAppRoleResponse ToAppRoleResponse(ModellingAppRole appRole)
    {
        List<ModellingAppServerResponse> appServers = appRole.AppServers
            .Select(wrapper => ToAppServerResponse(wrapper.Content))
            .ToList();
        return new ModellingAppRoleResponse
        {
            Id = appRole.Id,
            IdString = appRole.IdString ?? string.Empty,
            Name = appRole.Name ?? string.Empty,
            AppServers = appServers,
            IpAddresses = DistinctAddresses(appServers.Select(appServer => appServer.IpAddress))
        };
    }

    /// <summary>
    /// Maps one network area with the distinct addresses of its subnets.
    /// </summary>
    public static ModellingNetworkAreaResponse ToNetworkAreaResponse(ModellingNetworkArea area)
    {
        IEnumerable<string> addresses = area.IpData
            .Select(wrapper => ToCompactAddress(wrapper.Content.Ip, wrapper.Content.IpEnd));
        return new ModellingNetworkAreaResponse
        {
            Id = area.Id,
            IdString = area.IdString ?? string.Empty,
            Name = area.Name ?? string.Empty,
            IpAddresses = DistinctAddresses(addresses)
        };
    }

    /// <summary>
    /// Maps one network group that is neither an app role nor a network area, with the distinct addresses of its members.
    /// </summary>
    public static ModellingNetworkGroupResponse ToNetworkGroupResponse(ModellingAppRole nwGroup)
    {
        IEnumerable<string> addresses = nwGroup.AppServers
            .Select(wrapper => ToCompactAddress(wrapper.Content.Ip, wrapper.Content.IpEnd));
        return new ModellingNetworkGroupResponse
        {
            Id = nwGroup.Id,
            IdString = nwGroup.IdString ?? string.Empty,
            Name = nwGroup.Name ?? string.Empty,
            IpAddresses = DistinctAddresses(addresses)
        };
    }

    /// <summary>
    /// Maps one app server.
    /// </summary>
    public static ModellingAppServerResponse ToAppServerResponse(ModellingAppServer appServer)
    {
        return new ModellingAppServerResponse
        {
            Id = appServer.Id,
            Name = appServer.Name ?? string.Empty,
            IpAddress = ToCompactAddress(appServer.Ip, appServer.IpEnd)
        };
    }

    /// <summary>
    /// Maps one service.
    /// </summary>
    public static ModellingServiceResponse ToServiceResponse(ModellingService service)
    {
        return new ModellingServiceResponse
        {
            Id = service.Id,
            Name = service.Name ?? string.Empty,
            Value = ToServiceValue(service)
        };
    }

    /// <summary>
    /// Maps one service group with its member services.
    /// </summary>
    public static ModellingServiceGroupResponse ToServiceGroupResponse(ModellingServiceGroup serviceGroup)
    {
        return new ModellingServiceGroupResponse
        {
            Id = serviceGroup.Id,
            Name = serviceGroup.Name ?? string.Empty,
            Services = serviceGroup.Services.Select(wrapper => ToServiceResponse(wrapper.Content)).ToList()
        };
    }

    /// <summary>
    /// Builds the service notation: <c>port/protocol</c>, <c>portStart-portEnd/protocol</c>, or the protocol name
    /// alone for services without ports. Protocol names are lower case.
    /// </summary>
    public static string ToServiceValue(ModellingService service)
    {
        string protocol = service.Protocol?.Name is { Length: > 0 } protocolName
            ? protocolName.ToLowerInvariant()
            : service.ProtoId?.ToString() ?? string.Empty;
        if (service.Port is null)
        {
            return protocol;
        }

        string ports = service.PortEnd is null || service.PortEnd == service.Port
            ? $"{service.Port}"
            : $"{service.Port}-{service.PortEnd}";
        return string.IsNullOrEmpty(protocol) ? ports : $"{ports}/{protocol}";
    }

    /// <summary>
    /// Returns the public permission value; a missing permission is public.
    /// </summary>
    public static string ToPermissionValue(string? storedPermission)
    {
        return string.IsNullOrWhiteSpace(storedPermission)
            ? InterfacePermissions.Public.ToString().ToLowerInvariant()
            : storedPermission.Trim().ToLowerInvariant();
    }

    private static ModellingNetworkSideResponse ToNetworkSide(List<ModellingAppServerWrapper> appServers,
        List<ModellingAppRoleWrapper> appRoles, List<ModellingNetworkAreaWrapper> areas, List<ModellingAppRoleWrapper> otherGroups)
    {
        return new ModellingNetworkSideResponse
        {
            AppServers = appServers.Select(wrapper => ToAppServerResponse(wrapper.Content)).ToList(),
            AppRoles = appRoles.Select(wrapper => ToAppRoleResponse(wrapper.Content)).ToList(),
            NetworkAreas = areas.Select(wrapper => ToNetworkAreaResponse(wrapper.Content)).ToList(),
            NetworkGroups = otherGroups.Select(wrapper => ToNetworkGroupResponse(wrapper.Content)).ToList()
        };
    }

    private static string ToCompactAddress(string? ip, string? ipEnd)
    {
        return string.IsNullOrWhiteSpace(ip) ? string.Empty : IpOperations.ToCompactNotation(ip, ipEnd ?? string.Empty);
    }

    private static List<string> DistinctAddresses(IEnumerable<string> addresses)
    {
        return addresses
            .Where(address => !string.IsNullOrEmpty(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
