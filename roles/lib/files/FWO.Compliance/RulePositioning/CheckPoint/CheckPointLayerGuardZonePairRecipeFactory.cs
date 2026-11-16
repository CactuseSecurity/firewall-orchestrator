using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;

namespace FWO.Compliance.RulePositioning.CheckPoint;

/// <summary>Builds the Check Point layer-guard zone-pair Section positioning recipe.</summary>
public static class CheckPointLayerGuardZonePairRecipeFactory
{
    /// <summary>Validates layer-guard zone-pair inputs and constructs the complete immutable recipe.</summary>
    public static PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>,
        CheckPointLayerGuardZonePairRecipeFailureCategory> Create(
        CheckPointLayerGuardZonePairPositioningInput? positioningInput,
        string? normalizedSectionName)
    {
        if (positioningInput == null || string.IsNullOrWhiteSpace(normalizedSectionName))
        {
            return Failure(CheckPointLayerGuardZonePairRecipeFailureCategory.InvalidInput);
        }

        try
        {
            CheckPointLayerGuardScopeBlock layer = new();
            CheckPointSectionScopeBlock section = new(
                layer,
                CheckPointSectionSelector.ByNormalizedName(normalizedSectionName));
            PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> recipe = new(new EndOfBlock(section));
            return PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>,
                CheckPointLayerGuardZonePairRecipeFailureCategory>.Success(recipe);
        }
        catch (PositioningRecipeException)
        {
            return Failure(CheckPointLayerGuardZonePairRecipeFailureCategory.InvalidRecipe);
        }
    }

    /// <summary>Creates a safe category-only failure.</summary>
    private static PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>,
        CheckPointLayerGuardZonePairRecipeFailureCategory> Failure(
        CheckPointLayerGuardZonePairRecipeFailureCategory category) =>
        PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>,
            CheckPointLayerGuardZonePairRecipeFailureCategory>.Failure(category);
}

/// <summary>Safe diagnostics for Check Point zone-pair recipe failures.</summary>
public static class CheckPointLayerGuardZonePairRecipeFailureDiagnostics
{
    /// <summary>Returns the fixed safe diagnostic for a recipe-construction failure.</summary>
    public static string GetDiagnostic(this CheckPointLayerGuardZonePairRecipeFailureCategory category) => category switch
    {
        CheckPointLayerGuardZonePairRecipeFailureCategory.InvalidInput =>
            "The Check Point layer-guard zone-pair positioning input is invalid.",
        _ => "The Check Point layer-guard zone-pair positioning recipe is invalid."
    };
}

/// <summary>Stable failure categories for Check Point zone-pair recipe construction.</summary>
public enum CheckPointLayerGuardZonePairRecipeFailureCategory
{
    InvalidInput,
    InvalidRecipe
}
