using FWO.Basics;
using FWO.Data;
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
    private sealed record SideResolution(List<PositioningZoneReference> Zones, ZoneCoverage Coverage);

    /// <summary>Internal control-flow failure carrying a safe public category.</summary>
    private sealed class ResolutionFailureException(CreationRuleZoneResolutionFailureCategory category) : Exception
    {
        public CreationRuleZoneResolutionFailureCategory Category { get; } = category;
    }

    /// <inheritdoc />
    public Task<CreationRuleZoneResolutionResult> ResolveAsync(
        CreationRuleNetworkInput networkInput,
        IReadOnlyCollection<ComplianceNetworkZone> networkZones,
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
            List<ComplianceNetworkZone> validatedZones = ValidateZones(networkZones, configuration, cancellationToken);
            SideResolution source = ResolveSide([.. networkInput.SourceRanges], validatedZones, configuration, cancellationToken);
            SideResolution destination = ResolveSide([.. networkInput.DestinationRanges], validatedZones, configuration, cancellationToken);
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
        catch (ResolutionFailureException exception)
        {
            return Task.FromResult<CreationRuleZoneResolutionResult>(Failure(exception.Category));
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
    private static List<ComplianceNetworkZone> ValidateZones(
        IReadOnlyCollection<ComplianceNetworkZone> networkZones,
        RulePositioningConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (configuration == null || configuration.DesignatedZoneMatrixId <= 0 ||
            networkZones == null || networkZones.Count == 0)
        {
            throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidZoneData);
        }

        HashSet<int> zoneIds = [];
        List<ComplianceNetworkZone> validatedZones = [];
        foreach (ComplianceNetworkZone? zone in networkZones)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (zone == null || zone.Id <= 0 || !zoneIds.Add(zone.Id)
                || zone.CriterionId != configuration.DesignatedZoneMatrixId || string.IsNullOrWhiteSpace(zone.Name)
                || zone.IPRanges == null)
            {
                throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidZoneData);
            }

            validatedZones.Add(CloneZone(zone, cancellationToken));
        }

        ValidateAutomaticInternetZone(validatedZones, configuration, cancellationToken);
        return validatedZones;
    }

    /// <summary>Validates the configured automatic Internet-zone invariant.</summary>
    private static void ValidateAutomaticInternetZone(
        List<ComplianceNetworkZone> networkZones,
        RulePositioningConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!configuration.AutoCalculateInternetZone)
        {
            return;
        }

        List<ComplianceNetworkZone> internetZones = [.. networkZones.Where(zone => zone.IsAutoCalculatedInternetZone)];
        bool containsIpv4Range = false;
        if (internetZones.Count == 1)
        {
            foreach (IPAddressRange range in internetZones[0].IPRanges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                containsIpv4Range |= range.Begin.AddressFamily == AddressFamily.InterNetwork;
            }
        }

        if (internetZones.Count != 1 || !containsIpv4Range)
        {
            throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidZoneData);
        }
    }

    /// <summary>Resolves and converts one side of the rule.</summary>
    private static SideResolution ResolveSide(
        List<IPAddressRange> ranges,
        List<ComplianceNetworkZone> networkZones,
        RulePositioningConfiguration configuration,
        CancellationToken cancellationToken)
    {
        List<ComplianceNetworkZone> resolvedZones = ComplianceZoneResolver.ResolveZones(
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
    private static PositioningZoneReference ToPositioningReference(ComplianceNetworkZone zone)
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
    private static ComplianceNetworkZone CloneZone(ComplianceNetworkZone zone, CancellationToken cancellationToken)
    {
        List<IPAddressRange> ranges = [];
        foreach (IPAddressRange? range in zone.IPRanges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsValidRange(range))
            {
                throw new ResolutionFailureException(CreationRuleZoneResolutionFailureCategory.InvalidZoneData);
            }

            ranges.Add(new IPAddressRange(range!.Begin, range.End));
        }

        return new ComplianceNetworkZone
        {
            Id = zone.Id,
            IdString = zone.IdString,
            Name = zone.Name,
            CriterionId = zone.CriterionId,
            IsAutoCalculatedInternetZone = zone.IsAutoCalculatedInternetZone,
            IsAutoCalculatedUndefinedInternalZone = zone.IsAutoCalculatedUndefinedInternalZone,
            IPRanges = [.. ranges]
        };
    }

    /// <summary>Creates a failed result.</summary>
    private static CreationRuleZoneResolutionResult Failure(CreationRuleZoneResolutionFailureCategory category)
    {
        return CreationRuleZoneResolutionResult.Failure(category);
    }
}
