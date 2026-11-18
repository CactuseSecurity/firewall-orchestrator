using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;

namespace FWO.Compliance.RulePositioning.CheckPoint;

/// <summary>Selects complete immutable Check Point positioning recipes.</summary>
public interface ICheckPointPositioningRecipeCatalog
{
    /// <summary>Determines whether the catalog supports the exact algorithm.</summary>
    bool SupportsAlgorithm(ProvisioningPositioningAlgorithm positioningAlgorithm);

    /// <summary>Selects one complete recipe for the supplied immutable input.</summary>
    PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>, CheckPointRecipeSelectionFailureCategory> SelectRecipe(
        ProvisioningPositioningAlgorithm positioningAlgorithm,
        CheckPointLayerGuardZonePairPositioningInput positioningInput,
        string normalizedSectionName);
}

/// <summary>Maps Check Point recipe selection failures to fixed caller-safe diagnostics.</summary>
public static class CheckPointRecipeSelectionFailureDiagnostics
{
    /// <summary>Maps a failure category to its fixed safe diagnostic.</summary>
    public static string GetDiagnostic(this CheckPointRecipeSelectionFailureCategory category) => category switch
    {
        CheckPointRecipeSelectionFailureCategory.NoMatchingRecipe =>
            "No Check Point positioning recipe matches the supplied input.",
        _ => "The Check Point positioning recipe catalog is invalid."
    };
}

/// <summary>Stable failure categories for Check Point recipe selection.</summary>
public enum CheckPointRecipeSelectionFailureCategory
{
    NoMatchingRecipe,
    CatalogInvalid
}

/// <summary>Maps the layer-guard zone-pair Check Point algorithm to its complete immutable recipe.</summary>
public sealed class CheckPointPositioningRecipeCatalog : ICheckPointPositioningRecipeCatalog
{
    /// <inheritdoc />
    public bool SupportsAlgorithm(ProvisioningPositioningAlgorithm positioningAlgorithm) =>
        positioningAlgorithm == ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection;

    /// <inheritdoc />
    public PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>, CheckPointRecipeSelectionFailureCategory> SelectRecipe(
        ProvisioningPositioningAlgorithm positioningAlgorithm,
        CheckPointLayerGuardZonePairPositioningInput positioningInput,
        string normalizedSectionName)
    {
        if (!SupportsAlgorithm(positioningAlgorithm))
        {
            return PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>,
                CheckPointRecipeSelectionFailureCategory>.Failure(
                    CheckPointRecipeSelectionFailureCategory.NoMatchingRecipe);
        }

        PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>,
            CheckPointLayerGuardZonePairRecipeFailureCategory> result = CheckPointLayerGuardZonePairRecipeFactory.Create(
            positioningInput,
            normalizedSectionName);
        return result.TryGetValue(out PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>? recipe)
            ? PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>,
                CheckPointRecipeSelectionFailureCategory>.Success(recipe)
            : PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>,
                CheckPointRecipeSelectionFailureCategory>.Failure(CheckPointRecipeSelectionFailureCategory.CatalogInvalid);
    }
}
