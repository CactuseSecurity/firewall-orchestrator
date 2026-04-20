using FWO.Basics;
using FWO.Data;

namespace FWO.Test
{
    internal static class TrivialityTestHelper
    {
        internal static Rule CreateRule(List<NetworkLocation> froms, List<NetworkLocation> tos, List<NetworkService>? services = null, int mgmtId = 0, long id = 0, string uid = "", string action = RuleActions.Accept, bool disabled = false)
        {
            return new()
            {
                Action = action,
                Disabled = disabled,
                Id = id,
                MgmtId = mgmtId,
                Uid = uid,
                Froms = [.. froms],
                Tos = [.. tos],
                Services = [.. (services ?? []).Select(service => new ServiceWrapper { Content = service })]
            };
        }

        internal static NetworkLocation CreateNetworkLocation(NetworkObject networkObject)
        {
            return new(new NetworkUser(), networkObject);
        }

        internal static NetworkObject CreateNetworkObject(string name, string ip, string ipEnd)
        {
            return new()
            {
                Name = name,
                IP = ip,
                IpEnd = ipEnd,
                Type = new NetworkObjectType
                {
                    Name = ObjectType.Network
                }
            };
        }

        internal static NetworkObject CreateGroup(string name, params NetworkObject[] members)
        {
            return new()
            {
                Name = name,
                Type = new NetworkObjectType
                {
                    Name = ObjectType.Group
                },
                ObjectGroupFlats = [.. members.Select(member => new GroupFlat<NetworkObject> { Object = member })]
            };
        }

        internal static NetworkService CreateService(string name, int protoId, int portStart, int portEnd)
        {
            return new()
            {
                Name = name,
                ProtoId = protoId,
                DestinationPort = portStart,
                DestinationPortEnd = portEnd,
                Type = new NetworkServiceType
                {
                    Name = ServiceType.SimpleService
                }
            };
        }

        internal static NetworkService CreateProtocolService(string name, int protoId, int sourcePortStart, int sourcePortEnd, int destinationPortStart, int destinationPortEnd)
        {
            return new()
            {
                Name = name,
                ProtoId = protoId,
                SourcePort = sourcePortStart,
                SourcePortEnd = sourcePortEnd,
                DestinationPort = destinationPortStart,
                DestinationPortEnd = destinationPortEnd,
                Type = new NetworkServiceType
                {
                    Name = ServiceType.SimpleService
                }
            };
        }

        internal static NetworkService CreatePortOnlyService(string name, int sourcePortStart, int sourcePortEnd, int destinationPortStart, int destinationPortEnd)
        {
            return new()
            {
                Name = name,
                SourcePort = sourcePortStart,
                SourcePortEnd = sourcePortEnd,
                DestinationPort = destinationPortStart,
                DestinationPortEnd = destinationPortEnd,
                Type = new NetworkServiceType
                {
                    Name = ServiceType.SimpleService
                }
            };
        }

        internal static NetworkService CreateServiceGroup(string name, params NetworkService[] members)
        {
            return new()
            {
                Name = name,
                Type = new NetworkServiceType
                {
                    Name = ServiceType.Group
                },
                ServiceGroupFlats = [.. members.Select(member => new GroupFlat<NetworkService> { Object = member })]
            };
        }
    }
}
