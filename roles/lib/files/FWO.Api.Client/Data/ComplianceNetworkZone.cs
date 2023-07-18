using FWO.Api.Client;
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
        public IPAddressRange[] IPRanges { get; set; } = new IPAddressRange[0];

        [JsonProperty("super_network_zone"), JsonPropertyName("super_network_zone")]
        public ComplianceNetworkZone? Superzone { get; set; } = null;

        [JsonProperty("sub_network_zones"), JsonPropertyName("sub_network_zones")]
        public ComplianceNetworkZone[] Subzones { get; set; } = new ComplianceNetworkZone[0];

        [JsonProperty("network_zone_communication_sources", ItemConverterType = typeof(WrapperConverter<ComplianceNetworkZone>),
            ItemConverterParameters = new object[] { "from_network_zone" }), JsonPropertyName("network_zone_communication_sources")]
        public ComplianceNetworkZone[] AllowedCommunicationSources { get; set; } = new ComplianceNetworkZone[0];

        [JsonProperty("network_zone_communication_destinations", ItemConverterType = typeof(WrapperConverter<ComplianceNetworkZone>),
            ItemConverterParameters = new object[] { "to_network_zone" }), JsonPropertyName("network_zone_communication_destinations")]
        public ComplianceNetworkZone[] AllowedCommunicationDestinations { get; set; } = new ComplianceNetworkZone[0];


        public bool CommunicationAllowedFrom(ComplianceNetworkZone from)
        {
            return AllowedCommunicationSources.Contains(from);
        }

        public bool CommunicationAllowedTo(ComplianceNetworkZone to)
        {
            return AllowedCommunicationDestinations.Contains(to);
        }

        public bool OverlapExists(List<IPAddressRange> ranges)
        {
            foreach (IPAddressRange zoneIpRange in IPRanges)
            {
                foreach (IPAddressRange ipRange in ranges)
                {
                    if (OverlapExists(zoneIpRange, ipRange))
                    {
                        return true;
                    }
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
        private bool OverlapExists(IPAddressRange a, IPAddressRange b)
        {
            return IpToUint(a.Begin) <= IpToUint(b.End) && IpToUint(b.Begin) <= IpToUint(a.End);
        }

        private uint IpToUint(IPAddress ipAddress)
        {
            byte[] bytes = ipAddress.GetAddressBytes();

            // flip big-endian(network order) to little-endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt32(bytes, 0);
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
