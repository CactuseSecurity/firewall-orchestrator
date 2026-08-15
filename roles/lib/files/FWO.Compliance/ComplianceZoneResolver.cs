using FWO.Data;
using NetTools;

namespace FWO.Compliance;

/// <summary>
/// Resolves compliance network zones for a set of IP ranges.
/// </summary>
public static class ComplianceZoneResolver
{
    /// <summary>
    /// Resolves the zones that overlap with the provided IP ranges.
    /// </summary>
    /// <param name="ranges">IP ranges to evaluate.</param>
    /// <param name="activeNetworkZones">The active compliance zones.</param>
    /// <param name="autoCalculatedInternetZoneActive">Whether the internet zone is auto-calculated and therefore should not be added as a fallback.</param>
    /// <param name="internetLocalZoneName">Localized display name for the fallback internet zone.</param>
    public static List<ComplianceNetworkZone> ResolveZones(
        List<IPAddressRange> ranges,
        List<ComplianceNetworkZone> activeNetworkZones,
        bool autoCalculatedInternetZoneActive,
        string internetLocalZoneName)
    {
        List<ComplianceNetworkZone> result = [];
        List<List<IPAddressRange>> unseenIpAddressRanges = [];

        for (int index = 0; index < ranges.Count; index++)
        {
            unseenIpAddressRanges.Add([new(ranges[index].Begin, ranges[index].End)]);
        }

        foreach (ComplianceNetworkZone zone in activeNetworkZones.Where(zone => zone.OverlapExists(ranges, unseenIpAddressRanges)))
        {
            result.Add(zone);
        }

        if (autoCalculatedInternetZoneActive)
        {
            return result;
        }

        List<IPAddressRange> undefinedIpRanges = [.. unseenIpAddressRanges.SelectMany(rangeSet => rangeSet)];
        if (undefinedIpRanges.Count > 0)
        {
            result.Add(new ComplianceNetworkZone
            {
                Name = internetLocalZoneName
            });
        }

        return result;
    }
}
