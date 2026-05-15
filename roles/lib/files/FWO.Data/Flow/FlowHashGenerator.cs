using System.Security.Cryptography;
using System.Text;

namespace FWO.Data.Flow
{
    /// <summary>
    /// Generates deterministic or random hashes for flow objects.
    /// - Uses SHA256 for technical values (IP ranges, protocol+ports, timestamps)
    /// - Uses UUID v4 for null/dynamic values
    /// </summary>
    public static class FlowHashGenerator
    {
        /// <summary>
        /// Generates hash for network objects from IP range.
        /// SHA256 of "ipstart-ipend" if both present, UUID v4 if either is null (FQDN/dynamic objects).
        /// </summary>
        public static string GenerateNwObjectHash(string? ipStart, string? ipEnd)
        {
            if (string.IsNullOrWhiteSpace(ipStart) || string.IsNullOrWhiteSpace(ipEnd))
            {
                // FQDN or dynamic object - use UUID for uniqueness
                return Guid.NewGuid().ToString("N"); // 32 hex chars, no dashes
            }

            // Deterministic hash from IP range
            string input = $"{ipStart}-{ipEnd}";
            return ComputeSha256(input);
        }

        /// <summary>
        /// Generates hash for service objects from protocol and port range.
        /// SHA256 of "proto_id-port_start-port_end" if ports present,
        /// UUID v4 if either port is null (protocol-only objects like ICMP).
        /// </summary>
        public static string GenerateSvcObjectHash(int protoId, int? portStart, int? portEnd)
        {
            if (!portStart.HasValue || !portEnd.HasValue)
            {
                // Protocol-only object (e.g. ICMP) - use UUID for uniqueness
                return Guid.NewGuid().ToString("N"); // 32 hex chars, no dashes
            }

            // Deterministic hash from protocol and port range
            string input = $"{protoId}-{portStart}-{portEnd}";
            return ComputeSha256(input);
        }

        /// <summary>
        /// Generates hash for time objects from time range.
        /// SHA256 of "start_time-end_time" if both present, UUID v4 if either is null.
        /// </summary>
        public static string GenerateTimeObjectHash(DateTime? startTime, DateTime? endTime)
        {
            if (!startTime.HasValue || !endTime.HasValue)
            {
                // Dynamic time object - use UUID for uniqueness
                return Guid.NewGuid().ToString("N"); // 32 hex chars, no dashes
            }

            // Deterministic hash from time range (using ISO 8601 format for reproducibility)
            string input = $"{startTime:O}-{endTime:O}";
            return ComputeSha256(input);
        }

        /// <summary>
        /// Generates single access hash from source, destination, and service hashes.
        /// SHA256 of concatenated hashes ensures deduplication across equivalent access triples.
        /// </summary>
        public static string GenerateAccessHash(
            IEnumerable<string> sourceHashes,
            IEnumerable<string> destinationHashes,
            IEnumerable<string> serviceHashes)
        {
            if (!sourceHashes.Any() || !destinationHashes.Any() || !serviceHashes.Any())
            {
                throw new ArgumentException("Access must have at least one source, destination, and service hash");
            }

            // Sort hashes to ensure same access (even if in different order) produces same hash
            var sortedSources = sourceHashes.OrderBy(h => h).ToList();
            var sortedDestinations = destinationHashes.OrderBy(h => h).ToList();
            var sortedServices = serviceHashes.OrderBy(h => h).ToList();

            // Concatenate in deterministic order
            string combined = string.Concat(
                string.Join("|", sortedSources),
                ":::",
                string.Join("|", sortedDestinations),
                ":::",
                string.Join("|", sortedServices)
            );

            return ComputeSha256(combined);
        }

        public static string GenerateGroupHash(IEnumerable<string> memberHashes)
        {
            // We don't allow empty flow groups
            if (!memberHashes.Any())
            {
                throw new ArgumentException("Group must have at least one member hash");
            }
            // Sort member hashes to ensure same group (even if members in different order) produces same hash
            var sortedMembers = memberHashes.OrderBy(h => h).ToList();
            string combined = string.Join("|", sortedMembers);
            return ComputeSha256(combined);
        }

        /// <summary>
        /// Computes SHA256 hash of input string, returns 64-character hex string.
        /// </summary>
        private static string ComputeSha256(string input)
        {
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
