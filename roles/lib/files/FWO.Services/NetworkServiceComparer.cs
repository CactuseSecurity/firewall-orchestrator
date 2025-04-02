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

            return option.SvcRegardPortAndProt ? service1.ProtoId == service2.ProtoId
                    && service1.DestinationPort == service2.DestinationPort
                    && service1.DestinationPortEnd == service2.DestinationPortEnd : true
                && option.SvcRegardName ? service1.Name == service2.Name : true;
        }

        public int GetHashCode(NetworkService service)
        {
            return (option.SvcRegardPortAndProt ? HashCode.Combine(service.ProtoId, service.DestinationPort, service.DestinationPortEnd) : 0)
                ^ (option.SvcRegardName ? HashCode.Combine(service.Name) : 0);
        }
    }

    public class NetworkServiceGroupComparer(RuleRecognitionOption option) : IEqualityComparer<NetworkService?>
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

            if (service1.ServiceGroupFlats.Length != service2.ServiceGroupFlats.Length
                || (option.SvcRegardGroupName && service1.Name != service2.Name))
            {
                return false;
            }

            NetworkServiceComparer networkServiceComparer = new(option);
            return service1.ServiceGroupFlats.ToList().ConvertAll(g => g.Object).ToList()
                    .Except([.. service2.ServiceGroupFlats.ToList().ConvertAll(g => g.Object)], networkServiceComparer).ToList().Count == 0 
                && service2.ServiceGroupFlats.ToList().ConvertAll(g => g.Object).ToList()
                    .Except([.. service1.ServiceGroupFlats.ToList().ConvertAll(g => g.Object)], networkServiceComparer).ToList().Count == 0;
        }

        public int GetHashCode(NetworkService service)
        {
            int hashCode = 0;
            foreach(var obj in service.ServiceGroupFlats)
            {
                hashCode ^= obj.GetHashCode();
            }
            return hashCode ^ (option.SvcRegardGroupName ? 
                HashCode.Combine(service.TypeId, service.Name) :
                HashCode.Combine(service.TypeId));
        }
    }
}
