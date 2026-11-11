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
                return MapExecutionFailure(executionResult);
            }
            if (executionSuccess.Value is not CheckPointPositionValue positionValue
                || positionValue.Position.PositioningAlgorithm != algorithm)
            {
                return Failure(RulePositioningFailureCategory.CalculationFailed);
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
        RulePositioningFailureCategory category = result.TryGetFailure(out RulebaseGraphReadFailureCategory failureCategory)
            && failureCategory is RulebaseGraphReadFailureCategory.InvalidInput or RulebaseGraphReadFailureCategory.ScopeMismatch
                ? RulePositioningFailureCategory.InvalidInput
                : RulePositioningFailureCategory.CalculationFailed;
        return Failure(category);
    }

    /// <summary>Maps graph-only recipe-selection failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapSelectionFailure(CheckPointGraphRecipeSelectionResult result)
    {
        RulePositioningFailureCategory category = result.TryGetFailure(
            out CheckPointGraphRecipeSelectionFailureCategory failureCategory)
            && failureCategory == CheckPointGraphRecipeSelectionFailureCategory.NoMatchingRecipe
                ? RulePositioningFailureCategory.PositioningMethodNotImplemented
                : RulePositioningFailureCategory.ConfigurationInvalid;
        return Failure(category);
    }

    /// <summary>Maps block failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapExecutionFailure(PositioningBlockResult<CheckPointBlockValue> result)
    {
        RulePositioningFailureCategory category = result is PositioningBlockFailure<CheckPointBlockValue> failure
            && failure.Category == PositioningBlockFailureCategory.InvalidRecipe
                ? RulePositioningFailureCategory.ConfigurationInvalid
                : RulePositioningFailureCategory.CalculationFailed;
        return Failure(category);
    }

    /// <summary>Creates a safe typed failure.</summary>
    private static RulePositioningFailure Failure(RulePositioningFailureCategory category)
    {
        return new RulePositioningFailure(category);
    }
}
