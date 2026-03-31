using System.Net;
using System.Net.Sockets;
using System.Numerics;
using DnsClient;
using NetTools;

namespace FWO.Basics
{
    /// <summary>
    /// Provides helper methods for DNS lookups and IP address conversions.
    /// </summary>
    public static class IpOperations
    {
        // Reuse the client to avoid socket churn and disable client-side caching.
        private static readonly LookupClient ReverseLookupClient = new(new LookupClientOptions
        {
            UseCache = false,
            ContinueOnDnsError = true,
            ThrowDnsErrors = false
        });

        /// <summary>
        /// Resolves all PTR records for an IP address.
        /// </summary>
        public static async Task<IReadOnlyList<string>> DnsReverseLookUpAllAsync(
            IPAddress address,
            CancellationToken cancellationToken = default)
        {
            // QueryReverseAsync issues a PTR query and returns all answers from the DNS server.
            IDnsQueryResponse response = await ReverseLookupClient.QueryReverseAsync(address, cancellationToken)
                .ConfigureAwait(false);

            if (response.HasError || response.Answers.Count == 0)
            {
                return Array.Empty<string>();
            }

            return response.Answers
                .PtrRecords()
                .Select(ptr => ptr.PtrDomainName.Value.TrimEnd('.')) // Drop the trailing DNS root dot.
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// Resolves the preferred PTR record for an IP address.
        /// </summary>
        public static async Task<string> DnsReverseLookUpPreferredAsync(IPAddress address)
        {
            IReadOnlyList<string> names = await DnsReverseLookUpAllAsync(address);
            return names.FirstOrDefault(name => !name.StartsWith("lx", StringComparison.OrdinalIgnoreCase))
                ?? names.FirstOrDefault()
                ?? "";
        }

        /// <summary>
        /// Resolves the host name for an IP address.
        /// </summary>
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

        /// <summary>
        /// Resolves the first IPv4 address for a host name.
        /// </summary>
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

        /// <summary>
        /// Converts a single IP, CIDR, or explicit range string into start and end addresses.
        /// </summary>
        public static (string, string) SplitIpToRange(string ipString)
        {
            if (ipString.TryGetNetmask(out _))
            {
                return ipString.CidrToRangeString();
            }

            return TryParseExplicitRange(ipString, out IPAddressRange ipRange)
                ? (ipRange.Begin.ToString(), ipRange.End.ToString())
                : (ipString, ipString);
        }

        /// <summary>
        /// Tries to parse a single IP, CIDR, or range string into string endpoints.
        /// </summary>
        public static bool TryParseIPStringToRange(this string ipString, out (string start, string end) ipRange, bool strictv4Parse = false)
        {
            ipRange = default;
            if (!TryParseAddressPair(ipString, strictv4Parse, out string ipStart, out string ipEnd, out _, out _))
            {
                return false;
            }

            ipRange = (ipStart, ipEnd);
            return true;
        }

        /// <summary>
        /// Tries to parse a single IP, CIDR, or range string into a supported target type.
        /// </summary>
        public static bool TryParseIPString<T>(this string ipString, out T? ipResult, bool strictv4Parse = false)
        {
            ipResult = default;
            if (!TryParseAddressPair(ipString, strictv4Parse, out _, out _, out IPAddress? addressStart, out IPAddress? addressEnd))
            {
                return false;
            }

            object? parsedValue = typeof(T) switch
            {
                var t when t == typeof((string, string)) =>
                    (addressStart!.ToString(), addressEnd!.ToString()),

                var t when t == typeof(IPAddressRange) =>
                    new IPAddressRange(addressStart!, addressEnd!),

                var t when t == typeof((IPAddress, IPAddress)) =>
                    (addressStart!, addressEnd!),

                _ => null
            };

            if (parsedValue is null)
            {
                return false;
            }

            ipResult = (T)parsedValue;
            return true;
        }

        private static bool IsValidIPv4(string ipAddress)
        {
            string[] octets = ipAddress.Split('.');
            return octets.Length == 4 && octets.All(octet => byte.TryParse(octet, out _));
        }

        /// <summary>
        /// Returns the matching object type for one or two IP values.
        /// </summary>
        public static string GetObjectType(string ip1, string ip2)
        {
            ip1 = ip1.StripOffUnnecessaryNetmask();
            ip2 = ip2.StripOffUnnecessaryNetmask();

            if (ip1 == ip2 || ip2 == "")
            {
                return ip1.TryGetNetmask(out _) ? ObjectType.Network : ObjectType.Host;
            }

            return SpanSingleNetwork(ip1, ip2) ? ObjectType.Network : ObjectType.IPRange;
        }

        private static bool SpanSingleNetwork(string ipStart, string ipEnd)
        {
            IPAddressRange range = IPAddressRange.Parse(ipStart.StripOffNetmask() + "-" + ipEnd.StripOffNetmask());
            return HasValidNetmask(range);
        }

        private static bool HasValidNetmask(IPAddressRange range)
        {
            // Adapted from IPAddressRange.getPrefixLength() without exception handling.
            byte[] addressBytes = range.Begin.GetAddressBytes();
            if (range.Begin.Equals(range.End))
            {
                return true;
            }

            int num = addressBytes.Length * 8;
            for (int i = 0; i < num; i++)
            {
                byte[] bitMask = Bits.GetBitMask(addressBytes.Length, i);
                if (new IPAddress(Bits.And(addressBytes, bitMask)).Equals(range.Begin) &&
                    new IPAddress(Bits.Or(addressBytes, Bits.Not(bitMask))).Equals(range.End))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether two IP ranges overlap.
        /// </summary>
        /// <param name="a">The first IP range.</param>
        /// <param name="b">The second IP range.</param>
        /// <returns><c>true</c> when the ranges overlap, otherwise <c>false</c>.</returns>
        public static bool RangeOverlapExists(IPAddressRange a, IPAddressRange b)
        {
            return IpToUint(a.Begin) <= IpToUint(b.End) && IpToUint(b.Begin) <= IpToUint(a.End);
        }

        /// <summary>
        /// Calculates the intersection of two IP address ranges.
        /// </summary>
        /// <param name="a">The first IP address range.</param>
        /// <param name="b">The second IP address range.</param>
        /// <returns>
        /// A new <see cref="IPAddressRange"/> representing the overlapping range,
        /// or <c>null</c> if the ranges do not overlap.
        /// </returns>
        public static IPAddressRange? GetIntersection(IPAddressRange a, IPAddressRange b)
        {
            BigInteger startA = ToBigInteger(a.Begin);
            BigInteger endA = ToBigInteger(a.End);
            BigInteger startB = ToBigInteger(b.Begin);
            BigInteger endB = ToBigInteger(b.End);

            BigInteger startOverlap = BigInteger.Max(startA, startB);
            BigInteger endOverlap = BigInteger.Min(endA, endB);

            if (startOverlap <= endOverlap)
            {
                return new IPAddressRange(
                    FromBigInteger(startOverlap, a.Begin.AddressFamily),
                    FromBigInteger(endOverlap, a.Begin.AddressFamily)
                );
            }

            return null;
        }

        /// <summary>
        /// Converts an IPv4 address to an unsigned integer.
        /// </summary>
        public static uint IpToUint(IPAddress ipAddress)
        {
            byte[] bytes = ipAddress.GetAddressBytes();

            // Convert from network byte order to host byte order.
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt32(bytes, 0);
        }

        /// <summary>
        /// Converts an unsigned integer to an IPv4 address.
        /// </summary>
        public static IPAddress UintToIp(uint ipAddress)
        {
            byte[] bytes = BitConverter.GetBytes(ipAddress);

            // Convert from host byte order to network byte order.
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return new IPAddress(bytes);
        }

        /// <summary>
        /// Checks whether two single IP, CIDR, or range strings overlap.
        /// </summary>
        public static bool CheckOverlap(string ip1, string ip2)
        {
            IPAddressRange range1 = GetIPAdressRange(ip1);
            IPAddressRange range2 = GetIPAdressRange(ip2);

            if (range1.Begin.AddressFamily != range2.Begin.AddressFamily)
            {
                return false;
            }

            return RangeOverlapExists(range1, range2);
        }

        /// <summary>
        /// Converts a single IP, CIDR, or range string into an <see cref="IPAddressRange"/>.
        /// </summary>
        public static IPAddressRange GetIPAdressRange(string ip)
        {
            (string start, string end) = SplitIpToRange(ip);
            return new IPAddressRange(IPAddress.Parse(start), IPAddress.Parse(end));
        }

        /// <summary>
        /// Converts start and end addresses to dotted mask notation for the matching network.
        /// </summary>
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

            // Ensure both IPs are in the same address family.
            if (start.AddressFamily != end.AddressFamily)
            {
                throw new ArgumentException("Start and end IPs must be of the same address family.");
            }

            // Walk from the most specific prefix down to the least specific prefix.
            int maxPrefixLength = start.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;

            for (int prefixLength = maxPrefixLength; prefixLength >= 0; prefixLength--)
            {
                IPNetwork network = IPNetwork.Parse($"{start}/{prefixLength}");
                if (network.Contains(start) && network.Contains(end))
                {
                    string subnetMask = start.AddressFamily == AddressFamily.InterNetwork
                        ? GetIPv4SubnetMask(network.PrefixLength)
                        : $"(IPv6) /{network.PrefixLength}";

                    return $"{network.ToString().StripOffNetmask()}/{subnetMask}";
                }
            }

            return "";
        }

        // Convert a prefix length to an IPv4 subnet mask.
        private static string GetIPv4SubnetMask(int prefixLength)
        {
            uint mask = 0xffffffff << (32 - prefixLength);
            uint[] bytes = [(mask >> 24) & 0xff, (mask >> 16) & 0xff, (mask >> 8) & 0xff, mask & 0xff];
            return string.Join(".", bytes);
        }

        /// <summary>
        /// Compares two IP addresses byte by byte.
        /// </summary>
        public static int CompareIpValues(IPAddress ip1, IPAddress ip2)
        {
            byte[] ip1Bytes = ip1.GetAddressBytes();
            byte[] ip2Bytes = ip2.GetAddressBytes();

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
        /// Compares two IP address objects by address family.
        /// </summary>
        public static int CompareIpFamilies(IPAddress ip1, IPAddress ip2)
        {
            return (ip1.AddressFamily, ip2.AddressFamily) switch
            {
                (AddressFamily.InterNetwork, AddressFamily.InterNetworkV6) => -1,
                (AddressFamily.InterNetworkV6, AddressFamily.InterNetwork) => 1,
                _ => 0
            };
        }

        /// <summary>
        /// Subtracts one or more ranges from a source range.
        /// </summary>
        public static List<IPAddressRange> Subtract(this IPAddressRange source, List<IPAddressRange> subtractor)
        {
            return ToNetworks([source]).Subtract(ToNetworks(subtractor)).ToMergedRanges();
        }

        /// <summary>
        /// Subtracts each network in <paramref name="subtract"/> from all source networks.
        /// </summary>
        public static IEnumerable<IPNetwork2> Subtract(
            this IEnumerable<IPNetwork2> source,
            IEnumerable<IPNetwork2> subtract)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(subtract);

            List<IPNetwork2> result = source.ToList();
            foreach (IPNetwork2 sub in subtract)
            {
                // Subtract each network from the current result set.
                result = result
                    .SelectMany(network => network - sub) // Use IPNetwork2's subtraction operator.
                    .ToList();
            }

            return result;
        }

        /// <summary>
        /// Merges overlapping or adjacent networks into address ranges.
        /// </summary>
        public static List<IPAddressRange> ToMergedRanges(this IEnumerable<IPNetwork2> networks, bool includeNetworkAndBroadcast = true)
        {
            List<IPNetwork2> networkList = networks.ToList();
            if (networkList.Count == 0)
            {
                return [];
            }

            // Convert to inclusive [start, end] intervals.
            List<(IPAddress start, IPAddress end)> intervals = networkList.Select(network =>
            {
                IPAddress start = includeNetworkAndBroadcast ? network.Network : network.FirstUsable ?? network.Network;
                IPAddress end = includeNetworkAndBroadcast ? network.Broadcast ?? start : network.LastUsable ?? start;
                return (start, end);
            }).ToList();

            // Ensure all intervals use the same address family.
            AddressFamily addressFamily = intervals[0].start.AddressFamily;
            if (intervals.Any(interval => interval.start.AddressFamily != addressFamily || interval.end.AddressFamily != addressFamily))
            {
                throw new InvalidOperationException("Mixed address families (IPv4/IPv6) are not supported.");
            }

            intervals.Sort((left, right) => CompareIpValues(left.start, right.start));

            List<(IPAddress start, IPAddress end)> merged = new();
            (IPAddress start, IPAddress end) currentInterval = intervals[0];

            foreach ((IPAddress start, IPAddress end) in intervals.Skip(1))
            {
                if (CompareIpValues(start, AddIp(currentInterval.end, 1)) <= 0)
                {
                    if (CompareIpValues(end, currentInterval.end) > 0)
                    {
                        currentInterval.end = end;
                    }
                }
                else
                {
                    merged.Add(currentInterval);
                    currentInterval = (start, end);
                }
            }

            merged.Add(currentInterval);
            return [.. merged.Select(interval => new IPAddressRange(interval.start, interval.end))];
        }

        private static IPAddress AddIp(IPAddress ip, long delta)
        {
            BigInteger value = ToBigInteger(ip) + new BigInteger(delta);
            if (value < BigInteger.Zero)
            {
                value = BigInteger.Zero;
            }

            AddressFamily family = ip.AddressFamily;
            BigInteger max = MaxValue(family);
            if (value > max)
            {
                value = max;
            }

            return FromBigInteger(value, family);
        }

        public static BigInteger ToBigInteger(IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes(); // Big-endian.
            byte[] littleEndian = [.. bytes.Reverse(), 0]; // Little-endian plus unsigned padding.
            return new BigInteger(littleEndian);
        }

        private static IPAddress FromBigInteger(BigInteger value, AddressFamily family)
        {
            int length = family == AddressFamily.InterNetwork ? 4 : 16;
            byte[] bytesLittleEndian = value.ToByteArray(); // Little-endian.
            byte[] bytesBigEndian = new byte[length];

            for (int i = 0; i < length; i++)
            {
                bytesBigEndian[length - 1 - i] = i < bytesLittleEndian.Length ? bytesLittleEndian[i] : (byte)0;
            }

            return new IPAddress(bytesBigEndian);
        }

        private static BigInteger MaxValue(AddressFamily family)
        {
            int bits = family == AddressFamily.InterNetwork ? 32 : 128;
            return (BigInteger.One << bits) - 1;
        }

        private static bool TryParseExplicitRange(string ipString, out IPAddressRange ipRange)
        {
            ipRange = default!;
            return ipString.TrySplit('-', 1, out _) && IPAddressRange.TryParse(ipString, out ipRange);
        }

        private static bool TryParseAddressPair(
            string ipString,
            bool strictv4Parse,
            out string ipStart,
            out string ipEnd,
            out IPAddress? addressStart,
            out IPAddress? addressEnd)
        {
            ipStart = ipEnd = "";
            addressStart = addressEnd = null;

            try
            {
                (ipStart, ipEnd) = SplitIpToRange(ipString);
                return IPAddress.TryParse(ipStart, out addressStart)
                    && IPAddress.TryParse(ipEnd, out addressEnd)
                    && !HasStrictIPv4ParseError(ipStart, ipEnd, addressStart, addressEnd, strictv4Parse);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool HasStrictIPv4ParseError(
            string ipStart,
            string ipEnd,
            IPAddress addressStart,
            IPAddress addressEnd,
            bool strictv4Parse)
        {
            return strictv4Parse
                && addressStart.AddressFamily == AddressFamily.InterNetwork
                && addressEnd.AddressFamily == AddressFamily.InterNetwork
                && (!IsValidIPv4(ipStart) || !IsValidIPv4(ipEnd));
        }

        private static List<IPNetwork2> ToNetworks(IEnumerable<IPAddressRange> ranges)
        {
            List<IPNetwork2> networks = new();
            foreach (IPAddressRange range in ranges)
            {
                if (range.Begin.Equals(range.End))
                {
                    int mask = range.Begin.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
                    networks.Add(IPNetwork2.Parse(range.ToString(), (byte)mask));
                }
                else if (IPNetwork2.TryParseRange(range.ToString(), out IEnumerable<IPNetwork2>? parsedRanges))
                {
                    networks.AddRange(parsedRanges);
                }
            }

            return networks;
        }
    }
}
