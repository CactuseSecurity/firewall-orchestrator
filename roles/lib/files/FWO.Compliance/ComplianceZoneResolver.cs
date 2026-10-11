using FWO.Data;
using NetTools;
using System.Net.Sockets;

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
        return ResolveZones(ranges, activeNetworkZones, autoCalculatedInternetZoneActive, internetLocalZoneName, out _);
    }

    /// <summary>
    /// Resolves the zones that overlap with the provided IP ranges and reports the ranges no zone can cover.
    /// </summary>
    /// <param name="ranges">IP ranges to evaluate.</param>
    /// <param name="activeNetworkZones">The active compliance zones.</param>
    /// <param name="autoCalculatedInternetZoneActive">Whether the internet zone is auto-calculated and therefore should not be added as a fallback.</param>
    /// <param name="internetLocalZoneName">Localized display name for the fallback internet zone.</param>
    /// <param name="unassignableRanges">Ranges that matched no zone and that the internet zone cannot cover either.</param>
    public static List<ComplianceNetworkZone> ResolveZones(
        List<IPAddressRange> ranges,
        List<ComplianceNetworkZone> activeNetworkZones,
        bool autoCalculatedInternetZoneActive,
        string internetLocalZoneName,
        out List<IPAddressRange> unassignableRanges)
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

        List<IPAddressRange> undefinedIpRanges = [.. unseenIpAddressRanges.SelectMany(rangeSet => rangeSet)];

        // Both the auto-calculated and the fallback internet zone describe IPv4 only, so an unmatched range of
        // any other address family stays unassigned instead of being reported against a zone it never belonged to.
        unassignableRanges = [.. undefinedIpRanges.Where(range => range.Begin.AddressFamily != AddressFamily.InterNetwork)];

        if (autoCalculatedInternetZoneActive)
        {
            return result;
        }

        if (undefinedIpRanges.Count > unassignableRanges.Count)
        {
            result.Add(new ComplianceNetworkZone
            {
                Name = internetLocalZoneName
            });
        }

        return result;
    }
}
