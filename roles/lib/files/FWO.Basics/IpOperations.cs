using System.Net.Sockets;
using System.Net;
using NetTools;

namespace FWO.Basics
{
    public static class IpOperations
    {
        public static async Task<string> DnsReverseLookUp(IPAddress address)
        {
            try
            {
                return (await Dns.GetHostEntryAsync(address)).HostName;
            }
            catch(Exception)
            {
                return "";
            }
        }

        public static (string, string) SplitIpToRange(string ipString)
        {
            string ipStart;
            string ipEnd;
            if (ipString.TryGetNetmask(out _))
            {
                (ipStart, ipEnd) = ipString.CidrToRangeString();
            }
            else if (ipString.TrySplit('-', 1, out _) && IPAddressRange.TryParse(ipString, out IPAddressRange ipRange))
            {
                ipStart = ipRange.Begin.ToString();
                ipEnd = ipRange.End.ToString();
            }
            else
            {
                ipStart = ipString;
                ipEnd = ipString;
            }
            return (ipStart, ipEnd);
        }

        public static string GetObjectType(string ip1, string ip2)
        {
            ip1 = ip1.StripOffUnnecessaryNetmask();
            ip2 = ip2.StripOffUnnecessaryNetmask();
            if (ip1 == ip2 || ip2 == "")
            {
                if (ip1.TryGetNetmask(out _))
                {
                    return ObjectType.Network;
                }
                return ObjectType.Host;
            }
            if (SpanSingleNetwork(ip1, ip2))
            {
                return ObjectType.Network;
            }
            return ObjectType.IPRange;
        }

        private static bool SpanSingleNetwork(string ipStart, string ipEnd)
        {
            IPAddressRange range = IPAddressRange.Parse(ipStart.StripOffNetmask() + "-" + ipEnd.StripOffNetmask());
            return HasValidNetmask(range);
        }

        private static bool HasValidNetmask(IPAddressRange range)
        {
            // code adapted (without exception) from IPAddressRange.getPrefixLength()
            byte[] addressBytes = range.Begin.GetAddressBytes();
            if (range.Begin.Equals(range.End))
            {
                return true;
            }

            int num = addressBytes.Length * 8;
            for (int i = 0; i < num; i++)
            {
                byte[] bitMask = Bits.GetBitMask(addressBytes.Length, i);
                if (new IPAddress(Bits.And(addressBytes, bitMask)).Equals(range.Begin) && new IPAddress(Bits.Or(addressBytes, Bits.Not(bitMask))).Equals(range.End))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if IP range a and b overlap.
        /// </summary>
        /// <param name="a">First IP range</param>
        /// <param name="b">Second IP range</param>
        /// <returns>True, if IP ranges overlap, false otherwise.</returns>
        public static bool RangeOverlapExists(IPAddressRange a, IPAddressRange b)
        {
            return IpToUint(a.Begin) <= IpToUint(b.End) && IpToUint(b.Begin) <= IpToUint(a.End);
        }

        public static uint IpToUint(IPAddress ipAddress)
        {
            byte[] bytes = ipAddress.GetAddressBytes();

            // flip big-endian(network order) to little-endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToUInt32(bytes, 0);
        }

        public static IPAddress UintToIp(uint ipAddress)
        {
            byte[] bytes = BitConverter.GetBytes(ipAddress);

            // flip big-endian(network order) to little-endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return new IPAddress(bytes);
        }

        public static bool CheckOverlap(string ip1, string ip2)
        {
            IPAddressRange range1 = GetIPAdressRange(ip1);
            IPAddressRange range2 = GetIPAdressRange(ip2);

            if (range1.Begin.AddressFamily != range2.Begin.AddressFamily)
                return false;

            return RangeOverlapExists(range1, range2);
        }

        public static IPAddressRange GetIPAdressRange(string ip)
        {
            IPAddressRange ipAddressRange;

            if (ip.TryGetNetmask(out _))
            {
                (string Start, string End) = ip.CidrToRangeString();
                ipAddressRange = new(IPAddress.Parse(Start), IPAddress.Parse(End));
            }
            else if (ip.TrySplit('-', 1, out _) && IPAddressRange.TryParse(ip, out IPAddressRange ipRange))
            {
                ipAddressRange = ipRange;
            }
            else
            {
                ipAddressRange = new IPAddressRange(IPAddress.Parse(ip), IPAddress.Parse(ip));
            }

            return ipAddressRange;
        }

        public static string ToDotNotation(string startIp, string endIp)
        {
            if (!IPAddress.TryParse(startIp.StripOffNetmask(), out IPAddress? start))
            {
                throw new ArgumentException($"IP {startIp} is not valid");
            }
            if (!IPAddress.TryParse(endIp.StripOffNetmask(), out IPAddress? end))
            {
                throw new ArgumentException($"IP {endIp} is not valid");
            }
            // Ensure both IPs are of the same address family (both IPv4 or both IPv6)
            if (start.AddressFamily != end.AddressFamily)
            {
                throw new ArgumentException("Start and end IPs must be of the same address family.");
            }

            // Start from the largest possible prefix length and decrease to find the exact network match
            int maxPrefixLength = start.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;

            for (int prefixLength = maxPrefixLength; prefixLength >= 0; prefixLength--)
            {
                // Create a network based on the start IP and current prefix length
                string networkString = $"{start}/{prefixLength}"; // Combine start IP and prefix length into a single string
                IPNetwork network = IPNetwork.Parse(networkString);

                // Check if both start and end IPs are within this exact network
                if (network.Contains(start) && network.Contains(end))
                {
                    // Get subnet mask for IPv4
                    string subnetMask = start.AddressFamily == AddressFamily.InterNetwork
                        ? GetIPv4SubnetMask(network.PrefixLength)  // Convert PrefixLength to Subnet Mask for IPv4
                        : $"(IPv6) /{network.PrefixLength}"; // IPv6 uses CIDR notation directly

                    return $"{network.ToString().StripOffNetmask()}/{subnetMask}";
                }
            }
            return ""; // No exact network match found
        }

        // Convert a prefix length to an IPv4 subnet mask
        private static string GetIPv4SubnetMask(int prefixLength)
        {
            uint mask = 0xffffffff << (32 - prefixLength);
            uint[] bytes = [(mask >> 24) & 0xff, (mask >> 16) & 0xff, (mask >> 8) & 0xff, mask & 0xff];
            return string.Join(".", bytes);
        }

        /// <summary>
        /// Compares the values of two IP adresses byte by byte.
        /// </summary>
        public static int CompareIpValues(IPAddress ip1, IPAddress ip2)
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
        /// Compares two IPAdress objects by their family (IPv4 or IPv6).
        /// </summary>
        public static int CompareIpFamilies(IPAddress ip1, IPAddress ip2)
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
