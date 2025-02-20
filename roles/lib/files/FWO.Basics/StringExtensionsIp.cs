
using System.Net;
using System.Numerics;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using NetTools;

namespace FWO.Basics
{
    public static partial class StringExtensions
    {
        private const string HtmlTagPattern = "<.*?>";
        private static readonly string[] AllowedTags = ["br?", "i", "hr"];

        [GeneratedRegex(@"(\/[\d\.\:]+)\D?")]
        private static partial Regex NetmaskRegex();


        public static bool TrySplit(this string text, char separator, int index, out string output)
        {
            string[] splits = text.Split(separator);

            output = "";

            if (splits.Length < 2 || splits.Length < index + 1)
                return false;

            output = splits[index];

            return true;
        }

        public static bool TryGetNetmask(this string ip, out string netmask)
        {
            netmask = "";

            Match match = NetmaskRegex().Match(ip);

            if (match.Success)
                netmask = match.Groups[1].Value;

            return match.Success;
        }

        public static bool TrySplit(this string text, char separator, out int length)
        {
            string[] splits = text.Split(separator);

            length = 0;

            if (splits.Length < 2)
                return false;

            length = splits.Length;

            return true;
        }

        public static bool CheckOverlap(this string ip1, string ip2)
        {
            return IpOperations.CheckOverlap(ip1, ip2);
        }

        public static string GetNetmask(this string ip)
        {
            int pos = ip.LastIndexOf('/');
            if (pos > -1 && ip.Length > pos + 1)
            {
                return ip[(pos + 1)..];
            }
            return "";
        }

        public static bool IsV6Address(this string ip)
        {
            return ip.Contains(':');
        }

        public static bool IsV4Address(this string ip)
        {
            return ip.Contains('.');
        }

        public static string StripOffNetmask(this string ip)
        {
            if (ip.TryGetNetmask(out string netmask))
                return ip.Replace(netmask, "");

            return ip;
        }

        public static string StripOffUnnecessaryNetmask(this string ip)
        {
            string netmask = ip.GetNetmask();
            if (ip.IsV4Address() && netmask == "32" || ip.IsV6Address() && netmask == "128")
            {
                return ip.StripOffNetmask();
            }
            return ip;
        }

        private static string BuildDangerousHtmlTagPattern()
        {
            string allowedTags = string.Join('|', AllowedTags);
            return $"<(?!:{allowedTags}).*?(?<!{allowedTags})>";
        }

        public static string StripHtmlTags(this string text, RegexOptions options = RegexOptions.None)
        {
            return Regex.Replace(text, HtmlTagPattern, string.Empty, options);
        }

        public static string StripDangerousHtmlTags(this string text, RegexOptions options = RegexOptions.None)
        {
            string pattern = BuildDangerousHtmlTagPattern();
            return Regex.Replace(text, pattern, string.Empty, options);
        }

        public static bool IsIPv4(this string ipAddress)
        {
            if (IPAddress.TryParse(ipAddress, out IPAddress? addr))
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    return true;

                return false;
            }

            return false;
        }

        public static bool IsIPv6(this string ipAddress)
        {
            if (IPAddress.TryParse(ipAddress, out IPAddress? addr))
            {
                if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                    return true;

                return false;
            }

            return false;
        }

        public static string IpAsCidr(this string ip)
		{
			return IPAddressRange.Parse(ip).ToCidrString();
		}

        public static (string start, string end) CidrToRangeString(this string cidr)
        {
            IPAddress ipStart;
            IPAddress ipEnd;

            (ipStart, ipEnd) = CidrToRange(cidr);

            return (ipStart.ToString(), ipEnd.ToString());
        }

        public static (IPAddress start, IPAddress end) CidrToRange(this string cidr)
        {
            string[] parts = cidr.Split('/');
            if (parts.Length != 2)
            {
                throw new FormatException("Invalid CIDR format.");
            }

            var ip = IPAddress.Parse(parts[0]);
            int prefixLength = int.Parse(parts[1]);

            var addressBytes = ip.GetAddressBytes();

            if (addressBytes.Length == 4)
            {
                // IPv4 case
                return IPv4CidrToRange(addressBytes, prefixLength);
            }
            else if (addressBytes.Length == 16)
            {
                // IPv6 case
                return IPv6CidrToRange(addressBytes, prefixLength);
            }
            else
            {
                throw new FormatException("Invalid IP address format.");
            }
        }

        private static (IPAddress start, IPAddress end) IPv4CidrToRange(byte[] addressBytes, int prefixLength)
        {
            uint ipAddress = BitConverter.ToUInt32([.. addressBytes.Reverse()], 0);
            uint mask = (uint.MaxValue << (32 - prefixLength)) & uint.MaxValue;

            uint startIp = ipAddress & mask;
            uint endIp = startIp | ~mask;

            return (new IPAddress([.. BitConverter.GetBytes(startIp).Reverse()]),
                    new IPAddress([.. BitConverter.GetBytes(endIp).Reverse()]));
        }

        private static (IPAddress start, IPAddress end) IPv6CidrToRange(byte[] addressBytes, int prefixLength)
        {
            if (BitConverter.IsLittleEndian)
            {
                addressBytes = [.. addressBytes.Reverse()];  // Reverse byte array for BigInteger compatibility
            }

            var addressBigInt = new BigInteger(addressBytes.Concat(new byte[] { 0 }).ToArray());  // Treat as unsigned

            var mask = BigInteger.Pow(2, 128) - BigInteger.Pow(2, 128 - prefixLength);  // Compute mask
            var startIpBigInt = addressBigInt & mask;  // Apply mask for the start IP
            var endIpBigInt = startIpBigInt | (~mask & BigInteger.Pow(2, 128) - 1);  // Compute end IP

            var startIpBytes = startIpBigInt.ToByteArray();
            var endIpBytes = endIpBigInt.ToByteArray();

            // Ensure the byte arrays are 16 bytes (128 bits) for IPv6
            startIpBytes = NormalizeBytes(startIpBytes, 16);
            endIpBytes = NormalizeBytes(endIpBytes, 16);

            if (BitConverter.IsLittleEndian)
            {
                startIpBytes = [.. startIpBytes.Reverse()];
                endIpBytes = [.. endIpBytes.Reverse()];
            }

            return (new IPAddress(startIpBytes), new IPAddress(endIpBytes));
        }

        private static byte[] NormalizeBytes(byte[] bytes, int targetLength)
        {
            if (bytes.Length < targetLength)
            {
                // Pad the byte array to the required length
                byte[] padded = new byte[targetLength];
                Array.Copy(bytes, padded, bytes.Length);
                return padded;
            }
            return [.. bytes.Take(targetLength)];  // Ensure it's exactly targetLength bytes
        }

        /// <summary>
        /// Parses a string to an IPAdress object and a string that represents the subnet mask (empty if there is no subnet mask).
        /// </summary>
        public static (IPAddress, string) ToIPAdressAndSubnetMask(this string str)
        {
            IPAddress ipAdress = IPAddress.Parse(str.StripOffNetmask());
            var hasSubnetMask = str.TryGetNetmask(out string subnetMask);

            return (ipAdress, hasSubnetMask ? subnetMask[1..] : "");
        }
    }
}
