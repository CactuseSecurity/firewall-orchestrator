using System.Net;
using FWO.Basics;
using FWO.Basics.Comparer;
using FWO.Data;
using NetTools;

namespace FWO.Compliance
{
    public static class SpecialZone
    {
        public static void CalculateInternetZone(List<ComplianceNetworkZone> excludedZones, ComplianceNetworkZone internetZone)
        {
            IPAddressRange fullRangeIPv4 = IPAddressRange.Parse("0.0.0.0/0");

            List<IPAddressRange> excludedZonesIPRanges = excludedZones.ParseToListOfRanges(true);
            List<IPAddressRange> internetZoneIPRanges = fullRangeIPv4.Subtract(excludedZonesIPRanges);

            internetZone.IPRanges = internetZoneIPRanges.ToArray();
        }

        private static List<IPAddressRange> ParseToListOfRanges(this List<ComplianceNetworkZone> networkZones, bool sort)
        {
            List<IPAddressRange> listOfRanges = new();

            // Gather ip ranges from excluded network zone list

            foreach (ComplianceNetworkZone networkZone in networkZones)
            {
                if (networkZone.IPRanges != null)
                {
                    listOfRanges.AddRange(networkZone.IPRanges);
                }
            }

            // Sort

            if (sort)
            {
                listOfRanges.Sort(new IPAddressRangeComparer());
            }

            return listOfRanges;
        }

    }

}