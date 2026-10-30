using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;

namespace FWO.Compliance.RulePositioning.CheckPoint;

/// <summary>Builds the Check Point layer-guard zone-pair Section positioning recipe.</summary>
public static class CheckPointLayerGuardZonePairRecipeFactory
{
    /// <summary>Validates layer-guard zone-pair inputs and constructs the complete immutable recipe.</summary>
    public static CheckPointLayerGuardZonePairRecipeResult Create(
        CheckPointLayerGuardZonePairPositioningInput? positioningInput,
        CreationRuleZoneInput? zoneInput)
    {
        if (positioningInput == null || zoneInput == null)
        {
            return Failure(CheckPointLayerGuardZonePairRecipeFailureCategory.InvalidInput);
        }
        if (zoneInput.SourceCoverage != ZoneCoverage.Complete ||
            zoneInput.DestinationCoverage != ZoneCoverage.Complete)
        {
            return Failure(CheckPointLayerGuardZonePairRecipeFailureCategory.ZoneCoverageInvalid);
        }

        List<PositioningZoneReference> source = DistinctById(zoneInput.SourceZones);
        List<PositioningZoneReference> destination = DistinctById(zoneInput.DestinationZones);
        if (source.Count != 1 || destination.Count != 1)
        {
            return Failure(CheckPointLayerGuardZonePairRecipeFailureCategory.ZoneCardinalityInvalid);
        }
        if (source[0].Kind != PositioningZoneKind.Persisted || destination[0].Kind != PositioningZoneKind.Persisted)
        {
            return Failure(CheckPointLayerGuardZonePairRecipeFailureCategory.ZoneNotPersisted);
        }
        if (source[0].Id is not > 0 || destination[0].Id is not > 0 ||
            !CheckPointSectionName.TryBuild(destination[0].Name, source[0].Name, out string sectionName))
        {
            return Failure(CheckPointLayerGuardZonePairRecipeFailureCategory.ZoneDataInvalid);
        }

        try
        {
            CheckPointLayerGuardScopeBlock layer = new();
            CheckPointSectionScopeBlock section = new(layer, CheckPointSectionSelector.ByNormalizedName(sectionName));
            PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> recipe = new(new EndOfBlock(section));
            return new CheckPointLayerGuardZonePairRecipeSuccess(recipe);
        }
        catch (PositioningRecipeException)
        {
            return Failure(CheckPointLayerGuardZonePairRecipeFailureCategory.InvalidRecipe);
        }
    }

    /// <summary>Deduplicates positive persisted identities without hiding invalid IDs.</summary>
    private static List<PositioningZoneReference> DistinctById(
        IReadOnlyCollection<PositioningZoneReference> zones)
    {
        return zones.GroupBy(zone => zone.Id).Select(group => group.First()).ToList();
    }

    /// <summary>Creates a safe category-only failure.</summary>
    private static CheckPointLayerGuardZonePairRecipeFailure Failure(
        CheckPointLayerGuardZonePairRecipeFailureCategory category) => new(category);
}

public abstract class CheckPointLayerGuardZonePairRecipeResult;

public sealed class CheckPointLayerGuardZonePairRecipeSuccess(
    PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> recipe)
    : CheckPointLayerGuardZonePairRecipeResult
{
    public PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> Recipe { get; } =
        recipe ?? throw new ArgumentNullException(nameof(recipe));
}

public sealed class CheckPointLayerGuardZonePairRecipeFailure(
    CheckPointLayerGuardZonePairRecipeFailureCategory category)
    : CheckPointLayerGuardZonePairRecipeResult
{
    public CheckPointLayerGuardZonePairRecipeFailureCategory Category { get; } = category;
    public string Diagnostic { get; } = category switch
    {
        CheckPointLayerGuardZonePairRecipeFailureCategory.InvalidInput =>
            "The Check Point layer-guard zone-pair positioning input is invalid.",
        CheckPointLayerGuardZonePairRecipeFailureCategory.ZoneCoverageInvalid =>
            "The requested addresses do not have complete zone coverage.",
        CheckPointLayerGuardZonePairRecipeFailureCategory.ZoneCardinalityInvalid =>
            "The requested addresses do not resolve to one zone on each side.",
        CheckPointLayerGuardZonePairRecipeFailureCategory.ZoneNotPersisted =>
            "A resolved zone is not persisted.",
        CheckPointLayerGuardZonePairRecipeFailureCategory.ZoneDataInvalid =>
            "The resolved zone data is invalid.",
        _ => "The Check Point layer-guard zone-pair positioning recipe is invalid."
    };
}

public enum CheckPointLayerGuardZonePairRecipeFailureCategory
{
    InvalidInput,
    ZoneCoverageInvalid,
    ZoneCardinalityInvalid,
    ZoneNotPersisted,
    ZoneDataInvalid,
    InvalidRecipe
}
