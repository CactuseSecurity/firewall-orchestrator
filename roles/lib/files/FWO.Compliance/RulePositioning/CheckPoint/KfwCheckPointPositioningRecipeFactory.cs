using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;

namespace FWO.Compliance.RulePositioning.CheckPoint;

/// <summary>Builds the inactive KfW Check Point Section-native positioning recipe.</summary>
public sealed class KfwCheckPointPositioningRecipeFactory
{
    /// <summary>Validates customer inputs and constructs the complete immutable recipe.</summary>
    public KfwCheckPointPositioningRecipeResult Create(
        KfwCheckPointPositioningInput? positioningInput,
        CreationRuleZoneInput? zoneInput)
    {
        if (positioningInput == null || zoneInput == null)
        {
            return Failure(KfwCheckPointPositioningRecipeFailureCategory.InvalidInput);
        }
        if (zoneInput.SourceCoverage != ZoneCoverage.Complete ||
            zoneInput.DestinationCoverage != ZoneCoverage.Complete)
        {
            return Failure(KfwCheckPointPositioningRecipeFailureCategory.ZoneCoverageInvalid);
        }

        List<PositioningZoneReference> source = DistinctById(zoneInput.SourceZones);
        List<PositioningZoneReference> destination = DistinctById(zoneInput.DestinationZones);
        if (source.Count != 1 || destination.Count != 1)
        {
            return Failure(KfwCheckPointPositioningRecipeFailureCategory.ZoneCardinalityInvalid);
        }
        if (source[0].Kind != PositioningZoneKind.Persisted || destination[0].Kind != PositioningZoneKind.Persisted)
        {
            return Failure(KfwCheckPointPositioningRecipeFailureCategory.ZoneNotPersisted);
        }
        if (source[0].Id is not > 0 || destination[0].Id is not > 0 ||
            !CheckPointSectionName.TryBuild(destination[0].Name, source[0].Name, out string sectionName))
        {
            return Failure(KfwCheckPointPositioningRecipeFailureCategory.ZoneDataInvalid);
        }

        try
        {
            CheckPointLayerGuardScopeBlock layer = new();
            CheckPointSectionScopeBlock section = new(layer, CheckPointSectionSelector.ByNormalizedName(sectionName));
            PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> recipe = new(new EndOfBlock(section));
            return new KfwCheckPointPositioningRecipeSuccess(recipe);
        }
        catch (PositioningRecipeException)
        {
            return Failure(KfwCheckPointPositioningRecipeFailureCategory.InvalidRecipe);
        }
    }

    /// <summary>Deduplicates positive persisted identities without hiding invalid IDs.</summary>
    private static List<PositioningZoneReference> DistinctById(
        IReadOnlyCollection<PositioningZoneReference> zones)
    {
        return zones.GroupBy(zone => zone.Id).Select(group => group.First()).ToList();
    }

    /// <summary>Creates a safe category-only failure.</summary>
    private static KfwCheckPointPositioningRecipeFailure Failure(
        KfwCheckPointPositioningRecipeFailureCategory category) => new(category);
}

public abstract class KfwCheckPointPositioningRecipeResult;

public sealed class KfwCheckPointPositioningRecipeSuccess(
    PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> recipe)
    : KfwCheckPointPositioningRecipeResult
{
    public PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> Recipe { get; } =
        recipe ?? throw new ArgumentNullException(nameof(recipe));
}

public sealed class KfwCheckPointPositioningRecipeFailure(
    KfwCheckPointPositioningRecipeFailureCategory category)
    : KfwCheckPointPositioningRecipeResult
{
    public KfwCheckPointPositioningRecipeFailureCategory Category { get; } = category;
    public string Diagnostic { get; } = category switch
    {
        KfwCheckPointPositioningRecipeFailureCategory.InvalidInput => "The KfW positioning input is invalid.",
        KfwCheckPointPositioningRecipeFailureCategory.ZoneCoverageInvalid => "The requested addresses do not have complete zone coverage.",
        KfwCheckPointPositioningRecipeFailureCategory.ZoneCardinalityInvalid => "The requested addresses do not resolve to one zone on each side.",
        KfwCheckPointPositioningRecipeFailureCategory.ZoneNotPersisted => "A resolved zone is not persisted.",
        KfwCheckPointPositioningRecipeFailureCategory.ZoneDataInvalid => "The resolved zone data is invalid.",
        _ => "The KfW positioning recipe is invalid."
    };
}

public enum KfwCheckPointPositioningRecipeFailureCategory
{
    InvalidInput,
    ZoneCoverageInvalid,
    ZoneCardinalityInvalid,
    ZoneNotPersisted,
    ZoneDataInvalid,
    InvalidRecipe
}
