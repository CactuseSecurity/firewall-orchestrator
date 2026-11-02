using System.Net;
using System.Numerics;
using System.Net.Sockets;
using System.Globalization;
using DnsClient;
using NetTools;

namespace FWO.Basics
{
    /// <summary>
    /// Provides helper methods for DNS lookups and IP address conversions.
    /// </summary>
    public static class IpOperations
    {
        private const int kIpv4OctetCount = 4;
        private const int kMaxIpv4OctetLength = 3;
        private const int kIpv4CompatiblePrefixByteCount = 12;
        private const int kBitsPerByte = 8;
        private const uint kIpv6LoopbackSuffix = 1;

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

            return ExtractPtrNames(response);
        }

        /// <summary>
        /// Resolves all PTR records for an IP address and tells a definitive answer from a failed lookup,
        /// so that a caller storing the result can retry the failed ones later.
        /// </summary>
        /// <returns>The PTR names, an empty list if the DNS server answered that the address has no name,
        /// or null if no definitive answer was received (e.g. server failure or refused query).</returns>
        /// <exception cref="DnsResponseException">If none of the name servers could be reached.</exception>
        public static async Task<IReadOnlyList<string>?> TryDnsReverseLookUpAllAsync(
            IPAddress address,
            CancellationToken cancellationToken = default)
        {
            IDnsQueryResponse response = await ReverseLookupClient.QueryReverseAsync(address, cancellationToken)
                .ConfigureAwait(false);
            return SelectReverseLookupNames(response.Header.ResponseCode, ExtractPtrNames(response));
        }

        /// <summary>
        /// Decides whether a reverse lookup response is definitive. Only a response without error and a
        /// non-existent domain answer whether the address has a name; every other response code may change
        /// with the next query.
        /// </summary>
        /// <returns>The PTR names, an empty list for a non-existent domain, or null for any other response code.</returns>
        public static IReadOnlyList<string>? SelectReverseLookupNames(DnsHeaderResponseCode responseCode, IReadOnlyList<string> ptrNames)
        {
            return responseCode switch
            {
                DnsHeaderResponseCode.NoError => ptrNames,
                DnsHeaderResponseCode.NotExistentDomain => Array.Empty<string>(),
                _ => null
            };
        }

        private static string[] ExtractPtrNames(IDnsQueryResponse response)
        {
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

        /// <summary>
        /// Tries to parse a start address and an optional end address into one range, IPv4 or IPv6. Start and end may
        /// each be a single IP, a CIDR or a range string; the range reaches from the first address of the start to the
        /// last address of the end. So a network stored with netmask on both, e.g. "10.0.0.0/24" to "10.0.0.0/24", keeps
        /// its size, and host notations such as "10.0.0.1/32" to "10.0.0.5/32" give the range between them. Fails for a
        /// missing start, an unparsable address, a reversed range and a range mixing IPv4 and IPv6.
        /// </summary>
        /// <param name="start">Start address, CIDR or range.</param>
        /// <param name="end">End address of a range; empty for a single address or network.</param>
        /// <param name="range">The parsed range, or null if parsing failed.</param>
        public static bool TryParseIPRange(string? start, string? end, out IPAddressRange? range)
        {
            range = null;
            if (string.IsNullOrWhiteSpace(start) || !start.TryParseIPString(out IPAddressRange? startRange) || startRange == null)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(end))
            {
                range = startRange;
                return true;
            }
            if (!end.TryParseIPString(out IPAddressRange? endRange) || endRange == null)
            {
                return false;
            }
            try
            {
                range = new IPAddressRange(startRange.Begin, endRange.End);
                return true;
            }
            catch (ArgumentException)
            {
                // a reversed range or a range mixing IPv4 and IPv6
                return false;
            }
        }

