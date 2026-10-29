using FWO.Basics;
using FWO.Data.RulePositioning.CheckPoint;
using NetTools;
using System.Net;

namespace FWO.Services.RulePositioning.CheckPoint;

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
            if (requestedRanges.Any(requested => definite.Any(known => Overlaps(requested, known))))
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
                result.AddRange(Subtract(included, exclusions));
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
            uncovered.AddRange(Subtract(range, sameFamily));
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
                term.IncludedRanges.Any(included => Overlaps(candidate, included))))
            {
                return true;
            }
        }
        return terms.Count == 0;
    }

    /// <summary>Checks inclusive overlap only within one address family.</summary>
    private static bool Overlaps(IPAddressRange left, IPAddressRange right)
    {
        return left.Begin.AddressFamily == right.Begin.AddressFamily &&
            IpOperations.CompareIpValues(left.Begin, right.End) <= 0 &&
            IpOperations.CompareIpValues(right.Begin, left.End) <= 0;
    }

    /// <summary>Subtracts inclusive ranges without coercing IPv4 and IPv6 values.</summary>
    private static List<IPAddressRange> Subtract(
        IPAddressRange source,
        IReadOnlyCollection<IPAddressRange> subtractors)
    {
        List<IPAddressRange> remaining = [new IPAddressRange(source.Begin, source.End)];
        foreach (IPAddressRange subtractor in subtractors)
        {
            List<IPAddressRange> next = [];
            foreach (IPAddressRange current in remaining)
            {
                if (!Overlaps(current, subtractor))
                {
                    next.Add(current);
                    continue;
                }
                if (IpOperations.CompareIpValues(current.Begin, subtractor.Begin) < 0)
                {
                    next.Add(new IPAddressRange(current.Begin, Add(subtractor.Begin, -1)));
                }
                if (IpOperations.CompareIpValues(subtractor.End, current.End) < 0)
                {
                    next.Add(new IPAddressRange(Add(subtractor.End, 1), current.End));
                }
            }
            remaining = next;
        }
        return remaining;
    }

    /// <summary>Adds or subtracts one from a non-boundary IP address.</summary>
    private static IPAddress Add(IPAddress address, int delta)
    {
        byte[] bytes = address.GetAddressBytes();
        int carry = delta;
        for (int index = bytes.Length - 1; index >= 0 && carry != 0; index--)
        {
            int value = bytes[index] + carry;
            bytes[index] = unchecked((byte)value);
            carry = 0;
            if (value < 0)
            {
                carry = -1;
            }
            else if (value > byte.MaxValue)
            {
                carry = 1;
            }
        }
        return new IPAddress(bytes);
    }
}
