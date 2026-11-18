using FWO.Basics;
using FWO.Data;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;

namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>Pure three-state containment over imported Check Point LayerGuard services.</summary>
internal static class CheckPointGuardServiceContainmentEvaluator
{
    /// <summary>Determines whether every requested service is contained by one LayerGuard service scope.</summary>
    internal static CheckPointGuardContainmentResult Evaluate(
        CheckPointGuardServiceScope scope,
        IReadOnlyCollection<PositioningService> requestedServices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(requestedServices);
        if (requestedServices.Count == 0)
        {
            throw new ArgumentException("Requested services must not be empty.", nameof(requestedServices));
        }
        cancellationToken.ThrowIfCancellationRequested();

        return scope.IsNegated
            ? EvaluateNegated(scope, requestedServices, cancellationToken)
            : EvaluatePositive(scope, requestedServices, cancellationToken);
    }

    /// <summary>Evaluates requested services against positive guard coverage.</summary>
    private static CheckPointGuardContainmentResult EvaluatePositive(
        CheckPointGuardServiceScope scope,
        IReadOnlyCollection<PositioningService> requestedServices,
        CancellationToken cancellationToken)
    {
        foreach (PositioningService requested in requestedServices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCovered(requested, scope.Terms))
            {
                return scope.HasUnresolvedTerms
                    ? CheckPointGuardContainmentResult.Indeterminate
                    : CheckPointGuardContainmentResult.NotContained;
            }
        }
        return CheckPointGuardContainmentResult.Contained;
    }

    /// <summary>Evaluates requested services against exclusions in a negated guard scope.</summary>
    private static CheckPointGuardContainmentResult EvaluateNegated(
        CheckPointGuardServiceScope scope,
        IReadOnlyCollection<PositioningService> requestedServices,
        CancellationToken cancellationToken)
    {
        foreach (PositioningService requested in requestedServices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (scope.Terms.Any(excluded => Overlaps(requested, excluded)))
            {
                return CheckPointGuardContainmentResult.NotContained;
            }
        }
        return scope.HasUnresolvedTerms
            ? CheckPointGuardContainmentResult.Indeterminate
            : CheckPointGuardContainmentResult.Contained;
    }

    /// <summary>Determines whether known positive terms fully cover one requested service.</summary>
    private static bool IsCovered(
        PositioningService requested,
        IReadOnlyCollection<PositioningService> terms)
    {
        if (terms.Any(IsAny))
        {
            return true;
        }
        if (IsAny(requested))
        {
            return false;
        }

        List<PositioningService> matching = [.. terms.Where(term => term.ProtocolId == requested.ProtocolId)];
        if (!IpProtocol.HasPorts(requested.ProtocolId))
        {
            return matching.Count > 0;
        }
        return PortsAreCovered(requested, matching);
    }

    /// <summary>Determines whether matching terms jointly cover the requested inclusive port interval.</summary>
    private static bool PortsAreCovered(
        PositioningService requested,
        IReadOnlyCollection<PositioningService> matchingTerms)
    {
        (int RequestedStart, int RequestedEnd) = PortInterval(requested);
        int nextUncovered = RequestedStart;
        foreach ((int Start, int End) in matchingTerms.Select(PortInterval).OrderBy(interval => interval.Start))
        {
            if (End < nextUncovered)
            {
                continue;
            }
            if (Start > nextUncovered)
            {
                return false;
            }
            if (End >= RequestedEnd)
            {
                return true;
            }
            nextUncovered = End + 1;
        }
        return false;
    }

    /// <summary>Determines whether two normalized service definitions share any traffic.</summary>
    private static bool Overlaps(PositioningService left, PositioningService right)
    {
        if (IsAny(left) || IsAny(right))
        {
            return true;
        }
        if (left.ProtocolId != right.ProtocolId)
        {
            return false;
        }
        if (!IpProtocol.HasPorts(left.ProtocolId))
        {
            return true;
        }
        (int LeftStart, int LeftEnd) = PortInterval(left);
        (int RightStart, int RightEnd) = PortInterval(right);
        return LeftStart <= RightEnd && RightStart <= LeftEnd;
    }

    /// <summary>Converts optional port bounds to a complete inclusive interval.</summary>
    private static (int Start, int End) PortInterval(PositioningService service) =>
        (service.DestinationPortStart ?? 0, service.DestinationPortEnd ?? GlobalConst.kMaxPortNumber);

    /// <summary>Determines whether a service represents every protocol and port.</summary>
    private static bool IsAny(PositioningService service) =>
        service.ProtocolId == GlobalConst.kAnyIpProtocolId;
}
