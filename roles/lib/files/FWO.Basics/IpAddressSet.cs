using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace FWO.Basics
{
    /// <summary>Inclusive interval of IP addresses of one address family.</summary>
    /// <param name="Family">Address family of start and end.</param>
    /// <param name="Start">First address as number.</param>
    /// <param name="End">Last address as number.</param>
    public readonly record struct IpInterval(AddressFamily Family, BigInteger Start, BigInteger End);

    /// <summary>
    /// Set of IP addresses, kept as sorted and merged intervals per address family. Compares and combines address sets
    /// independent of their notation, e.g. a range written as "10.0.0.0-10.0.0.17" and the CIDR blocks 10.0.0.0/28 and
    /// 10.0.0.16/31 are the same set.
    /// </summary>
    public sealed class IpAddressSet
    {
        private const int kIpv4Bits = 32;
        private const int kIpv6Bits = 128;
        private const char kRangeSeparator = '-';

        private readonly List<IpInterval> intervals = [];

        /// <summary>The merged intervals of the set, IPv4 before IPv6, each family in ascending order.</summary>
        public IReadOnlyList<IpInterval> Intervals => intervals;

        /// <summary>Whether the set holds no address.</summary>
        public bool IsEmpty => intervals.Count == 0;

        /// <summary>Creates a set of the given intervals.</summary>
        public static IpAddressSet Of(IEnumerable<IpInterval> addresses)
        {
            IpAddressSet set = new();
            foreach (IpInterval interval in addresses)
            {
                set.Add(interval);
            }
            return set;
        }

        /// <summary>
        /// Parses a single address, a CIDR block or a range "start-end", optionally with a separate end address (whose
        /// netmask, if any, extends it to the end of its network).
        /// </summary>
        /// <returns>False if the addresses cannot be parsed, belong to different families or the end lies before the start.</returns>
        public static bool TryParseInterval(string? ip, string? ipEnd, out IpInterval interval)
        {
            interval = default;
            if (string.IsNullOrWhiteSpace(ip))
            {
                return false;
            }
            try
            {
                (string start, string end) = IpOperations.SplitIpToRange(ip.Trim());
                if (!string.IsNullOrWhiteSpace(ipEnd))
                {
                    (_, end) = IpOperations.SplitIpToRange(ipEnd.Trim());
                }
                if (!IPAddress.TryParse(start, out IPAddress? startAddress) || !IPAddress.TryParse(end, out IPAddress? endAddress)
                    || startAddress.AddressFamily != endAddress.AddressFamily)
                {
                    return false;
                }
                BigInteger startValue = IpOperations.ToBigInteger(startAddress);
                BigInteger endValue = IpOperations.ToBigInteger(endAddress);
                if (endValue < startValue)
                {
                    return false;
                }
                interval = new IpInterval(startAddress.AddressFamily, startValue, endValue);
                return true;
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                return false;
            }
        }

        /// <summary>Parses a single address, a CIDR block or a range "start-end".</summary>
        public static bool TryParseInterval(string? notation, out IpInterval interval) => TryParseInterval(notation, null, out interval);

        /// <summary>
        /// Writes an interval as CIDR block if it is one ("10.0.0.5/32", "10.0.1.0/24"), else as range "start-end".
        /// </summary>
        public static string Format(IpInterval interval)
        {
            IPAddress start = ToAddress(interval.Start, interval.Family);
            BigInteger size = interval.End - interval.Start + 1;
            if (size.IsPowerOfTwo && (interval.Start % size).IsZero)
            {
                int prefixLength = Bits(interval.Family) - (int)size.GetBitLength() + 1;
                return $"{start}/{prefixLength}";
            }
            return $"{start}{kRangeSeparator}{ToAddress(interval.End, interval.Family)}";
        }

        /// <summary>Adds the addresses of an interval.</summary>
        public void Add(IpInterval interval)
        {
            List<IpInterval> touching = [.. intervals.Where(existing => existing.Family == interval.Family
                && existing.Start <= interval.End + 1 && interval.Start <= existing.End + 1)];
            BigInteger start = touching.Select(existing => existing.Start).Append(interval.Start).Min();
            BigInteger end = touching.Select(existing => existing.End).Append(interval.End).Max();
            intervals.RemoveAll(touching.Contains);
            intervals.Add(new IpInterval(interval.Family, start, end));
            Sort();
        }

        /// <summary>Adds all addresses of another set.</summary>
        public void Add(IpAddressSet other)
        {
            foreach (IpInterval interval in other.intervals)
            {
                Add(interval);
            }
        }

        /// <summary>Removes the addresses of an interval.</summary>
        public void Remove(IpInterval interval)
        {
            List<IpInterval> remaining = [];
            foreach (IpInterval existing in intervals)
            {
                if (existing.Family != interval.Family || existing.End < interval.Start || interval.End < existing.Start)
                {
                    remaining.Add(existing);
                    continue;
                }
                if (existing.Start < interval.Start)
                {
                    remaining.Add(existing with { End = interval.Start - 1 });
                }
                if (interval.End < existing.End)
                {
                    remaining.Add(existing with { Start = interval.End + 1 });
                }
            }
            intervals.Clear();
            intervals.AddRange(remaining);
            Sort();
        }

        /// <summary>Removes all addresses of another set.</summary>
        public void Remove(IpAddressSet other)
        {
            foreach (IpInterval interval in other.intervals)
            {
                Remove(interval);
            }
        }

        /// <summary>Whether all addresses of an interval belong to the set.</summary>
        public bool Contains(IpInterval interval)
        {
            return intervals.Exists(existing => existing.Family == interval.Family && existing.Start <= interval.Start && interval.End <= existing.End);
        }

        /// <summary>Whether an interval and the set have an address in common.</summary>
        public bool Overlaps(IpInterval interval)
        {
            return intervals.Exists(existing => existing.Family == interval.Family && existing.Start <= interval.End && interval.Start <= existing.End);
        }

        /// <summary>Whether both sets hold the same addresses.</summary>
        public bool SetEquals(IpAddressSet other) => intervals.SequenceEqual(other.intervals);

        /// <summary>Returns a copy of the set.</summary>
        public IpAddressSet Copy() => Of(intervals);

        private void Sort()
        {
            intervals.Sort((left, right) => left.Family != right.Family
                ? left.Family.CompareTo(right.Family)
                : left.Start.CompareTo(right.Start));
        }

        private static int Bits(AddressFamily family) => family == AddressFamily.InterNetworkV6 ? kIpv6Bits : kIpv4Bits;

        private static IPAddress ToAddress(BigInteger value, AddressFamily family) => IpOperations.FromBigInteger(value, family);
    }
}
