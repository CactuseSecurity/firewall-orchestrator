using FWO.Data.RulePositioning;
using FWO.Services.RulePositioning.CheckPoint;

namespace FWO.Compliance.RulePositioning.CheckPoint;

/// <summary>Resolves the zone values required by a parsed LayerGuard section-name template.</summary>
public static class CheckPointLayerGuardSectionNameResolver
{
    /// <summary>Validates only referenced zone variables and renders the normalized section name.</summary>
    public static PositioningResult<string, CheckPointLayerGuardSectionNameFailureCategory> Resolve(
        CheckPointSectionNameTemplate template,
        CreationRuleZoneInput? zoneInput)
    {
        ArgumentNullException.ThrowIfNull(template);

        Dictionary<CheckPointSectionNameVariable, string> values = [];
        if (template.RequiredVariables.Count > 0 && zoneInput == null)
        {
            return Failure(CheckPointLayerGuardSectionNameFailureCategory.MissingZoneInput);
        }

        if (template.RequiredVariables.Contains(CheckPointSectionNameVariable.SourceZone) &&
            !TryAddZoneValue(
                values,
                CheckPointSectionNameVariable.SourceZone,
                zoneInput!.SourceZones,
                zoneInput.SourceCoverage,
                out CheckPointLayerGuardSectionNameFailureCategory sourceFailure))
        {
            return Failure(sourceFailure);
        }

        if (template.RequiredVariables.Contains(CheckPointSectionNameVariable.DestinationZone) &&
            !TryAddZoneValue(
                values,
                CheckPointSectionNameVariable.DestinationZone,
                zoneInput!.DestinationZones,
                zoneInput.DestinationCoverage,
                out CheckPointLayerGuardSectionNameFailureCategory destinationFailure))
        {
            return Failure(destinationFailure);
        }

        PositioningResult<string, CheckPointSectionNameTemplateFailureCategory> rendered = template.Render(values);
        return rendered.TryGetValue(out string? sectionName)
            ? PositioningResult<string, CheckPointLayerGuardSectionNameFailureCategory>.Success(sectionName)
            : Failure(CheckPointLayerGuardSectionNameFailureCategory.TemplateRenderInvalid);
    }

    private static bool TryAddZoneValue(
        Dictionary<CheckPointSectionNameVariable, string> values,
        CheckPointSectionNameVariable variable,
        IReadOnlyCollection<PositioningZoneReference> zones,
        ZoneCoverage coverage,
        out CheckPointLayerGuardSectionNameFailureCategory failure)
    {
        if (coverage != ZoneCoverage.Complete)
        {
            failure = CheckPointLayerGuardSectionNameFailureCategory.ZoneCoverageInvalid;
            return false;
        }

        List<PositioningZoneReference> distinctZones = zones
            .GroupBy(zone => zone.Id)
            .Select(group => group.First())
            .ToList();
        if (distinctZones.Count != 1)
        {
            failure = CheckPointLayerGuardSectionNameFailureCategory.ZoneCardinalityInvalid;
            return false;
        }

        PositioningZoneReference zone = distinctZones[0];
        if (zone.Kind != PositioningZoneKind.Persisted || zone.Id is not > 0)
        {
            failure = CheckPointLayerGuardSectionNameFailureCategory.ZoneNotPersisted;
            return false;
        }

        string name = zone.Name.Trim();
        if (name.Length == 0)
        {
            failure = CheckPointLayerGuardSectionNameFailureCategory.ZoneDataInvalid;
            return false;
        }

        values.Add(variable, name);
        failure = default;
        return true;
    }

    private static PositioningResult<string, CheckPointLayerGuardSectionNameFailureCategory> Failure(
        CheckPointLayerGuardSectionNameFailureCategory category) =>
        PositioningResult<string, CheckPointLayerGuardSectionNameFailureCategory>.Failure(category);
}

/// <summary>Safe failure categories for resolving a LayerGuard section name.</summary>
public enum CheckPointLayerGuardSectionNameFailureCategory
{
    MissingZoneInput,
    ZoneCoverageInvalid,
    ZoneCardinalityInvalid,
    ZoneNotPersisted,
    ZoneDataInvalid,
    TemplateRenderInvalid
}
