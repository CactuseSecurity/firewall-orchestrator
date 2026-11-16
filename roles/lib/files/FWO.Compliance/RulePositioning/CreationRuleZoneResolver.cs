using FWO.Basics;
using FWO.Data;
using FWO.NetworkTopology;
using FWO.Data.RulePositioning;
using NetTools;
using System.Net.Sockets;
using CreationRuleZoneResolutionResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Compliance.RulePositioning.CreationRuleZoneInput,
    FWO.Compliance.RulePositioning.CreationRuleZoneResolutionFailureCategory>;

namespace FWO.Compliance.RulePositioning;

/// <summary>Resolves a new access rule's normalized address ranges to ordered zone sets.</summary>
public sealed class CreationRuleZoneResolver : ICreationRuleZoneResolver
{
    /// <summary>Resolved zones and address coverage for one rule side.</summary>
    private sealed record SideResolution(List<PositioningZoneReference> Zones, ZoneCoverage Coverage);

    /// <inheritdoc />
    public Task<CreationRuleZoneResolutionResult> ResolveAsync(
        CreationRuleNetworkInput networkInput,
        IReadOnlyCollection<NetworkZone> networkZones,
        RulePositioningConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (networkInput == null)
            {
                return Task.FromResult<CreationRuleZoneResolutionResult>(
                    Failure(CreationRuleZoneResolutionFailureCategory.InvalidInput));
            }
            if (!TryValidateZones(networkZones, configuration, cancellationToken, out List<NetworkZone> validatedZones))
            {
                return Task.FromResult<CreationRuleZoneResolutionResult>(
                    Failure(CreationRuleZoneResolutionFailureCategory.InvalidZoneData));
            }
            List<IPAddressRange> sourceRanges = networkInput.SourceRanges.ToList();
            List<IPAddressRange> destinationRanges = networkInput.DestinationRanges.ToList();
            SideResolution source = ResolveSide(sourceRanges, validatedZones, configuration, cancellationToken);
            SideResolution destination = ResolveSide(destinationRanges, validatedZones, configuration, cancellationToken);
            return Task.FromResult(CreationRuleZoneResolutionResult.Success(
                new CreationRuleZoneInput(
                    configuration.DesignatedZoneMatrixId,
                    source.Zones,
                    destination.Zones,
                    source.Coverage,
                    destination.Coverage)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            return Task.FromResult<CreationRuleZoneResolutionResult>(
                Failure(CreationRuleZoneResolutionFailureCategory.InvalidAddress));
        }
        catch (Exception)
        {
            return Task.FromResult<CreationRuleZoneResolutionResult>(
                Failure(CreationRuleZoneResolutionFailureCategory.UnexpectedFailure));
        }
    }

    /// <summary>Validates and copies caller-supplied zone rows.</summary>
    private static bool TryValidateZones(
        IReadOnlyCollection<NetworkZone> networkZones,
        RulePositioningConfiguration configuration,
        CancellationToken cancellationToken,
        out List<NetworkZone> validatedZones)
    {
        validatedZones = [];
        if (!HasValidZoneInput(networkZones, configuration))
        {
            return false;
        }

        HashSet<int> zoneIds = [];
        foreach (NetworkZone? zone in networkZones!)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateZone(
                    zone, configuration.DesignatedZoneMatrixId, zoneIds, cancellationToken,
                    out NetworkZone? validatedZone))
            {
                return false;
            }

            validatedZones.Add(validatedZone!);
        }

