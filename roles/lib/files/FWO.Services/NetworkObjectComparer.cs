using FWO.Data;

namespace FWO.Services
{
    public class NetworkObjectComparer() : IEqualityComparer<NetworkObject?>
    {
        public bool Equals(NetworkObject? nwObject1, NetworkObject? nwObject2)
        {
            if (ReferenceEquals(nwObject1, nwObject2))
            {
                return true;
            }

            if (nwObject1 is null || nwObject2 is null)
            {
                return false;
            }

            return nwObject1.IP == nwObject2.IP
                && nwObject1.IpEnd == nwObject2.IpEnd
                && nwObject1.Name == nwObject2.Name;
        }

        public int GetHashCode(NetworkObject nwObject)
        {
            return nwObject.GetHashCode();
        }
    }

        public class NetworkObjectGroupComparer() : IEqualityComparer<NetworkObject?>
    {
        public bool Equals(NetworkObject? nwObject1, NetworkObject? nwObject2)
        {
            if (ReferenceEquals(nwObject1, nwObject2))
            {
                return true;
            }

            if (nwObject1 is null || nwObject2 is null)
            {
                return false;
            }

            if (nwObject1.ObjectGroupFlats.Length == nwObject2.ObjectGroupFlats.Length
                && nwObject1.Name == nwObject2.Name)
            {
                return false;
            }

            NetworkObjectComparer networkObjectComparer = new();
            List<NetworkObject?> oneNotTwo = nwObject1.ObjectGroupFlats.ToList().ConvertAll(g => g.Object).ToList()
                .Except(nwObject2.ObjectGroupFlats.ToList().ConvertAll(g => g.Object).ToList(), networkObjectComparer).ToList();
            List<NetworkObject?> twoNotOne = nwObject2.ObjectGroupFlats.ToList().ConvertAll(g => g.Object).ToList()
                .Except(nwObject1.ObjectGroupFlats.ToList().ConvertAll(g => g.Object).ToList(), networkObjectComparer).ToList();

            return oneNotTwo.Count == 0 && twoNotOne.Count == 0;
        }

        public int GetHashCode(NetworkObject nwObject)
        {
            return nwObject.GetHashCode();
        }
    }
}
