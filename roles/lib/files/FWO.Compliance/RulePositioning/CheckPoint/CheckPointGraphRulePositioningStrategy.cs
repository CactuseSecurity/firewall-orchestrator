using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;

namespace FWO.Compliance.RulePositioning.CheckPoint;

/// <summary>Calculates the generic graph-only Check Point rule position.</summary>
public sealed class CheckPointGraphRulePositioningStrategy : IRulePositioningStrategy
{
    private const int kCheckPointR8xDeviceTypeId = 9;

    private readonly IRulebaseGraphSnapshotReader graphReader;
    private readonly ICheckPointGraphPositioningRecipeCatalog recipeCatalog;
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
            && deviceType.Id == kCheckPointR8xDeviceTypeId
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
            RulePositioningFailure? validationFailure = ValidateContext(context);
            if (validationFailure != null)
            {
                return validationFailure;
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
            if (graphResult is not RulebaseGraphReadSuccess graphSuccess)
            {
                return MapGraphFailure(graphResult);
            }

            CheckPointGraphRecipeSelectionResult selectionResult = recipeCatalog.SelectRecipe(algorithm);
            if (selectionResult is not CheckPointGraphRecipeSelectionSuccess selectionSuccess)
            {
                return MapSelectionFailure(selectionResult);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CheckPointPositioningContext checkPointContext = new(context, graphSuccess.Snapshot);
            PositioningBlockResult<CheckPointBlockValue> executionResult = await recipeExecutor.ExecuteAsync(
                selectionSuccess.Recipe,
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

    /// <summary>Validates inputs before any graph is read.</summary>
    private static RulePositioningFailure? ValidateContext(RulePositioningContext context)
    {
        if (context == null
            || context.ImplementationTask == null
            || context.ParentRequestTask == null
            || context.TargetGateway == null
            || context.TargetGateway.DeviceType == null
            || context.TargetManagement == null
            || context.EffectiveSettings == null
            || context.TargetManagement.Id <= 0
            || context.TargetGateway.Id <= 0)
        {
            return Failure(RulePositioningFailureCategory.InvalidInput);
        }

        ProvisioningPositioningAlgorithm algorithm = context.EffectiveSettings.PositioningAlgorithm;
        return algorithm != ProvisioningPositioningAlgorithm.Undefined && Enum.IsDefined(algorithm)
            ? null
            : Failure(RulePositioningFailureCategory.ConfigurationInvalid);
    }

    /// <summary>Maps graph-reader failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapGraphFailure(RulebaseGraphReadResult result)
    {
        RulePositioningFailureCategory category = result is RulebaseGraphReadFailure failure
            && failure.Category is RulebaseGraphReadFailureCategory.InvalidInput or RulebaseGraphReadFailureCategory.ScopeMismatch
                ? RulePositioningFailureCategory.InvalidInput
                : RulePositioningFailureCategory.CalculationFailed;
        return Failure(category);
    }

    /// <summary>Maps graph-only recipe-selection failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapSelectionFailure(CheckPointGraphRecipeSelectionResult result)
    {
        RulePositioningFailureCategory category = result is CheckPointGraphRecipeSelectionFailure failure
            && failure.Category == CheckPointGraphRecipeSelectionFailureCategory.NoMatchingRecipe
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