        return IsValidAutomaticInternetZone(validatedZones, configuration, cancellationToken);
    }

    /// <summary>Checks matrix identity and caller-supplied zone collection presence.</summary>
    private static bool HasValidZoneInput(
        IReadOnlyCollection<NetworkZone>? networkZones,
        RulePositioningConfiguration? configuration) =>
        configuration?.DesignatedZoneMatrixId > 0 && networkZones is { Count: > 0 };

    /// <summary>Validates and defensively copies one zone row.</summary>
    private static bool TryValidateZone(
        NetworkZone? zone,
        int matrixId,
        HashSet<int> zoneIds,
        CancellationToken cancellationToken,
        out NetworkZone? validatedZone)
    {
        validatedZone = null;
        if (zone == null || zone.Id <= 0 || !zoneIds.Add(zone.Id) ||
            zone.CriterionId != matrixId || string.IsNullOrWhiteSpace(zone.Name) || zone.IPRanges == null)
        {
            return false;
        }
        if (!TryCloneZone(zone, cancellationToken, out NetworkZone clonedZone))
        {
            return false;
        }
        validatedZone = clonedZone;
        return true;
    }

    /// <summary>Validates the configured automatic Internet-zone invariant.</summary>
    private static bool IsValidAutomaticInternetZone(
        List<NetworkZone> networkZones,
        RulePositioningConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!configuration.AutoCalculateInternetZone)
        {
            return true;
        }

        List<NetworkZone> internetZones = [.. networkZones.Where(zone => zone.IsAutoCalculatedInternetZone)];
        bool containsIpv4Range = false;
        if (internetZones.Count == 1)
        {
            foreach (IPAddressRange range in internetZones[0].IPRanges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                containsIpv4Range |= range.Begin.AddressFamily == AddressFamily.InterNetwork;
            }
        }

        return internetZones.Count == 1 && containsIpv4Range;
    }

    /// <summary>Resolves and converts one side of the rule.</summary>
    private static SideResolution ResolveSide(
        List<IPAddressRange> ranges,
        List<NetworkZone> networkZones,
        RulePositioningConfiguration configuration,
        CancellationToken cancellationToken)
    {
        List<NetworkZone> resolvedZones = NetworkZoneResolver.ResolveZones(
            ranges,
            networkZones,
            configuration.AutoCalculateInternetZone,
            configuration.InternetLocalZoneName,
            cancellationToken,
            out List<IPAddressRange> unassignableRanges);
        List<PositioningZoneReference> references = resolvedZones
            .Select(ToPositioningReference)
            .OrderBy(reference => reference.Kind)
            .ThenBy(reference => reference.Id)
            .ToList();
        ZoneCoverage coverage = ZoneCoverage.Complete;
        if (unassignableRanges.Count > 0)
        {
            coverage = references.Count == 0 ? ZoneCoverage.None : ZoneCoverage.Partial;
        }
        return new SideResolution(references, coverage);
    }

    /// <summary>Converts a compliance zone without retaining mutable caller data.</summary>
    private static PositioningZoneReference ToPositioningReference(NetworkZone zone)
    {
        bool persisted = zone.Id > 0;
        return new PositioningZoneReference(
            persisted ? zone.Id : null,
            zone.IdString,
            zone.Name,
            zone.IsAutoCalculatedInternetZone,
            zone.IsAutoCalculatedUndefinedInternalZone,
            persisted ? PositioningZoneKind.Persisted : PositioningZoneKind.SyntheticInternetFallback);
    }

    /// <summary>Checks endpoint presence, family and ordering.</summary>
    private static bool IsValidRange(IPAddressRange? range)
    {
        return range?.Begin != null && range.End != null
            && range.Begin.AddressFamily == range.End.AddressFamily
            && IpOperations.CompareIpValues(range.Begin, range.End) <= 0;
    }

    /// <summary>Copies only zone state used by overlap resolution and positioning output.</summary>
    private static bool TryCloneZone(
        NetworkZone zone,
        CancellationToken cancellationToken,
        out NetworkZone clonedZone)
    {
        clonedZone = null!;
        List<IPAddressRange> ranges = [];
        foreach (IPAddressRange? range in zone.IPRanges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsValidRange(range))
            {
                return false;
            }

            ranges.Add(new IPAddressRange(range!.Begin, range.End));
        }

        clonedZone = new NetworkZone
        {
            Id = zone.Id,
            IdString = zone.IdString,
            Name = zone.Name,
            CriterionId = zone.CriterionId,
            IsAutoCalculatedInternetZone = zone.IsAutoCalculatedInternetZone,
            IsAutoCalculatedUndefinedInternalZone = zone.IsAutoCalculatedUndefinedInternalZone,
            IPRanges = [.. ranges]
        };
        return true;
    }

    /// <summary>Creates a failed result.</summary>
    private static CreationRuleZoneResolutionResult Failure(CreationRuleZoneResolutionFailureCategory category)
    {
        return CreationRuleZoneResolutionResult.Failure(category);
    }
}
