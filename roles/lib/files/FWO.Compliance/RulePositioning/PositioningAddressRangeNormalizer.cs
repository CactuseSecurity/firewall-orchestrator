using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using NetTools;

namespace FWO.Compliance.RulePositioning;

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
            NetworkObject networkObject = new()
            {
                IP = start,
                IpEnd = end,
                Type = new NetworkObjectType
                {
                    Name = string.IsNullOrWhiteSpace(end) ? ObjectType.Host : ObjectType.IPRange
                }
            };
            List<IPAddressRange> parsed = ComplianceCheck.ParseIpRange(networkObject);
            if (parsed.Count != 1 || !IsValid(parsed[0]))
            {
                throw InvalidAddress();
            }
            return new IPAddressRange(parsed[0].Begin, parsed[0].End);
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

    private static bool IsValid(IPAddressRange range) =>
        range.Begin != null && range.End != null &&
        range.Begin.AddressFamily == range.End.AddressFamily &&
        IpOperations.CompareIpValues(range.Begin, range.End) <= 0;

    private static NetworkResolutionException InvalidAddress() =>
        new(CreationRuleNetworkResolutionFailureCategory.InvalidAddress);
}

/// <summary>Signals one safely categorized network-input normalization failure.</summary>
public sealed class NetworkResolutionException(CreationRuleNetworkResolutionFailureCategory category) : Exception
{
    /// <summary>Gets the safe failure category.</summary>
    public CreationRuleNetworkResolutionFailureCategory Category { get; } = category;
}
