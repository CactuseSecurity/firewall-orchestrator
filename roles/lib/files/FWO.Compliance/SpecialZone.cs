using System.Net;
using FWO.Basics;
using FWO.Basics.Comparer;
using FWO.Data;
using NetTools;

namespace FWO.Compliance
{
    public static class SpecialZone
    {
        public static void CalculateInternetZone(List<ComplianceNetworkZone> excludedZones, ComplianceNetworkZone networkZone)
        {
            List<IPAddressRange> excludedZonesIPRanges = new();
            List<IPAddressRange> internetZoneIPRanges = new();

            // Gather ip ranges from excluded network zone list

            foreach (ComplianceNetworkZone excludedZone in excludedZones)
            {
                if (excludedZone.IPRanges != null)
                {
                    excludedZonesIPRanges.AddRange(excludedZone.IPRanges);
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
            
            networkZone.IPRanges = internetZoneIPRanges.ToArray();
        }

    }

}