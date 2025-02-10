using System.Net;
using FWO.Basics;
using System.Net.Sockets;

namespace FWO.Services
{
    public class ObjectWithIpAsStringComparer : IComparer<object>
    {
        /// <summary>
        /// Orders to IPs by the IP family, the value of the IP and the subenet mask.
        /// </summary>
        public int Compare(object? x, object? y)
        {
            if (x == null || y == null) return 0;

            var _ip1 = ParseIpAndSplit(x.ToString());
            var _ip2 = ParseIpAndSplit(y.ToString());

            int compareIpFamilyResult = CompareIpFamilies(_ip1.Item1, _ip2.Item1);
            if (compareIpFamilyResult != 0) return compareIpFamilyResult;

            int compareIpValuesResult = CompareIpValues(_ip1.Item1, _ip2.Item1);
            if (compareIpValuesResult != 0) return compareIpValuesResult;

            return CompareSubnetMasks(_ip1.Item2, _ip2.Item2);
        }

        /// <summary>
        /// Parses the given string to an IPAdress object and a string that represents the subnet mask.
        /// </summary>
        private (IPAddress, string) ParseIpAndSplit(string ipString)
        {
            IPAddress ipAdress = IPAddress.Parse(ipString.StripOffNetmask());
            string subnetMask = "";
            var hasSubnetMask = ipString.TryGetNetmask(out subnetMask);

            return (ipAdress, hasSubnetMask ? subnetMask.Substring(1) : "");
        }

        /// <summary>
        /// Compares the values of two IP adresses byte by byte.
        /// </summary>
        private int CompareIpValues(IPAddress ip1, IPAddress ip2)
        {
            var ip1Bytes = ip1.GetAddressBytes();
            var ip2Bytes = ip2.GetAddressBytes();

            for (int i = 0; i < ip1Bytes.Length; i++)
            {
                if (ip1Bytes[i] < ip2Bytes[i])
                {
                    return -1;
                }
                if (ip1Bytes[i] > ip2Bytes[i])
                {
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// Compares to strings that may contain subnet masks by their format and value.
        /// </summary>
        private int CompareSubnetMasks(string subnetMask1, string subnetMask2)
        {
            if (subnetMask1.StartsWith(@"\")) subnetMask1 = subnetMask1.Substring(1);
            if (subnetMask2.StartsWith(@"\")) subnetMask2 = subnetMask2.Substring(1);

            if(subnetMask1 != subnetMask2)
            {
                // first without subnet masks
                if (subnetMask1 == "") return -1;
                if (subnetMask2 == "") return 1;

                // then cidr
                int subnet1CIDR;
                int subnet2CIDR;
                bool subnet1IsInt = int.TryParse(subnetMask1, out subnet1CIDR);
                bool subnet2IsInt = int.TryParse(subnetMask2, out subnet2CIDR);
                if (subnet1IsInt && subnet2IsInt)
                {
                    if (subnet1CIDR < subnet2CIDR) return -1;
                    if (subnet1CIDR > subnet2CIDR) return 1;
                }
                if (subnet1IsInt) return -1;
                if (subnet2IsInt) return 1;

                
                IPAddress subnet1IP;
                IPAddress subnet2IP;
                bool subnet1IsIp = IPAddress.TryParse(subnetMask1, out subnet1IP);
                bool subnet2IsIp = IPAddress.TryParse(subnetMask2, out subnet2IP);
                if (subnet1IsIp && subnet2IsIp)
                {
                    // if both in ip format order by value
                    int compareIpValuesResult = CompareIpValues(subnet1IP, subnet2IP);
                    if (compareIpValuesResult != 0) return compareIpValuesResult;                  
                }

                // if one is ip format it should come before the unhandled case
                if (subnet1IsIp) return -1;
                if (subnet2IsIp) return 1;
            }

            // if nothing fits just treat as they were the same
            return 0;
        }

        /// <summary>
        /// Compares two IPAdress objects by their family (IPv4 or IPv6).
        /// </summary>
        private int CompareIpFamilies(IPAddress ip1, IPAddress ip2)
        {
            if (ip1.AddressFamily == AddressFamily.InterNetwork && ip2.AddressFamily == AddressFamily.InterNetworkV6 )
            {
                return -1;
            }
            if (ip1.AddressFamily == AddressFamily.InterNetworkV6 && ip2.AddressFamily == AddressFamily.InterNetwork )
            {
                return 1;
            }

            return 0;
        }
    }
}