        /// <summary>
        /// Checks whether an IPv4 value uses four unambiguous decimal octets.
        /// </summary>
        private static bool IsValidIPv4(string ipAddress)
        {
            string[] octets = ipAddress.Split('.');
            // Leading zeros are rejected because .NET interprets them as octal while FWO uses decimal addresses.
            return octets.Length == kIpv4OctetCount
                && octets.All(octet => octet.Length is > 0 and <= kMaxIpv4OctetLength
                    && octet.All(char.IsAsciiDigit)
                    && (octet.Length == 1 || octet[0] != '0'));
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
            return TryGetCidrNotation(range.Begin, range.End, out _);
        }

        /// <summary>
        /// Checks whether two IP ranges overlap.
        /// </summary>
        /// <param name="a">The first IP range.</param>
        /// <param name="b">The second IP range.</param>
        /// <returns><c>true</c> when the ranges overlap, otherwise <c>false</c>.</returns>
        public static bool RangeOverlapExists(IPAddressRange a, IPAddressRange b)
        {
            if (a.Begin.AddressFamily != b.Begin.AddressFamily)
            {
                return false;
            }

            return CompareIpValues(a.Begin, b.End) <= 0 && CompareIpValues(b.Begin, a.End) <= 0;
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
            if (a.Begin.AddressFamily != b.Begin.AddressFamily)
            {
                return null;
            }

            BigInteger startA = ToBigInteger(a.Begin);
            BigInteger endA = ToBigInteger(a.End);
            BigInteger startB = ToBigInteger(b.Begin);
            BigInteger endB = ToBigInteger(b.End);

            BigInteger startOverlap = BigInteger.Max(startA, startB);
            BigInteger endOverlap = BigInteger.Min(endA, endB);

            if (startOverlap <= endOverlap)
            {
                return new IPAddressRange(
                    IPNetwork2.ToIPAddress(startOverlap, a.Begin.AddressFamily),
                    IPNetwork2.ToIPAddress(endOverlap, a.Begin.AddressFamily)
                );
            }

            return null;
        }

        /// <summary>
        /// Converts an IPv4 address to an unsigned integer.
        /// </summary>
        public static uint IpToUint(IPAddress ipAddress)
        {
            return (uint)IPNetwork2.ToBigInteger(ipAddress);
        }

        /// <summary>
        /// Converts an unsigned integer to an IPv4 address.
        /// </summary>
        public static IPAddress UintToIp(uint ipAddress)
        {
            return IPNetwork2.ToIPAddress(ipAddress, AddressFamily.InterNetwork);
        }

