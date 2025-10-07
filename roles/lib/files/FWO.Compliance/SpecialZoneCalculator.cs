using System.Net;
using FWO.Basics;
using FWO.Basics.Comparer;
using FWO.Data;
using NetTools;

namespace FWO.Compliance
{
    public class SpecialZoneCalculator
    {
        private ComplianceNetworkZone _networkZone;

        public SpecialZoneCalculator(ComplianceNetworkZone networkZone)
        {
            _networkZone = networkZone;
        }

        public void CalculateInternetZone(List<ComplianceNetworkZone> excludedZones)
        {
            List<IPAddressRange> excludedZonesIPRanges = new();
            List<IPAddressRange> internetZoneIPRanges = new();

            // Gather ip ranges from excluded network zone list

            foreach (ComplianceNetworkZone networkZone in excludedZones)
            {
                if (networkZone.IPRanges != null)
                {
                    excludedZonesIPRanges.AddRange(networkZone.IPRanges);
                }
            }

            // Sort

            excludedZonesIPRanges.Sort(new IPAddressRangeComparer());

            // Get full IPv4

            IPAddressRange fullRange = IPAddressRange.Parse("0.0.0.0/0");

            IPAddress current = fullRange.Begin;

            // Gather gaps

            foreach (var range in excludedZonesIPRanges)
            {
                if (IpOperations.CompareIpValues(current, range.Begin) < 0)
                {
                    IPAddress prev = IpOperations.Decrement(range.Begin);
                    internetZoneIPRanges.Add(new IPAddressRange(current, prev));
                }

                current = IpOperations.Increment(range.End);
            }

            // Include top end if undefined

            if (IpOperations.CompareIpValues(current, fullRange.End) <= 0)
            {
                internetZoneIPRanges.Add(new IPAddressRange(current, fullRange.End));
            }

            // Assign new internet zone ranges to zone object
            
            _networkZone.IPRanges = internetZoneIPRanges.ToArray();
        }

    }

}