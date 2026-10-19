using FWO.Data.Provisioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;

namespace FWO.Compliance.RulePositioning.CheckPoint;

/// <summary>Selects complete graph-only Check Point positioning recipes.</summary>
public interface ICheckPointGraphPositioningRecipeCatalog
{
    /// <summary>Determines whether the catalog supports the exact algorithm.</summary>
    bool SupportsAlgorithm(ProvisioningPositioningAlgorithm positioningAlgorithm);

    /// <summary>Selects one complete recipe for the supplied algorithm.</summary>
    CheckPointGraphRecipeSelectionResult SelectRecipe(
        ProvisioningPositioningAlgorithm positioningAlgorithm);
}

/// <summary>Base result for graph-only Check Point recipe selection.</summary>
public abstract class CheckPointGraphRecipeSelectionResult
{
    private protected CheckPointGraphRecipeSelectionResult()
    { }
}

/// <summary>A successful graph-only Check Point recipe selection.</summary>
public sealed class CheckPointGraphRecipeSelectionSuccess : CheckPointGraphRecipeSelectionResult
{
    /// <summary>Creates a successful selection.</summary>
    public CheckPointGraphRecipeSelectionSuccess(
        PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> recipe)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
    }

    /// <summary>Gets the selected complete recipe.</summary>
    public PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> Recipe { get; }
}

/// <summary>A failed graph-only Check Point recipe selection.</summary>
public sealed class CheckPointGraphRecipeSelectionFailure : CheckPointGraphRecipeSelectionResult
{
    /// <summary>Creates a failed selection with a stable category.</summary>
    public CheckPointGraphRecipeSelectionFailure(CheckPointGraphRecipeSelectionFailureCategory category)
    {
        Category = category;
    }

    /// <summary>Gets the stable failure category.</summary>
    public CheckPointGraphRecipeSelectionFailureCategory Category { get; }
}

/// <summary>Stable graph-only recipe selection failure categories.</summary>
public enum CheckPointGraphRecipeSelectionFailureCategory
{
    NoMatchingRecipe,
    InvalidRecipe
}

/// <summary>Provides the generic end-of-initial-layer Check Point recipe.</summary>
public sealed class CheckPointGraphPositioningRecipeCatalog : ICheckPointGraphPositioningRecipeCatalog
{
    /// <inheritdoc />
    public bool SupportsAlgorithm(ProvisioningPositioningAlgorithm positioningAlgorithm)
    {
        return positioningAlgorithm == ProvisioningPositioningAlgorithm.DefaultEndOfRulebase;
    }

    /// <inheritdoc />
    public CheckPointGraphRecipeSelectionResult SelectRecipe(
        ProvisioningPositioningAlgorithm positioningAlgorithm)
    {
        if (!SupportsAlgorithm(positioningAlgorithm))
        {
            return new CheckPointGraphRecipeSelectionFailure(
                CheckPointGraphRecipeSelectionFailureCategory.NoMatchingRecipe);
        }

        return new CheckPointGraphRecipeSelectionSuccess(
            new PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>(
                new EndOfBlock(
                    new CheckPointLayerScopeBlock(CheckPointLayerSelector.InitialLayer()))));
    }
}
