using FWO.Basics;
using FWO.Data.RulePositioning.CheckPoint;
using NetTools;

namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>Three-state result of evaluating address ranges against one guard side.</summary>
internal enum CheckPointGuardContainmentResult
{
    Contained,
    NotContained,
    Indeterminate
}

/// <summary>Pure three-state containment over imported Check Point guard semantics.</summary>
internal static class CheckPointGuardContainmentEvaluator
{
    /// <summary>Determines whether all requested ranges are contained by one rule side.</summary>
    internal static CheckPointGuardContainmentResult Evaluate(
        CheckPointGuardSide side,
        IReadOnlyCollection<IPAddressRange> requestedRanges,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(side);
        ArgumentNullException.ThrowIfNull(requestedRanges);
        if (requestedRanges.Count == 0)
        {
            throw new ArgumentException("Requested ranges must not be empty.", nameof(requestedRanges));
        }

        List<IPAddressRange> definite = BuildDefiniteCoverage(side.Terms, cancellationToken);
        if (side.IsNegated)
        {
            if (requestedRanges.Any(requested => definite.Any(known => IpOperations.RangeOverlapExists(requested, known))))
            {
                return CheckPointGuardContainmentResult.NotContained;
            }
            return CouldUnknownOverlap(side.Terms, requestedRanges)
                ? CheckPointGuardContainmentResult.Indeterminate
                : CheckPointGuardContainmentResult.Contained;
        }

        List<IPAddressRange> uncovered = SubtractKnownCoverage(requestedRanges, definite, cancellationToken);
        if (uncovered.Count == 0)
        {
            return CheckPointGuardContainmentResult.Contained;
        }
        return CouldUnknownOverlap(side.Terms, uncovered)
            ? CheckPointGuardContainmentResult.Indeterminate
            : CheckPointGuardContainmentResult.NotContained;
    }

    /// <summary>Unions per-term definite positive coverage without leaking exclusions across terms.</summary>
    private static List<IPAddressRange> BuildDefiniteCoverage(
        IReadOnlyCollection<CheckPointGuardAddressTerm> terms,
        CancellationToken cancellationToken)
    {
        List<IPAddressRange> result = [];
        foreach (CheckPointGuardAddressTerm term in terms)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (term.HasUnresolvedExcludedTerms)
            {
                continue;
            }
            foreach (IPAddressRange included in term.IncludedRanges)
            {
                List<IPAddressRange> exclusions = [.. term.ExcludedRanges.Where(excluded =>
                    excluded.Begin.AddressFamily == included.Begin.AddressFamily)];
                result.AddRange(included.Subtract(exclusions));
            }
        }
        return result;
    }

    /// <summary>Subtracts known union coverage independently for IPv4 and IPv6.</summary>
    private static List<IPAddressRange> SubtractKnownCoverage(
        IReadOnlyCollection<IPAddressRange> requested,
        IReadOnlyCollection<IPAddressRange> coverage,
        CancellationToken cancellationToken)
    {
        List<IPAddressRange> uncovered = [];
        foreach (IPAddressRange range in requested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<IPAddressRange> sameFamily = [.. coverage.Where(known =>
                known.Begin.AddressFamily == range.Begin.AddressFamily)];
            uncovered.AddRange(range.Subtract(sameFamily));
        }
        return uncovered;
    }

    /// <summary>Detects whether unresolved semantics could change overlap for the requested set.</summary>
    private static bool CouldUnknownOverlap(
        IReadOnlyCollection<CheckPointGuardAddressTerm> terms,
        IReadOnlyCollection<IPAddressRange> requested)
    {
        foreach (CheckPointGuardAddressTerm term in terms)
        {
            if (term.HasUnresolvedIncludedTerms)
            {
                return true;
            }
            if (term.HasUnresolvedExcludedTerms && requested.Any(candidate =>
                term.IncludedRanges.Any(included => IpOperations.RangeOverlapExists(candidate, included))))
            {
                return true;
            }
        }
        return terms.Count == 0;
    }
}
