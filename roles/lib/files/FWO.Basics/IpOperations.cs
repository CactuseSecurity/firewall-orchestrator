using System.Net.Sockets;
using System.Net;
using System.Numerics;
using System.Text.RegularExpressions;
using NetTools;

namespace FWO.Basics
{
    public static class IpOperations
    {
        public static bool IsInSubnet(IPAddress address, string cidrString)
        {
            string[] parts = cidrString.Split('/');
            if (parts.Length != 2)
            {
                throw new FormatException("Invalid CIDR format.");
            }

            var networkAddress = IPAddress.Parse(parts[0]);
            int prefixLength = int.Parse(parts[1]);

            if (address.AddressFamily != networkAddress.AddressFamily)
            {
                // The IP versions must match (IPv4 vs IPv6)
                return false;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)  // IPv4
            {
                return IsIPv4InSubnet(address, networkAddress, prefixLength);
            }
            else if (address.AddressFamily == AddressFamily.InterNetworkV6)  // IPv6
            {
                return IsIPv6InSubnet(address, networkAddress, prefixLength);
            }
            else
            {
                throw new NotSupportedException("Only IPv4 and IPv6 are supported.");
            }
        }
        private static bool IsIPv4InSubnet(IPAddress address, IPAddress networkAddress, int prefixLength)
        {
            uint ipAddress = BitConverter.ToUInt32(address.GetAddressBytes().Reverse().ToArray(), 0);
            uint networkIpAddress = BitConverter.ToUInt32(networkAddress.GetAddressBytes().Reverse().ToArray(), 0);

            uint mask = (uint.MaxValue << (32 - prefixLength)) & uint.MaxValue;

            return (ipAddress & mask) == (networkIpAddress & mask);
        }
        private static bool IsIPv6InSubnet(IPAddress address, IPAddress networkAddress, int prefixLength)
        {
            BigInteger ipAddressBigInt = new(address.GetAddressBytes().Reverse().ToArray().Concat(new byte[] { 0 }).ToArray());
            BigInteger networkIpAddressBigInt = new(networkAddress.GetAddressBytes().Reverse().ToArray().Concat(new byte[] { 0 }).ToArray());

            BigInteger mask = BigInteger.Pow(2, 128) - BigInteger.Pow(2, 128 - prefixLength);

            return (ipAddressBigInt & mask) == (networkIpAddressBigInt & mask);
        }

        public static string SanitizeIp(string cidr_str)
        {
            cidr_str = cidr_str.StripOffNetmask();

            if (IPAddress.TryParse(cidr_str, out IPAddress? ip))
            {
                if (ip != null)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        cidr_str = ip.ToString();
                        if (cidr_str.IndexOf('/') < 0) // a single ip without mask
                        {
                            cidr_str += "/128";
                        }
                        if (cidr_str.IndexOf('/') == cidr_str.Length - 1) // wrong format (/ at the end, fixing this by adding 128 mask)
                        {
                            cidr_str += "128";
                        }
                    }
                    else if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        cidr_str = ip.ToString();
                        if (cidr_str.IndexOf('/') < 0) // a single ip without mask
                        {
                            cidr_str += "/32";
                        }
                        if (cidr_str.IndexOf('/') == cidr_str.Length - 1) // wrong format (/ at the end, fixing this by adding 32 mask)
                        {
                            cidr_str += "32";
                        }
                    }
                }
            }
            return cidr_str;
        }
        public static bool OverlapExists(IPAddressRange a, IPAddressRange b)
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
        public static bool CheckOverlap(string ip1, string ip2)
        {
            IPAddressRange range1 = GetIPAdressRange(ip1);
            IPAddressRange range2 = GetIPAdressRange(ip2);

            if (range1.Begin.AddressFamily != range2.Begin.AddressFamily)
                return false;


            return OverlapExists(range1, range2);
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
            IPAddress start;
            IPAddress end;
            if (!IPAddress.TryParse(startIp.StripOffNetmask(), out start))
            {
                throw new ArgumentException($"IP {startIp} is not valid");
            }
            if (!IPAddress.TryParse(endIp.StripOffNetmask(), out end))
            {
                throw new ArgumentException($"IP {endIp} is not valid");
            }
            // Ensure both IPs are of the same address family (both IPv4 or both IPv6)
            if (start.AddressFamily != end.AddressFamily)
            {
                throw new ArgumentException("Start and end IPs must be of the same address family.");
            }

            // Start from the largest possible prefix length and decrease to find the exact network match
            int maxPrefixLength = start.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;

            for (int prefixLength = maxPrefixLength; prefixLength >= 0; prefixLength--)
            {
                // Create a network based on the start IP and current prefix length
                string networkString = $"{start}/{prefixLength}"; // Combine start IP and prefix length into a single string
                IPNetwork network = IPNetwork.Parse(networkString);

                // Check if both start and end IPs are within this exact network
                if (network.Contains(start) && network.Contains(end))
                {
                    // Get subnet mask for IPv4
                    string subnetMask = start.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                        ? GetIPv4SubnetMask(network.PrefixLength)  // Convert PrefixLength to Subnet Mask for IPv4
                        : $"(IPv6) /{network.PrefixLength}"; // IPv6 uses CIDR notation directly

                    string resultingIpString = $"{network.ToString().StripOffNetmask()}/{subnetMask}";
                    return resultingIpString;
                }
            }

            return null; // No exact network match found
        }

        // Convert a prefix length to an IPv4 subnet mask
        private static string GetIPv4SubnetMask(int prefixLength)
        {
            uint mask = 0xffffffff << (32 - prefixLength);
            uint[] bytes = new uint[4];

            bytes[0] = (mask >> 24) & 0xff;
            bytes[1] = (mask >> 16) & 0xff;
            bytes[2] = (mask >> 8) & 0xff;
            bytes[3] = mask & 0xff;

            return string.Join(".", bytes);
        }

    }

}


