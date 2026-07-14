using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using FWO.Basics;
using FWO.Basics.Comparer;

namespace FWO.Data.Networking
{
    /// <summary>
    /// Analyzes normalized network objects for IPv4 range and prefix-based checks.
    /// </summary>
    public class NetworkObjectRangeAnalyzer
    {
        private readonly Dictionary<string, IPAddress?> _parseCache = new();

        /// <summary>
        /// Creates range analyses for a set of network objects.
        /// </summary>
        public List<NetworkObjectRangeAnalysis> AnalyzeMany(IEnumerable<NetworkObject> objects)
        {
            return objects
                .Where(obj => obj.Type.Name != ObjectType.Group)
                .Select(Analyze)
                .ToList();
        }

        /// <summary>
        /// Creates a range analysis for a single normalized network object.
        /// </summary>
        public NetworkObjectRangeAnalysis Analyze(NetworkObject networkObject)
        {
            IPAddress? start = ParseAndCache(networkObject.IP);
            IPAddress? end = ParseAndCache(networkObject.IpEnd);
            bool isSupported = start != null && start.AddressFamily == AddressFamily.InterNetwork;
            bool isIpv4 = isSupported && (end == null || end.AddressFamily == AddressFamily.InterNetwork);

            if (!isIpv4)
            {
                return new()
                {
                    NetworkObject = networkObject,
                    Start = start,
                    End = end,
                    IsSupported = false,
                    IsIpv4 = false,
                    PrefixLength = -1
                };
            }

            return new()
            {
                NetworkObject = networkObject,
                Start = start,
                End = end,
                IsSupported = true,
                IsIpv4 = true,
                PrefixLength = CommonPrefixLength(start, end)
            };
        }

        /// <summary>
        /// Checks whether all supported IPv4 objects contain the given IPv4 address and meet the minimum prefix length.
        /// </summary>
        public bool MatchesIpFilter(IPAddress ipAddress, int minPrefix, IEnumerable<NetworkObject> objects)
        {
            List<NetworkObjectRangeAnalysis> analyses = AnalyzeMany(objects);
            if (analyses.Count == 0)
            {
                return false;
            }

            return analyses.All(analysis =>
                analysis.IsSupported
                && analysis.PrefixLength >= minPrefix
                && IsIpInRange(ipAddress, analysis.Start, analysis.End));
        }

        /// <summary>
        /// Checks whether any supported IPv4 object is broader than the supplied minimum prefix length.
        /// </summary>
        public bool ExceedsPrefixThreshold(int minPrefix, IEnumerable<NetworkObject> objects)
        {
            return AnalyzeMany(objects)
                .Any(analysis => analysis.IsSupported && analysis.PrefixLength < minPrefix);
        }

        private IPAddress? ParseAndCache(string? ipString)
        {
            if (string.IsNullOrWhiteSpace(ipString))
            {
                return null;
            }

            string sanitized = SanitizeIpString(ipString);
            if (_parseCache.TryGetValue(sanitized, out IPAddress? cached))
            {
                return cached;
            }

            IPAddress.TryParse(sanitized, out IPAddress? parsed);
            _parseCache[sanitized] = parsed;

            return parsed;
        }

        private static string SanitizeIpString(string? ipString)
        {
            if (string.IsNullOrWhiteSpace(ipString))
            {
                return string.Empty;
            }

            ReadOnlySpan<char> trimmed = ipString.AsSpan().Trim();
            int slashIndex = trimmed.IndexOf('/');

            return (slashIndex >= 0 ? trimmed[..slashIndex] : trimmed).Trim().ToString();
        }

        private static bool IsIpInRange(IPAddress ip, IPAddress? startIp, IPAddress? endIp)
        {
            if (startIp == null)
            {
                return false;
            }

            if (endIp == null)
            {
                return startIp.Equals(ip);
            }

            IPAdressComparer comparer = new();
            if (comparer.Compare(startIp, endIp) > 0)
            {
                (startIp, endIp) = (endIp, startIp);
            }

            return comparer.Compare(startIp, ip) <= 0 &&
                   comparer.Compare(endIp, ip) >= 0;
        }

        private static int CommonPrefixLength(IPAddress? ipA, IPAddress? ipB)
        {
            if (ipA == null)
            {
                return -1;
            }

            if (ipB == null)
            {
                return 32;
            }

            uint a = BinaryPrimitives.ReadUInt32BigEndian(ipA.GetAddressBytes());
            uint b = BinaryPrimitives.ReadUInt32BigEndian(ipB.GetAddressBytes());
            uint diff = a ^ b;

            return diff == 0 ? 32 : BitOperations.LeadingZeroCount(diff);
        }
    }

    /// <summary>
    /// Stores the analyzed IPv4 range characteristics of a normalized network object.
    /// </summary>
    public class NetworkObjectRangeAnalysis
    {
        public NetworkObject NetworkObject { get; set; } = new();
        public IPAddress? Start { get; set; }
        public IPAddress? End { get; set; }
        public bool IsSupported { get; set; }
        public bool IsIpv4 { get; set; }
        public int PrefixLength { get; set; }
    }
}
