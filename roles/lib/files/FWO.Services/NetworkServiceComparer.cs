using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;

namespace FWO.Services
{
    public class NetworkServiceComparer(RuleRecognitionOption option) : IEqualityComparer<NetworkService?>
    {
        public bool Equals(NetworkService? service1, NetworkService? service2)
        {
            if (ReferenceEquals(service1, service2))
            {
                return true;
            }

            if (service1 is null || service2 is null)
            {
                return false;
            }

            int destPortEnd1 = service1.DestinationPortEnd ?? service1.DestinationPort ?? 0;
            int destPortEnd2 = service2.DestinationPortEnd ?? service2.DestinationPort ?? 0;

            return (!option.SvcRegardPortAndProt || service1.ProtoId == service2.ProtoId
                    && service1.DestinationPort == service2.DestinationPort
                    && destPortEnd1 == destPortEnd2)
                && (!option.SvcRegardName || service1.Name == service2.Name);
        }

        public int GetHashCode(NetworkService service)
        {
            int destPortEnd = service.DestinationPortEnd ?? service.DestinationPort ?? 0;
            return (option.SvcRegardPortAndProt ? HashCode.Combine(service.ProtoId, service.DestinationPort, destPortEnd) : 0)
                ^ (option.SvcRegardName ? HashCode.Combine(service.Name) : 0);
        }
    }

    public class NetworkServiceGroupComparer(RuleRecognitionOption option) : IEqualityComparer<NetworkService?>
    {
        readonly NetworkServiceComparer networkServiceComparer = new(option);

        public bool Equals(NetworkService? service1, NetworkService? service2)
        {
            if (ReferenceEquals(service1, service2))
            {
                return true;
            }

            if (service1 is null || service2 is null)
            {
                return false;
            }

            if (service1.ServiceGroupFlats.Length != service2.ServiceGroupFlats.Length
                || (option.SvcRegardGroupName && service1.Name != service2.Name))
            {
                return false;
            }

            return service1.ServiceGroupFlats.ToList().ConvertAll(g => g.Object).ToList()
                    .Except([.. service2.ServiceGroupFlats.ToList().ConvertAll(g => g.Object)], networkServiceComparer).ToList().Count == 0 
                && service2.ServiceGroupFlats.ToList().ConvertAll(g => g.Object).ToList()
                    .Except([.. service1.ServiceGroupFlats.ToList().ConvertAll(g => g.Object)], networkServiceComparer).ToList().Count == 0;
        }

        public int GetHashCode(NetworkService serviceGrp)
        {
            int hashCode = 0;
            foreach(var svc in serviceGrp.ServiceGroupFlats.Where(s => s.Object?.Type.Name != ServiceType.Group).ToList())
            {
                hashCode ^= (svc.Object != null ? networkServiceComparer.GetHashCode(svc.Object) : 0);
            }
            return hashCode ^ (option.SvcRegardGroupName ? HashCode.Combine(serviceGrp.Name) : 0);
        }
    }
}
