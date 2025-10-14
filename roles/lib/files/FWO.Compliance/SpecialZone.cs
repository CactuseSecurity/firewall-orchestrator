using System.Net;
using FWO.Basics;
using FWO.Basics.Comparer;
using FWO.Data;
using NetTools;

namespace FWO.Compliance
{
    public static class SpecialZone
    {
        public static void CalculateInternetZone(this ComplianceNetworkZone internetZone, List<ComplianceNetworkZone> excludedZones)
        {
            IPAddressRange fullRangeIPv4 = IPAddressRange.Parse("0.0.0.0/0");

            List<IPAddressRange> excludedZonesIPRanges = excludedZones.ParseToListOfRanges(true);
            List<IPAddressRange> internetZoneIPRanges = fullRangeIPv4.Subtract(excludedZonesIPRanges);

            internetZone.IPRanges = internetZoneIPRanges.ToArray();
        }

        public static void CalculateLocalZone(this ComplianceNetworkZone localZone, List<IPAddressRange> localZoneRanges, List<ComplianceNetworkZone> definedZones)
        {
            List<IPAddressRange> definedZonesIPRanges = definedZones.ParseToListOfRanges(true);
            List<IPAddressRange> localZoneIPRanges = new();

            foreach (IPAddressRange range in localZoneRanges)
            {
                List<IPAddressRange> ranges = range.Subtract(definedZonesIPRanges);

                foreach (IPAddressRange newRange in ranges)
                {
                    bool exists = localZoneIPRanges.Any(r =>
                        r.Begin.Equals(newRange.Begin) &&
                        r.End.Equals(newRange.End));

                    if (!exists)
                    {
                        localZoneIPRanges.Add(newRange);
                    }                 
                }

            }

            localZone.IPRanges = localZoneIPRanges.ToArray();
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