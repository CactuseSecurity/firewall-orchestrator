using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
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
    PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>, CheckPointGraphRecipeSelectionFailureCategory> SelectRecipe(
        ProvisioningPositioningAlgorithm positioningAlgorithm);
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
    public PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>, CheckPointGraphRecipeSelectionFailureCategory> SelectRecipe(
        ProvisioningPositioningAlgorithm positioningAlgorithm)
    {
        if (!SupportsAlgorithm(positioningAlgorithm))
        {
            return PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>,
                CheckPointGraphRecipeSelectionFailureCategory>.Failure(
                    CheckPointGraphRecipeSelectionFailureCategory.NoMatchingRecipe);
        }

        return PositioningResult<PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>,
            CheckPointGraphRecipeSelectionFailureCategory>.Success(
            new PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>(
                new EndOfBlock(
                    new CheckPointLayerScopeBlock(CheckPointLayerSelector.InitialLayer()))));
    }
}
