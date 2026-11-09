using FWO.Data;

namespace FWO.Test
{
    public static class TestDataGenerator
    {
        public static List<NetworkZone> CreateNetworkZones(int numberOfZones, bool createInternetZone, bool createUndefinedInternalZone)
        {
            List<NetworkZone> zones = new();

            for (int i = 0; i < numberOfZones; i++)
            {
                zones.Add(
                    new NetworkZone
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
                    new NetworkZone
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
                    new NetworkZone
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
