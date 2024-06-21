﻿using FWO.Api.Client;
using NetTools;
using Newtonsoft.Json;
using System.Net;
using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class ComplianceNetworkZone
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; } = -1;

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

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
                    if (OverlapExists(IPRanges[i], ipRanges[j]))
                    {
                        result = true;
                        RemoveOverlap(unseenIpRanges[j], IPRanges[i]);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Checks if IP range a and b overlap.
        /// </summary>
        /// <param name="a">First IP range</param>
        /// <param name="b">Second IP range</param>
        /// <returns>True, if IP ranges overlap, false otherwise.</returns>
        public static bool OverlapExists(IPAddressRange a, IPAddressRange b)
        {
            return IpToUint(a.Begin) <= IpToUint(b.End) && IpToUint(b.Begin) <= IpToUint(a.End);
        }

        private static void RemoveOverlap(List<IPAddressRange> ranges, IPAddressRange toRemove)
        {
            for (int i = 0; i < ranges.Count; i++)
            {
                if (OverlapExists(ranges[i], toRemove))
                {
                    if (IpToUint(toRemove.Begin) <= IpToUint(ranges[i].Begin) && IpToUint(toRemove.End) >= IpToUint(ranges[i].End))
                    {
                        // Complete overlap, remove the entire range
                        ranges.RemoveAt(i);
                        i--;
                    }
                    else if (IpToUint(toRemove.Begin) <= IpToUint(ranges[i].Begin))
                    {
                        // Overlap on the left side, update the start
                        ranges[i].Begin = UintToIp(IpToUint(toRemove.End) + 1);
                    }
                    else if (IpToUint(toRemove.End) >= IpToUint(ranges[i].End))
                    {
                        // Overlap on the right side, update the end
                        ranges[i].End = UintToIp(IpToUint(toRemove.Begin) - 1);
                    }
                    else
                    {
                        // Overlap in the middle, split the range
                        // begin..remove.begin-1
                        IPAddress end = ranges[i].End;
                        ranges[i].End = UintToIp(IpToUint(toRemove.Begin) - 1);
                        // remove.end+1..end
                        ranges.Insert(i, new IPAddressRange(UintToIp(IpToUint(toRemove.End) + 1), end));
                        i++;
                    }
                }
            }
        }

        private static uint IpToUint(IPAddress ipAddress)
        {
            byte[] bytes = ipAddress.GetAddressBytes();

            // flip big-endian(network order) to little-endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt32(bytes, 0);
        }

        private static IPAddress UintToIp(uint ipAddress)
        {
            byte[] bytes = BitConverter.GetBytes(ipAddress);

            // flip big-endian(network order) to little-endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return new IPAddress(bytes);
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
