using FWO.Basics;
using NetTools;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class ComplianceNetworkZone
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; } = -1;

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("id_string"), JsonPropertyName("id_string")]
        public string IdString { get; set; } = "";

        [JsonProperty("description"), JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonProperty("ip_ranges", ItemConverterType = typeof(IpAddressRangeJsonTypeConverter)), JsonPropertyName("ip_ranges")]
        public IPAddressRange[] IPRanges { get; set; } = [];

        [JsonProperty("super_network_zone"), JsonPropertyName("super_network_zone")]
        public ComplianceNetworkZone? Superzone { get; set; } = null;

        [JsonProperty("sub_network_zones"), JsonPropertyName("sub_network_zones")]
        public ComplianceNetworkZone[] Subzones { get; set; } = [];

        [JsonProperty("network_zone_communication_sources", ItemConverterType = typeof(WrapperConverter<ComplianceNetworkZone>),
            ItemConverterParameters = ["from_network_zone"]), JsonPropertyName("network_zone_communication_sources")]
        public ComplianceNetworkZone[] AllowedCommunicationSources { get; set; } = [];

        [JsonProperty("network_zone_communication_destinations", ItemConverterType = typeof(WrapperConverter<ComplianceNetworkZone>),
            ItemConverterParameters = ["to_network_zone"]), JsonPropertyName("network_zone_communication_destinations")]
        public ComplianceNetworkZone[] AllowedCommunicationDestinations { get; set; } = [];

        [JsonProperty("created"), JsonPropertyName("created")]
        public DateTime Created { get; set; }

        [JsonProperty("removed"), JsonPropertyName("removed")]
        public DateTime? Removed { get; set; }

        [JsonProperty("criterion_id"), JsonPropertyName("criterion_id")]
        public int CriterionId { get; set; } = 0;

        [JsonProperty("is_auto_calculated_internet_zone"), JsonPropertyName("is_auto_calculated_internet_zone")]
        public bool IsAutoCalculatedInternetZone { get; set; } = false;

        [JsonProperty("is_auto_calculated_undefined_internal_zone"), JsonPropertyName("is_auto_calculated_undefined_internal_zone")]
        public bool IsAutoCalculatedUndefinedInternalZone { get; set; } = false;

        public bool CommunicationAllowedFrom(ComplianceNetworkZone from)
        {
            return AllowedCommunicationSources.Contains(from);
        }

        public bool CommunicationAllowedTo(ComplianceNetworkZone to)
        {
            return AllowedCommunicationDestinations.Contains(to);
        }

        public bool OverlapExists(List<IPAddressRange> ipRanges, List<List<IPAddressRange>> unseenIpRanges)
        {
            bool result = false;

            for (int i = 0; i < IPRanges.Length; i++)
            {
                for (int j = 0; j < ipRanges.Count; j++)
                {
                    if (IpOperations.RangeOverlapExists(IPRanges[i], ipRanges[j]))
                    {
                        result = true;
                        RemoveOverlap(unseenIpRanges[j], IPRanges[i]);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Subtracts a range from a list of ranges. The arithmetic is done in 128-bit space so that
        /// IPv4 and IPv6 zone ranges are handled identically.
        /// </summary>
        /// <param name="ranges">Ranges the overlap is removed from; modified in place.</param>
        /// <param name="toRemove">Range to subtract.</param>
        private static void RemoveOverlap(List<IPAddressRange> ranges, IPAddressRange toRemove)
        {
            BigInteger removeBegin = IpOperations.ToBigInteger(toRemove.Begin);
            BigInteger removeEnd = IpOperations.ToBigInteger(toRemove.End);

            int index = 0;
            while (index < ranges.Count)
            {
                index += IpOperations.RangeOverlapExists(ranges[index], toRemove)
                    ? SubtractOverlap(ranges, index, removeBegin, removeEnd)
                    : 1;
            }
        }

        /// <summary>
        /// Subtracts an overlapping range from the entry at the given index.
        /// </summary>
        /// <param name="ranges">Ranges being reduced; modified in place.</param>
        /// <param name="index">Index of the overlapping entry.</param>
        /// <param name="removeBegin">First address of the subtracted range.</param>
        /// <param name="removeEnd">Last address of the subtracted range.</param>
        /// <returns>Number of entries the caller has to advance past after the subtraction.</returns>
        private static int SubtractOverlap(List<IPAddressRange> ranges, int index, BigInteger removeBegin, BigInteger removeEnd)
        {
            AddressFamily addressFamily = ranges[index].Begin.AddressFamily;
            BigInteger rangeBegin = IpOperations.ToBigInteger(ranges[index].Begin);
            BigInteger rangeEnd = IpOperations.ToBigInteger(ranges[index].End);

            if (removeBegin <= rangeBegin && removeEnd >= rangeEnd)
            {
                // Complete overlap, remove the entire range; the next entry moves into this index
                ranges.RemoveAt(index);
                return 0;
            }

            if (removeBegin <= rangeBegin)
            {
                // Overlap on the left side, update the start
                ranges[index].Begin = IpOperations.FromBigInteger(removeEnd + 1, addressFamily);
                return 1;
            }

            if (removeEnd >= rangeEnd)
            {
                // Overlap on the right side, update the end
                ranges[index].End = IpOperations.FromBigInteger(removeBegin - 1, addressFamily);
                return 1;
            }

            // Overlap in the middle, split the range
            // begin..remove.begin-1
            IPAddress end = ranges[index].End;
            ranges[index].End = IpOperations.FromBigInteger(removeBegin - 1, addressFamily);
            // remove.end+1..end
            ranges.Insert(index, new IPAddressRange(IpOperations.FromBigInteger(removeEnd + 1, addressFamily), end));
            return 2;
        }

        public object Clone()
        {
            IPAddressRange[] ipRangesClone = new IPAddressRange[IPRanges.Length];
            for (int i = 0; i < IPRanges.Length; i++)
            {
                ipRangesClone[i] = new IPAddressRange(IPRanges[i].Begin, IPRanges[i].End);
            }

            return new ComplianceNetworkZone()
            {
                Id = Id,
                Superzone = (ComplianceNetworkZone?)Superzone?.Clone(),
                Name = Name,
                Description = Description,
                IPRanges = ipRangesClone,
                CriterionId = CriterionId,
                IsAutoCalculatedInternetZone = IsAutoCalculatedInternetZone,
                IsAutoCalculatedUndefinedInternalZone = IsAutoCalculatedUndefinedInternalZone,
                Subzones = CloneArray(Subzones),
                AllowedCommunicationSources = CloneArray(AllowedCommunicationSources),
                AllowedCommunicationDestinations = CloneArray(AllowedCommunicationDestinations)
            };
        }

        private static ComplianceNetworkZone[] CloneArray(ComplianceNetworkZone[] array)
        {
            ComplianceNetworkZone[] arrayClone = new ComplianceNetworkZone[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                arrayClone[i] = (ComplianceNetworkZone)array[i].Clone();
            }
            return arrayClone;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            return ((ComplianceNetworkZone)obj).Id == Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }
    }
}