        /// <summary>
        /// Converts a numeric address value back into an address of the given family.
        /// </summary>
        /// <param name="ipValue">Numeric address value.</param>
        /// <param name="addressFamily">Address family the value belongs to.</param>
        public static IPAddress FromBigInteger(BigInteger ipValue, AddressFamily addressFamily)
        {
            return IPNetwork2.ToIPAddress(ipValue, addressFamily);
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
        /// Converts start and end addresses to the shortest matching notation: a plain IP for a single address,
        /// CIDR notation when the range spans exactly one network, and <c>start-end</c> for any other range.
        /// </summary>
        /// <param name="startIp">The first address of the range, with or without netmask.</param>
        /// <param name="endIp">The last address of the range, with or without netmask. May be empty for a single address.</param>
        /// <returns>The compact notation, or the unchanged <paramref name="startIp"/> when the input is not parsable.</returns>
        public static string ToCompactNotation(string startIp, string endIp)
        {
            (string start, string end) = string.IsNullOrEmpty(endIp)
                ? SplitIpToRange(startIp)
                : (startIp.StripOffNetmask(), endIp.StripOffNetmask());

            if (!IPAddress.TryParse(start, out IPAddress? startAddress)
                || !IPAddress.TryParse(end, out IPAddress? endAddress)
                || startAddress.AddressFamily != endAddress.AddressFamily)
            {
                return startIp;
            }

            if (startAddress.Equals(endAddress))
            {
                return startAddress.ToString();
            }

            return TryGetCidrNotation(startAddress, endAddress, out string cidrNotation)
                ? cidrNotation
                : $"{startAddress}-{endAddress}";
        }

        private static bool TryGetCidrNotation(IPAddress start, IPAddress end, out string cidrNotation)
        {
            try
            {
                cidrNotation = new IPAddressRange(start, end).ToCidrString();
                return !string.IsNullOrEmpty(cidrNotation);
            }
            catch (ArgumentException)
            {
                cidrNotation = string.Empty;
                return false;
            }
            catch (FormatException)
            {
                cidrNotation = string.Empty;
                return false;
            }
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

            IPNetwork2 network = IPNetwork2.WideSubnet(start.ToString(), end.ToString());
            string subnetMask = start.AddressFamily == AddressFamily.InterNetwork
                ? network.Netmask.ToString()
                : $"(IPv6) /{network.Cidr}";
            return $"{network.Network}/{subnetMask}";
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

            return IPNetwork2.ToIPAddress(value, family);
        }

        public static BigInteger ToBigInteger(IPAddress ip)
        {
            return IPNetwork2.ToBigInteger(ip);
        }

        private static BigInteger MaxValue(AddressFamily family)
        {
            int bits = family == AddressFamily.InterNetwork ? 32 : 128;
            return IPNetwork2.ToUint((byte)bits, family);
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
                if (strictv4Parse && !HasStrictIpParseFormat(ipString))
                {
                    return false;
                }

                (ipStart, ipEnd) = SplitIpToRange(ipString);
                return IPAddress.TryParse(ipStart, out addressStart)
                    && IPAddress.TryParse(ipEnd, out addressEnd);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks every address in a single address, CIDR, or explicit range for strict notation.
        /// </summary>
        private static bool HasStrictIpParseFormat(string ipString)
        {
            string[] rangeParts = ipString.Split('-');
            return rangeParts.Length switch
            {
                1 => HasStrictAddressFormat(rangeParts[0]),
                2 => rangeParts.All(part => HasStrictAddressFormat(part.Trim())),
                _ => false
            };
        }

        /// <summary>
        /// Checks one address, with an optional CIDR prefix, without allowing framework-specific aliases.
        /// </summary>
        private static bool HasStrictAddressFormat(string value)
        {
            string[] parts = value.Split('/');
            if (parts.Length > 2
                || !IPAddress.TryParse(parts[0], out IPAddress? address)
                || !HasStrictAddressNotation(parts[0], address))
            {
                return false;
            }

            if (parts.Length == 1)
            {
                return true;
            }

            int maxPrefixLength = address.GetAddressBytes().Length * kBitsPerByte;
            return int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int prefixLength)
                && prefixLength <= maxPrefixLength;
        }

        /// <summary>
        /// Checks that an address uses unambiguous dotted-decimal IPv4 or a supported IPv6 representation.
        /// </summary>
        private static bool HasStrictAddressNotation(string value, IPAddress address)
        {
            return address.AddressFamily switch
            {
                AddressFamily.InterNetwork => IsValidIPv4(value),
                AddressFamily.InterNetworkV6 => value == value.Trim()
                    && !value.Contains('%')
                    && !value.Contains('[')
                    && !value.Contains(']')
                    && !value.Contains('.')
                    && !IsIpv4EncodedAsIpv6(address),
                _ => false
            };
        }

        /// <summary>
        /// Checks whether an IPv6 value represents an IPv4 address in mapped or compatible notation.
        /// </summary>
        private static bool IsIpv4EncodedAsIpv6(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                return true;
            }

            byte[] addressBytes = address.GetAddressBytes();
            if (addressBytes.Take(kIpv4CompatiblePrefixByteCount).Any(addressByte => addressByte != 0))
            {
                return false;
            }

            uint embeddedIpv4 = 0;
            foreach (byte addressByte in addressBytes.Skip(kIpv4CompatiblePrefixByteCount))
            {
                embeddedIpv4 = (embeddedIpv4 << kBitsPerByte) | addressByte;
            }

            // The unspecified address and loopback share the zero prefix but are native IPv6 addresses.
            return embeddedIpv4 > kIpv6LoopbackSuffix;
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
