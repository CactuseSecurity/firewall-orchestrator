using FWO.Basics;
using FWO.Data.Workflow;
using NetTools;

namespace FWO.Compliance.RulePositioning;

/// <summary>Normalizes and deduplicates positioning address ranges.</summary>
internal static class PositioningAddressRangeNormalizer
{
    /// <summary>Parses one workflow leaf with the established compliance address semantics.</summary>
    internal static IPAddressRange Normalize(WfReqElement element)
    {
        bool hasCidr = element.Cidr?.Valid == true;
        string start = hasCidr ? element.Cidr!.CidrString : element.IpString ?? string.Empty;
        string end = element.IpEnd ?? string.Empty;
        if (hasCidr)
        {
            end = element.CidrEnd?.Valid == true ? element.CidrEnd.CidrString : string.Empty;
        }
        if (string.IsNullOrWhiteSpace(start))
        {
            throw new NetworkResolutionException(CreationRuleNetworkResolutionFailureCategory.ObjectUnresolved);
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(end))
            {
                start = start.StripOffNetmask();
                end = end.StripOffNetmask();
            }
            if (!IpOperations.TryParseIPRange(start, end, out IPAddressRange? parsed) || parsed == null || !IsValid(parsed))
            {
                throw InvalidAddress();
            }
            return new IPAddressRange(parsed.Begin, parsed.End);
        }
        catch (NetworkResolutionException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            throw InvalidAddress();
        }
    }

    /// <summary>Parses a flattened Flow-catalog member.</summary>
    internal static IPAddressRange Normalize(string? start, string? end)
    {
        WfReqElement element = new() { IpString = start, IpEnd = end };
        return Normalize(element);
    }

    /// <summary>Copies exact distinct ranges in stable first-seen order.</summary>
    internal static List<IPAddressRange> Distinct(IEnumerable<IPAddressRange> ranges)
    {
        HashSet<string> keys = [];
        List<IPAddressRange> result = [];
        foreach (IPAddressRange range in ranges)
        {
            string key = $"{range.Begin.AddressFamily}:{range.Begin}-{range.End}";
            if (keys.Add(key))
            {
                result.Add(new IPAddressRange(range.Begin, range.End));
            }
        }
        return result;
    }

    /// <summary>Checks endpoint presence, family, and inclusive order.</summary>
    private static bool IsValid(IPAddressRange range) =>
        range.Begin != null && range.End != null &&
        range.Begin.AddressFamily == range.End.AddressFamily &&
        IpOperations.CompareIpValues(range.Begin, range.End) <= 0;

    /// <summary>Creates the common invalid-address resolution failure.</summary>
    private static NetworkResolutionException InvalidAddress() =>
        new(CreationRuleNetworkResolutionFailureCategory.InvalidAddress);
}

/// <summary>Signals one safely categorized network-input normalization failure.</summary>
public sealed class NetworkResolutionException(CreationRuleNetworkResolutionFailureCategory category) : Exception
{
    /// <summary>Gets the safe failure category.</summary>
    public CreationRuleNetworkResolutionFailureCategory Category { get; } = category;
}
