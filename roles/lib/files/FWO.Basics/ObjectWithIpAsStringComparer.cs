using System.Net;
using System.Net.Sockets;

namespace FWO.Basics
{
    public class ObjectWithIpAsStringComparer : IComparer<object>
    {
        /// <summary>
        /// Orders to IPs by the IP family, the value of the IP and the subenet mask.
        /// </summary>
        public int Compare(object? x, object? y)
        {
            if (x == null || y == null) return 0;

            if (x is not string) throw new ArgumentException("Argument 'x' is not a string");
            if (y is not string) throw new ArgumentException("Argument 'y' is not a string");

            var _ip1 = x.ToString().ToIPAdressAndSubnetMask();
            var _ip2 = y.ToString().ToIPAdressAndSubnetMask();

            int compareIpFamilyResult = IpOperations.CompareIpFamilies(_ip1.Item1, _ip2.Item1);
            if (compareIpFamilyResult != 0) return compareIpFamilyResult;

            int compareIpValuesResult = IpOperations.CompareIpValues(_ip1.Item1, _ip2.Item1);
            if (compareIpValuesResult != 0) return compareIpValuesResult;

            return IpOperations.CompareSubnetMasks(_ip1.Item2, _ip2.Item2);
        }
    }
}
