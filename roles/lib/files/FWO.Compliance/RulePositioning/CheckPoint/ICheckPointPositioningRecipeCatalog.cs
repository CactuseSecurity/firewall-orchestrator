using FWO.Data.DeviceSpecificSettings;
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
    CheckPointRecipeSelectionResult SelectRecipe(
        ProvisioningPositioningAlgorithm positioningAlgorithm,
        KfwCheckPointPositioningInput positioningInput,
        CreationRuleZoneInput zoneInput);
}

/// <summary>Base result for Check Point recipe selection.</summary>
public abstract class CheckPointRecipeSelectionResult;

/// <summary>A successful Check Point recipe selection.</summary>
public sealed class CheckPointRecipeSelectionSuccess : CheckPointRecipeSelectionResult
{
    /// <summary>Creates a successful selection.</summary>
    public CheckPointRecipeSelectionSuccess(
        PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> recipe)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
    }

    /// <summary>Gets the selected complete recipe.</summary>
    public PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> Recipe { get; }
}

/// <summary>A failed Check Point recipe selection with a safe diagnostic.</summary>
public sealed class CheckPointRecipeSelectionFailure : CheckPointRecipeSelectionResult
{
    /// <summary>Creates a failed selection.</summary>
    public CheckPointRecipeSelectionFailure(CheckPointRecipeSelectionFailureCategory category)
    {
        Category = category;
        Diagnostic = GetDiagnostic(category);
    }

    /// <summary>Gets the stable failure category.</summary>
    public CheckPointRecipeSelectionFailureCategory Category { get; }

    /// <summary>Gets the fixed caller-safe diagnostic.</summary>
    public string Diagnostic { get; }

    /// <summary>Maps a failure category to its fixed safe diagnostic.</summary>
    private static string GetDiagnostic(CheckPointRecipeSelectionFailureCategory category) => category switch
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

/// <summary>Maps the customer Check Point algorithm to its complete immutable recipe.</summary>
public sealed class CheckPointPositioningRecipeCatalog(KfwCheckPointPositioningRecipeFactory recipeFactory)
    : ICheckPointPositioningRecipeCatalog
{
    private readonly KfwCheckPointPositioningRecipeFactory recipeFactory = recipeFactory ??
        throw new ArgumentNullException(nameof(recipeFactory));

    /// <inheritdoc />
    public bool SupportsAlgorithm(ProvisioningPositioningAlgorithm positioningAlgorithm) =>
        positioningAlgorithm == ProvisioningPositioningAlgorithm.CheckPointLayerGuardEndOfZonePairSection;

    /// <inheritdoc />
    public CheckPointRecipeSelectionResult SelectRecipe(
        ProvisioningPositioningAlgorithm positioningAlgorithm,
        KfwCheckPointPositioningInput positioningInput,
        CreationRuleZoneInput zoneInput)
    {
        if (!SupportsAlgorithm(positioningAlgorithm))
        {
            return new CheckPointRecipeSelectionFailure(
                CheckPointRecipeSelectionFailureCategory.NoMatchingRecipe);
        }

        KfwCheckPointPositioningRecipeResult result = recipeFactory.Create(positioningInput, zoneInput);
        return result is KfwCheckPointPositioningRecipeSuccess success
            ? new CheckPointRecipeSelectionSuccess(success.Recipe)
            : new CheckPointRecipeSelectionFailure(CheckPointRecipeSelectionFailureCategory.CatalogInvalid);
    }
}
