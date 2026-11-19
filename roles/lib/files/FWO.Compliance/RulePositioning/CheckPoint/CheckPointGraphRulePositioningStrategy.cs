using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;
using RulebaseGraphReadResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Services.RulePositioning.RulebaseGraphReadValue,
    FWO.Services.RulePositioning.RulebaseGraphReadFailureCategory>;
using CheckPointGraphRecipeSelectionResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Services.RulePositioning.Blocks.PositioningRecipe<FWO.Data.RulePositioning.CheckPoint.CheckPointPositioningContext,
        FWO.Services.RulePositioning.CheckPoint.CheckPointBlockValue>,
    FWO.Compliance.RulePositioning.CheckPoint.CheckPointGraphRecipeSelectionFailureCategory>;

namespace FWO.Compliance.RulePositioning.CheckPoint;

/// <summary>Calculates the generic graph-only Check Point rule position.</summary>
public sealed class CheckPointGraphRulePositioningStrategy : IRulePositioningStrategy
{
    private readonly IRulebaseGraphSnapshotReader graphReader;
    private readonly ICheckPointGraphPositioningRecipeCatalog recipeCatalog;
    /// <summary>Executes validated graph-only recipes.</summary>
    private readonly PositioningRecipeExecutor<CheckPointPositioningContext, CheckPointBlockValue> recipeExecutor = new();

    /// <summary>Creates the graph-only Check Point positioning strategy.</summary>
    public CheckPointGraphRulePositioningStrategy(
        IRulebaseGraphSnapshotReader graphReader,
        ICheckPointGraphPositioningRecipeCatalog recipeCatalog)
    {
        this.graphReader = graphReader ?? throw new ArgumentNullException(nameof(graphReader));
        this.recipeCatalog = recipeCatalog ?? throw new ArgumentNullException(nameof(recipeCatalog));
    }

    /// <inheritdoc />
    public bool CanHandle(DeviceType deviceType, ProvisioningPositioningAlgorithm positioningAlgorithm)
    {
        return deviceType != null
            && deviceType.Id == DeviceTypeIds.kCheckPointR8x
            && recipeCatalog.SupportsAlgorithm(positioningAlgorithm);
    }

    /// <inheritdoc />
    public async Task<RulePositioningResult> CalculateAsync(
        RulePositioningContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RulePositioningFailureCategory? validationFailure =
                RulePositioningContextValidator.ValidateGraphContext(context);
            if (validationFailure.HasValue)
            {
                return Failure(validationFailure.Value);
            }

            ProvisioningPositioningAlgorithm algorithm = context.EffectiveSettings.PositioningAlgorithm;
            if (!CanHandle(context.TargetGateway.DeviceType, algorithm))
            {
                return Failure(RulePositioningFailureCategory.PositioningMethodNotImplemented);
            }

            RulebaseGraphReadResult graphResult = await graphReader.ReadAsync(
                context.TargetManagement.Id,
                context.TargetGateway.Id,
                cancellationToken);
            if (!graphResult.TryGetValue(out RulebaseGraphReadValue? graphValue))
            {
                return MapGraphFailure(graphResult);
            }

            CheckPointGraphRecipeSelectionResult selectionResult = recipeCatalog.SelectRecipe(algorithm);
            if (!selectionResult.TryGetValue(out PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>? recipe))
            {
                return MapSelectionFailure(selectionResult);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CheckPointPositioningContext checkPointContext = new(context, graphValue.Snapshot);
            PositioningBlockResult<CheckPointBlockValue> executionResult = await recipeExecutor.ExecuteAsync(
                recipe,
                checkPointContext,
                cancellationToken);
            if (executionResult is not PositioningBlockSuccess<CheckPointBlockValue> executionSuccess)
            {
                return CheckPointRulePositioningFailureMapper.MapExecutionFailure(
                    "graph recipe execution",
                    executionResult);
            }
            if (executionSuccess.Value is not CheckPointPositionValue positionValue
                || positionValue.Position.PositioningAlgorithm != algorithm)
            {
                return CheckPointRulePositioningFailureMapper.KnownFailure(
                    "graph recipe execution result",
                    RulePositioningFailureCategory.CalculationFailed,
                    CheckPointStrategyFailureCategory.UnexpectedPositionResult,
                    "The positioning recipe returned an unexpected result.");
            }

            return new RulePositioningSuccess(positionValue.Position);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested
                || cancellationToken.CanBeCanceled && exception.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (Exception)
        {
            return Failure(RulePositioningFailureCategory.CalculationFailed);
        }
    }

    /// <summary>Maps graph-reader failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapGraphFailure(RulebaseGraphReadResult result)
    {
        RulebaseGraphReadFailureCategory failureCategory = result.FailureCategory!.Value;
        RulePositioningFailureCategory category = failureCategory is
            RulebaseGraphReadFailureCategory.InvalidInput or RulebaseGraphReadFailureCategory.ScopeMismatch
                ? RulePositioningFailureCategory.InvalidInput
                : RulePositioningFailureCategory.CalculationFailed;
        return CheckPointRulePositioningFailureMapper.KnownFailure(
            "rulebase graph",
            category,
            failureCategory,
            failureCategory.GetDiagnostic());
    }

    /// <summary>Maps graph-only recipe-selection failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapSelectionFailure(CheckPointGraphRecipeSelectionResult result)
    {
        CheckPointGraphRecipeSelectionFailureCategory failureCategory = result.FailureCategory!.Value;
        RulePositioningFailureCategory category = failureCategory ==
            CheckPointGraphRecipeSelectionFailureCategory.NoMatchingRecipe
            ? RulePositioningFailureCategory.PositioningMethodNotImplemented
            : RulePositioningFailureCategory.ConfigurationInvalid;
        return CheckPointRulePositioningFailureMapper.KnownFailure(
            "graph recipe selection",
            category,
            failureCategory,
            SelectionDiagnostic(failureCategory));
    }

    /// <summary>Creates a safe typed failure.</summary>
    private static RulePositioningFailure Failure(RulePositioningFailureCategory category)
    {
        return new RulePositioningFailure(category);
    }

    /// <summary>Maps graph recipe-selection failures to fixed safe diagnostics.</summary>
    private static string SelectionDiagnostic(CheckPointGraphRecipeSelectionFailureCategory category) =>
        category == CheckPointGraphRecipeSelectionFailureCategory.NoMatchingRecipe
            ? "No graph-only Check Point positioning recipe matches the selected algorithm."
            : "The graph-only Check Point positioning recipe is invalid.";

}
