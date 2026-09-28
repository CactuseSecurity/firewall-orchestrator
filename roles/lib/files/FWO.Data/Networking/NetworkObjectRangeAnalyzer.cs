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
            return AnalyzeLazy(objects).ToList();
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
        /// Evaluates whether the supported IPv4 objects comply with the minimum prefix length and contain the given address.
        /// A prefix violation takes precedence over an address match. Unsupported objects are ignored.
        /// </summary>
        public IpFilterEvaluation EvaluateIpFilter(IPAddress ipAddress, int minPrefix,
            IEnumerable<NetworkObject> objects)
        {
            return EvaluateField(ipAddress, minPrefix, objects);
        }

        /// <summary>
        /// Checks whether every supported IPv4 object meets the supplied minimum prefix length.
        /// </summary>
        public bool MeetsMinimumPrefix(int minPrefix, IEnumerable<NetworkObject> objects)
        {
            return EvaluateField(null, minPrefix, objects) != IpFilterEvaluation.PrefixViolation;
        }

        private IpFilterEvaluation EvaluateField(IPAddress? ipAddress, int minPrefix,
            IEnumerable<NetworkObject> objects)
        {
            bool containsIp = false;

            foreach (NetworkObjectRangeAnalysis analysis in AnalyzeLazy(objects))
            {
                if (!analysis.IsSupported)
                {
                    continue;
                }

                if (analysis.PrefixLength < minPrefix)
                {
                    return IpFilterEvaluation.PrefixViolation;
                }

                if (ipAddress is not null && IsIpInRange(ipAddress, analysis.Start, analysis.End))
                {
                    containsIp = true;
                }
            }

            return containsIp ? IpFilterEvaluation.Match : IpFilterEvaluation.NoIpMatch;
        }

        private IEnumerable<NetworkObjectRangeAnalysis> AnalyzeLazy(IEnumerable<NetworkObject> objects)
        {
            return objects
                .Where(obj => obj.Type.Name != ObjectType.Group)
                .Select(Analyze);
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
    /// Describes the result of applying an IP and minimum-prefix filter to one rule field.
    /// </summary>
    public enum IpFilterEvaluation
    {
        /// <summary>
        /// All supported objects meet the prefix requirement, but none contains the requested IP address.
        /// </summary>
        NoIpMatch,

        /// <summary>
        /// All supported objects meet the prefix requirement and at least one contains the requested IP address.
        /// </summary>
        Match,

        /// <summary>
        /// At least one supported object is broader than the minimum prefix length.
        /// </summary>
        PrefixViolation
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
