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

            return (!option.NwRegardIp || (string.Equals(nwObject1.IP, nwObject2.IP, StringComparison.Ordinal)
                    && string.Equals(nwObject1.IpEnd, nwObject2.IpEnd, StringComparison.Ordinal)))
                && (!option.NwRegardName || string.Equals(nwObject1.Name, nwObject2.Name, StringComparison.Ordinal));
        }

        public int GetHashCode(NetworkObject? nwObject)
        {
            if (nwObject is null)
            {
                return 0;
            }

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

            if(option.NwSeparateGroupAnalysis)
            {
                return !option.NwRegardGroupName || string.Equals(nwObjectGrp1.Name, nwObjectGrp2.Name, StringComparison.Ordinal);
            }

            List<NetworkObject> objectList1 = nwObjectGrp1.ObjectGroupFlats
                .Where(o => o.Object != null && !string.Equals(o.Object.Type.Name, ObjectType.Group, StringComparison.Ordinal))
                .Select(o => o.Object!)
                .ToList();
            List<NetworkObject> objectList2 = nwObjectGrp2.ObjectGroupFlats
                .Where(o => o.Object != null && !string.Equals(o.Object.Type.Name, ObjectType.Group, StringComparison.Ordinal))
                .Select(o => o.Object!)
                .ToList();

            if (objectList1.Count != objectList2.Count
                || (option.NwRegardGroupName && !string.Equals(nwObjectGrp1.Name, nwObjectGrp2.Name, StringComparison.Ordinal)))
            {
                return false;
            }

            return !objectList1.Except(objectList2, networkObjectComparer).Any()
                && !objectList2.Except(objectList1, networkObjectComparer).Any();
        }

        public int GetHashCode(NetworkObject? nwObject)
        {
            if (nwObject is null)
            {
                return 0;
            }

            int hashCode = 0;
            
            if(!option.NwSeparateGroupAnalysis)
            {
                foreach(var obj in nwObject.ObjectGroupFlats.Where(o => o.Object != null && !string.Equals(o.Object.Type.Name, ObjectType.Group, StringComparison.Ordinal)).Select(o => o.Object!))
                {
                    hashCode ^= networkObjectComparer.GetHashCode(obj);
                }
            }
            return hashCode ^ (option.NwRegardGroupName ? 
                HashCode.Combine(nwObject.Type.Name, nwObject.Name) :
                HashCode.Combine(nwObject.Type.Name));
        }
    }
}
