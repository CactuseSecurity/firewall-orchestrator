using FWO.Data;

namespace FWO.Test
{
    public static class TestDataGenerator
    {
        public static List<ComplianceNetworkZone> CreateComplianceNetworkZones(int numberOfZones, bool createInternetZone, bool createUndefinedInternalZone)
        {
            List<ComplianceNetworkZone> zones = new();

            for (int i = 0; i < numberOfZones; i++)
            {
                zones.Add(
                    new ComplianceNetworkZone
                    {
                        Id = i + 1,
                        IdString = $"zone_{i + 1}",
                        Name = $"Zone {i + 1}"
                    }
                );
            }

            if (createInternetZone)
            {
                zones.Add(
                    new ComplianceNetworkZone
                    {
                        Id = numberOfZones + 1,
                        IdString = "AUTO_CALCULATED_ZONE_INTERNET",
                        Name = "Auto-calculated Internet Zone",
                        IsAutoCalculatedInternetZone = true
                    }
                );
            }

            if (createUndefinedInternalZone)
            {
                zones.Add(
                    new ComplianceNetworkZone
                    {
                        Id = numberOfZones + 2,
                        IdString = "AUTO_CALCULATED_ZONE_UNDEFINED_INTERNAL",
                        Name = "Auto-calculated Undefined-internal Zone",
                        IsAutoCalculatedUndefinedInternalZone = true,
                    }
                );
            }

            return zones;
        }        
    }
}