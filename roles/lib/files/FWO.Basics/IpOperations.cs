using System.Net.Sockets;
using System.Net;
using NetTools;
using System;
using System.Numerics;

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
            catch (Exception)
            {
                return "";
            }
        }

        public static async Task<string> DnsLookUp(string hostname)
        {
            try
            {
                return (await Dns.GetHostAddressesAsync(hostname))
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?
                    .ToString() ?? "";
            }
            catch (Exception)
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

        public static bool TryParseIPStringToRange(this string ipString, out (string Start, string End) ipRange, bool strictv4Parse = false)
        {
            ipRange = default;

            try
            {
                (string ipStart, string ipEnd) = SplitIpToRange(ipString);

                bool ipStartOK = IPAddress.TryParse(ipStart, out IPAddress? ipAdressStart);
                bool ipEndOK = IPAddress.TryParse(ipEnd, out IPAddress? ipAdressEnd);

                if (ipAdressStart is null || ipAdressEnd is null)
                {
                    return false;
                }

                if (strictv4Parse && ipAdressStart?.AddressFamily == AddressFamily.InterNetwork && ipAdressEnd?.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (!IsValidIPv4(ipStart) || !IsValidIPv4(ipEnd))
                    {
                        return false;
                    }
                }

                if (!ipStartOK || !ipEndOK)
                {
                    return false;
                }

                if (!IPAddress.TryParse(ipStart, out _) || !IPAddress.TryParse(ipEnd, out _))
                {
                    return false;
                }

                ipRange.Start = ipStart;
                ipRange.End = ipEnd;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryParseIPString<T>(this string ipString, out T? ipResult, bool strictv4Parse = false)
        {
            ipResult = default;

            try
            {
                (string ipStart, string ipEnd) = SplitIpToRange(ipString);

                bool ipStartOK = IPAddress.TryParse(ipStart, out IPAddress? ipAdressStart);
                bool ipEndOK = IPAddress.TryParse(ipEnd, out IPAddress? ipAdressEnd);

                if (ipAdressStart is null || ipAdressEnd is null)
                {
                    return false;
                }

                if (strictv4Parse && ipAdressStart?.AddressFamily == AddressFamily.InterNetwork && ipAdressEnd?.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (!IsValidIPv4(ipStart) || !IsValidIPv4(ipEnd))
                    {
                        return false;
                    }
                }

                if (!ipStartOK || !ipEndOK)
                {
                    return false;
                }

                if (typeof(T) == typeof((string, string)))
                {
                    ipResult = (T)Convert.ChangeType((ipAdressStart!.ToString(), ipAdressEnd!.ToString()), typeof(T));
                    return true;
                }
                else if (typeof(T) == typeof(IPAddressRange) && IPAddressRange.TryParse(ipString, out IPAddressRange ipRange))
                {
                    ipResult = (T)Convert.ChangeType(ipRange, typeof(T));
                    return true;
                }
                else if (typeof(T) == typeof((IPAddress, IPAddress)))
                {
                    Tuple<IPAddress, IPAddress>? ipTuple = new(ipAdressStart!, ipAdressEnd!);
                    ipResult = (T)Convert.ChangeType(ipTuple, typeof(T));
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsValidIPv4(string ipAddress)
        {
            byte[] addBytes = [.. ipAddress.Split('.').Where(_ => byte.Parse(_) <= 255 && byte.Parse(_) >= 0).Select(byte.Parse)];

            return addBytes.Length == 4;
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
            if (ip1.AddressFamily == AddressFamily.InterNetwork && ip2.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return -1;
            }
            if (ip1.AddressFamily == AddressFamily.InterNetworkV6 && ip2.AddressFamily == AddressFamily.InterNetwork)
            {
                return 1;
            }

            return 0;
        }

        public static List<IPAddressRange> Subtract(this IPAddressRange source, List<IPAddressRange> subtractor)
        {
            List<IPNetwork2> sourceNetwork = new();
            List<IPNetwork2> subtractorNetwork = new();

            if (source.Begin.ToString().Equals(source.End.ToString()))
            {
                sourceNetwork.Add(IPNetwork2.Parse(source.ToString(), 32));
            }
            else if (IPNetwork2.TryParseRange(source.ToString(), out IEnumerable<IPNetwork2> parsedSourceRange))
            {
                sourceNetwork.AddRange(parsedSourceRange);
            }
             
            foreach (IPAddressRange range in subtractor)
            {
                if (range.Begin.ToString().Equals(range.End.ToString()))
                {
                    subtractorNetwork.Add(IPNetwork2.Parse(range.ToString(), 32));
                }
                else if (IPNetwork2.TryParseRange(range.ToString(), out IEnumerable<IPNetwork2> parsedSubtractorRange))
                {
                    subtractorNetwork.AddRange(parsedSubtractorRange);
                }
            }

            List<IPNetwork2> result = sourceNetwork.Subtract(subtractorNetwork).ToList();
            List<IPAddressRange> mergedRanges = result.ToMergedRanges();

            return mergedRanges;
        }

        public static IEnumerable<IPNetwork2> Subtract(
            this IEnumerable<IPNetwork2> source,
            IEnumerable<IPNetwork2> subtract)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (subtract == null) throw new ArgumentNullException(nameof(subtract));

            var result = source.ToList();

            foreach (var sub in subtract)
            {
                // jedes Netz aus 'subtract' von allen bisherigen abziehen
                result = result
                    .SelectMany(n => n - sub) // nutzt IPNetwork2's Operator -
                    .ToList();
            }

            return result;
        }
        
        public static List<IPAddressRange> ToMergedRanges(this IEnumerable<IPNetwork2> networks, bool includeNetworkAndBroadcast = true)
        {
            var list = networks?.ToList() ?? new List<IPNetwork2>();
            if (list.Count == 0) return new List<IPAddressRange>();

            // 1) in [start,end] (inklusive) umwandeln
            var intervals = list.Select(n =>
            {
                var start = includeNetworkAndBroadcast ? n.Network : n.FirstUsable;
                var end   = includeNetworkAndBroadcast ? n.Broadcast : n.LastUsable;
                return (start, end);
            }).ToList();

            // Sicherheit: alles gleicher AddressFamily?
            var af = intervals[0].start.AddressFamily;
            if (intervals.Any(t => t.start.AddressFamily != af || t.end.AddressFamily != af))
                throw new InvalidOperationException("Gemischte AddressFamily (IPv4/IPv6) in den Netzen.");

            // 2) sortieren
            intervals.Sort((a, b) => CompareIpValues(a.start, b.start));

            // 3) zusammenführen (merge), wenn überlappend ODER direkt benachbart
            var merged = new List<(IPAddress start, IPAddress end)>();
            (IPAddress s, IPAddress e) cur = intervals[0];

            foreach (var (s, e) in intervals.Skip(1))
            {
                // Wenn s <= cur.e + 1  => zusammenlegen
                var nextToCurEndPlusOne = CompareIpValues(s, AddIp(cur.e, 1)) <= 0;
                if (nextToCurEndPlusOne)
                {
                    if (CompareIpValues(e, cur.e) > 0) cur.e = e;
                }
                else
                {
                    merged.Add(cur);
                    cur = (s, e);
                }
            }
            merged.Add(cur);

            // 4) in IPAddressRange (inklusive) umwandeln
            return merged.Select(t => new IPAddressRange(t.start, t.end)).ToList();
        }

        private static IPAddress AddIp(IPAddress ip, long delta)
        {
            var bi = ToBigInteger(ip) + new BigInteger(delta);
            if (bi < BigInteger.Zero) bi = BigInteger.Zero;
            var family = ip.AddressFamily;
            var max = MaxValue(family);
            if (bi > max) bi = max;
            return FromBigInteger(bi, family);
        }

        private static BigInteger ToBigInteger(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes(); // big-endian
            var le = bytes.Reverse().Concat(new byte[] { 0 }).ToArray(); // little-endian + unsignd pad
            return new BigInteger(le);
        }

        private static IPAddress FromBigInteger(BigInteger value, AddressFamily family)
        {
            int len = family == AddressFamily.InterNetwork ? 4 : 16;
            var bytesLE = value.ToByteArray(); // little-endian
            var bytesBE = new byte[len];
            for (int i = 0; i < len; i++)
            {
                var src = i < bytesLE.Length ? bytesLE[i] : (byte)0;
                bytesBE[len - 1 - i] = src;
            }
            return new IPAddress(bytesBE);
        }

        private static BigInteger MaxValue(AddressFamily family)
        {
            int bits = family == AddressFamily.InterNetwork ? 32 : 128;
            return (BigInteger.One << bits) - 1;
        }

    }
}
