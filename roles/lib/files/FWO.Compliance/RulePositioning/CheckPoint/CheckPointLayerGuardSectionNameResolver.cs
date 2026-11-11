using FWO.Data.RulePositioning;
using FWO.Services.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning;

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

        RulePositioningDiagnosticLog.WriteInfo("section-template.render", "start", new Dictionary<string, object?>
        {
            ["required_variables"] = template.RequiredVariables.OrderBy(variable => variable).Select(variable => variable.ToString()).ToArray(),
            ["source_zone_ids"] = zoneInput?.SourceZones.Select(zone => zone.Id).Take(16).ToArray(),
            ["destination_zone_ids"] = zoneInput?.DestinationZones.Select(zone => zone.Id).Take(16).ToArray()
        });

        Dictionary<CheckPointSectionNameVariable, string> values = [];
        if (template.RequiredVariables.Count > 0 && zoneInput == null)
        {
            return LoggedFailure(CheckPointLayerGuardSectionNameFailureCategory.MissingZoneInput);
        }

        if (template.RequiredVariables.Contains(CheckPointSectionNameVariable.SourceZone) &&
            !TryAddZoneValue(
                values,
                CheckPointSectionNameVariable.SourceZone,
                zoneInput!.SourceZones,
                zoneInput.SourceCoverage,
                out CheckPointLayerGuardSectionNameFailureCategory sourceFailure))
        {
            return LoggedFailure(sourceFailure);
        }

        if (template.RequiredVariables.Contains(CheckPointSectionNameVariable.DestinationZone) &&
            !TryAddZoneValue(
                values,
                CheckPointSectionNameVariable.DestinationZone,
                zoneInput!.DestinationZones,
                zoneInput.DestinationCoverage,
                out CheckPointLayerGuardSectionNameFailureCategory destinationFailure))
        {
            return LoggedFailure(destinationFailure);
        }

        PositioningResult<string, CheckPointSectionNameTemplateFailureCategory> rendered = template.Render(values);
        if (!rendered.TryGetValue(out string? sectionName))
        {
            rendered.TryGetFailure(out CheckPointSectionNameTemplateFailureCategory renderCategory);
            RulePositioningDiagnosticLog.WriteFailure("section-template.render", new Dictionary<string, object?>
            {
                ["failure_category"] = CheckPointLayerGuardSectionNameFailureCategory.TemplateRenderInvalid.ToString(),
                ["render_category"] = renderCategory.ToString()
            });
            return Failure(CheckPointLayerGuardSectionNameFailureCategory.TemplateRenderInvalid);
        }
        RulePositioningDiagnosticFingerprint fingerprint = RulePositioningDiagnosticLog.Fingerprint(sectionName);
        RulePositioningDiagnosticLog.WriteInfo("section-template.render", "success", new Dictionary<string, object?>
        {
            ["rendered_name_length"] = fingerprint.Length,
            ["rendered_name_fingerprint"] = fingerprint.Sha256Prefix
        });
        return PositioningResult<string, CheckPointLayerGuardSectionNameFailureCategory>.Success(sectionName);
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

    private static PositioningResult<string, CheckPointLayerGuardSectionNameFailureCategory> LoggedFailure(
        CheckPointLayerGuardSectionNameFailureCategory category)
    {
        RulePositioningDiagnosticLog.WriteFailure("section-template.render", new Dictionary<string, object?>
        {
            ["failure_category"] = category.ToString()
        });
        return Failure(category);
    }
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
