using System.Collections.ObjectModel;
using FWO.Basics;
using NetTools;

namespace FWO.Data.RulePositioning.CheckPoint;

/// <summary>Immutable fixed-import guard snapshot for Check Point inline layers.</summary>
public sealed class CheckPointLayerGuardSnapshot
{
    /// <summary>Gets the management owning the imported guards.</summary>
    public int ManagementId { get; }
    /// <summary>Gets the gateway whose inline-layer guards were read.</summary>
    public int GatewayId { get; }
    /// <summary>Gets the fixed import identifier.</summary>
    public long ImportId { get; }
    /// <summary>Gets guards keyed by target inline-layer identifier.</summary>
    public IReadOnlyDictionary<long, CheckPointLayerGuard> GuardsByTargetLayerId { get; }

    /// <summary>Creates a validated snapshot keyed by unique target layer identity.</summary>
    public CheckPointLayerGuardSnapshot(
        int managementId,
        int gatewayId,
        long importId,
        IReadOnlyCollection<CheckPointLayerGuard> guards)
    {
        if (managementId <= 0 || gatewayId <= 0 || importId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(managementId));
        }
        ArgumentNullException.ThrowIfNull(guards);
        Dictionary<long, CheckPointLayerGuard> copied = [];
        foreach (CheckPointLayerGuard? guard in guards)
        {
            if (guard == null || !copied.TryAdd(guard.TargetLayerId, guard))
            {
                throw new ArgumentException("Guard target layers must be unique.", nameof(guards));
            }
        }
        ManagementId = managementId;
        GatewayId = gatewayId;
        ImportId = importId;
        GuardsByTargetLayerId = new ReadOnlyDictionary<long, CheckPointLayerGuard>(copied);
    }
}

/// <summary>Guard rule attached to one inline target layer.</summary>
public sealed class CheckPointLayerGuard
{
    /// <summary>Gets the rule implementing the layer guard.</summary>
    public long SourceRuleId { get; }
    /// <summary>Gets the rulebase containing the guard rule.</summary>
    public long SourceRulebaseId { get; }
    /// <summary>Gets the inline layer selected by the guard.</summary>
    public long TargetLayerId { get; }
    /// <summary>Gets normalized source-side guard semantics.</summary>
    public CheckPointGuardSide Source { get; }
    /// <summary>Gets normalized destination-side guard semantics.</summary>
    public CheckPointGuardSide Destination { get; }

    /// <summary>Creates one guard from positive identities and immutable sides.</summary>
    public CheckPointLayerGuard(
        long sourceRuleId,
        long sourceRulebaseId,
        long targetLayerId,
        CheckPointGuardSide source,
        CheckPointGuardSide destination)
    {
        if (sourceRuleId <= 0 || sourceRulebaseId <= 0 || targetLayerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRuleId));
        }
        SourceRuleId = sourceRuleId;
        SourceRulebaseId = sourceRulebaseId;
        TargetLayerId = targetLayerId;
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Destination = destination ?? throw new ArgumentNullException(nameof(destination));
    }
}

/// <summary>One guard rule side and its top-level address terms.</summary>
public sealed class CheckPointGuardSide
{
    /// <summary>Gets whether the complete guard side is negated.</summary>
    public bool IsNegated { get; }
    /// <summary>Gets normalized top-level terms.</summary>
    public IReadOnlyList<CheckPointGuardAddressTerm> Terms { get; }

    /// <summary>Defensively copies top-level terms; an empty side remains explicitly unresolved.</summary>
    public CheckPointGuardSide(bool isNegated, IReadOnlyCollection<CheckPointGuardAddressTerm> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        if (terms.Any(term => term == null))
        {
            throw new ArgumentException("Guard terms must not contain null values.", nameof(terms));
        }
        IsNegated = isNegated;
        Terms = new ReadOnlyCollection<CheckPointGuardAddressTerm>(terms.ToList());
    }
}

/// <summary>Known and unresolved address semantics of one top-level guard association.</summary>
public sealed class CheckPointGuardAddressTerm
{
    /// <summary>Gets address ranges included by the term.</summary>
    public IReadOnlyList<IPAddressRange> IncludedRanges { get; }
    /// <summary>Gets address ranges excluded by the term.</summary>
    public IReadOnlyList<IPAddressRange> ExcludedRanges { get; }
    /// <summary>Gets whether an included association could not be resolved.</summary>
    public bool HasUnresolvedIncludedTerms { get; }
    /// <summary>Gets whether an excluded association could not be resolved.</summary>
    public bool HasUnresolvedExcludedTerms { get; }

    /// <summary>Copies and deduplicates valid known address ranges.</summary>
    public CheckPointGuardAddressTerm(
        IReadOnlyCollection<IPAddressRange> includedRanges,
        IReadOnlyCollection<IPAddressRange> excludedRanges,
        bool hasUnresolvedIncludedTerms,
        bool hasUnresolvedExcludedTerms)
    {
        IncludedRanges = CopyDistinct(includedRanges, nameof(includedRanges));
        ExcludedRanges = CopyDistinct(excludedRanges, nameof(excludedRanges));
        HasUnresolvedIncludedTerms = hasUnresolvedIncludedTerms;
        HasUnresolvedExcludedTerms = hasUnresolvedExcludedTerms;
    }

    /// <summary>Copies exact distinct ranges after validating endpoints.</summary>
    private static ReadOnlyCollection<IPAddressRange> CopyDistinct(
        IReadOnlyCollection<IPAddressRange> ranges,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(ranges, parameterName);
        Dictionary<string, IPAddressRange> distinct = [];
        foreach (IPAddressRange? range in ranges)
        {
            if (range?.Begin == null || range.End == null ||
                range.Begin.AddressFamily != range.End.AddressFamily ||
                IpOperations.CompareIpValues(range.Begin, range.End) > 0)
            {
                throw new ArgumentException("A guard address range is invalid.", parameterName);
            }
            string key = $"{range.Begin.AddressFamily}:{range.Begin}-{range.End}";
            distinct.TryAdd(key, new IPAddressRange(range.Begin, range.End));
        }
        return new ReadOnlyCollection<IPAddressRange>(distinct.Values.ToList());
    }
}
