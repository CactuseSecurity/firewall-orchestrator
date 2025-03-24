using FWO.Data;

namespace FWO.Services
{
    public class NetworkServiceComparer() : IEqualityComparer<NetworkService>
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

            return service1.ProtoId == service2.ProtoId
                // && service1.SourcePort == service2.SourcePort
                // && service1.SourcePortEnd == service2.SourcePortEnd
                && service1.DestinationPort == service2.DestinationPort
                && service1.DestinationPortEnd == service2.DestinationPortEnd;
        }

        public int GetHashCode(NetworkService service)
        {
            return service.GetHashCode();
        }
    }
}
