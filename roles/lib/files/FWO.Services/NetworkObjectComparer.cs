using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;

namespace FWO.Services
{
    public class NetworkObjectComparer(RuleRecognitionOption option) : IEqualityComparer<NetworkObject?>
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

            return option.NwRegardIp ? nwObject1.IP == nwObject2.IP && nwObject1.IpEnd == nwObject2.IpEnd : true
                && option.NwRegardName ? nwObject1.Name == nwObject2.Name : true;
        }

        public int GetHashCode(NetworkObject nwObject)
        {
            return (option.NwRegardIp ? HashCode.Combine(nwObject.IP, nwObject.IpEnd) : 0)
                ^ (option.NwRegardName ? HashCode.Combine(nwObject.Name) : 0);
        }
    }

    public class NetworkObjectGroupFlatComparer(RuleRecognitionOption option) : IEqualityComparer<NetworkObject?>
    {
        private readonly NetworkObjectComparer networkObjectComparer = new(option);

        public bool Equals(NetworkObject? nwObjectGrp1, NetworkObject? nwObjectGrp2)
        {
            if (ReferenceEquals(nwObjectGrp1, nwObjectGrp2))
            {
                return true;
            }

            if (nwObjectGrp1 is null || nwObjectGrp2 is null)
            {
                return false;
            }

            List<NetworkObject> objectList1 = [.. nwObjectGrp1.ObjectGroupFlats.Where(o => o.Object != null && o.Object?.Type.Name != ObjectType.Group).ToList().ConvertAll(g => g.Object)];
            List<NetworkObject> objectList2 = [.. nwObjectGrp2.ObjectGroupFlats.Where(o => o.Object != null && o.Object?.Type.Name != ObjectType.Group).ToList().ConvertAll(g => g.Object)];

            if (objectList1.Count != objectList2.Count
                || (option.NwRegardGroupName && nwObjectGrp1.Name != nwObjectGrp2.Name))
            {
                return false;
            }

            return objectList1.Except(objectList2, networkObjectComparer).ToList().Count == 0 
                && objectList2.Except(objectList1, networkObjectComparer).ToList().Count == 0;
        }

        public int GetHashCode(NetworkObject nwObject)
        {
            int hashCode = 0;
            
            foreach(var obj in nwObject.ObjectGroupFlats.Where(o => o.Object?.Type.Name != ObjectType.Group).ToList())
            {
                hashCode ^= obj.Object != null ? networkObjectComparer.GetHashCode(obj.Object) : 0;
            }
            return hashCode ^ (option.NwRegardGroupName ? 
                HashCode.Combine(nwObject.Type.Name, nwObject.Name) :
                HashCode.Combine(nwObject.Type.Name));
        }
    }
}
