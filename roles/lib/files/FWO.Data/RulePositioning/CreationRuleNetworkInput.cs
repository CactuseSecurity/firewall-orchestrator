using System.Collections.ObjectModel;
using FWO.Basics;
using NetTools;

namespace FWO.Data.RulePositioning;

/// <summary>Immutable normalized source and destination address input for a creation rule.</summary>
public sealed class CreationRuleNetworkInput
{
    public IReadOnlyList<IPAddressRange> SourceRanges { get; }
    public IReadOnlyList<IPAddressRange> DestinationRanges { get; }

    /// <summary>Validates and defensively copies both non-empty address collections.</summary>
    public CreationRuleNetworkInput(
        IReadOnlyCollection<IPAddressRange> sourceRanges,
        IReadOnlyCollection<IPAddressRange> destinationRanges)
    {
        SourceRanges = CopyRanges(sourceRanges, nameof(sourceRanges));
        DestinationRanges = CopyRanges(destinationRanges, nameof(destinationRanges));
    }

    /// <summary>Copies valid ranges without retaining caller-owned collections.</summary>
    private static ReadOnlyCollection<IPAddressRange> CopyRanges(
        IReadOnlyCollection<IPAddressRange> ranges,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(ranges, parameterName);
        if (ranges.Count == 0)
        {
            throw new ArgumentException("At least one address range is required.", parameterName);
        }

        List<IPAddressRange> copies = [];
        foreach (IPAddressRange? range in ranges)
        {
            if (range?.Begin == null || range.End == null ||
                range.Begin.AddressFamily != range.End.AddressFamily ||
                IpOperations.CompareIpValues(range.Begin, range.End) > 0)
            {
                throw new ArgumentException("An address range is invalid.", parameterName);
            }
            copies.Add(new IPAddressRange(range.Begin, range.End));
        }
        return new ReadOnlyCollection<IPAddressRange>(copies);
    }
}
