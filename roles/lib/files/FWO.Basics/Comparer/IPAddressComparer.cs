using System.Net;
using NetTools;
using FWO.Basics;

namespace FWO.Basics.Comparer
{
    public class IPAdressComparer : IComparer<IPAddress>
    {
        public int Compare(IPAddress? x, IPAddress? y)
        {
            if(x is null || y is null)
            {
                return 0;
            } 

            int compareIPFamiliesResult = IpOperations.CompareIpFamilies(x, y);

            if (compareIPFamiliesResult != 0)
            {
                return compareIPFamiliesResult;
            }

            return IpOperations.CompareIpValues(x, y);
        }
    }
}