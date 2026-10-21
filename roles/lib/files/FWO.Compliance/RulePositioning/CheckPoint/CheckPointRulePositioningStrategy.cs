using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;

namespace FWO.Compliance.RulePositioning.CheckPoint;

/// <summary>Orchestrates one inactive Check Point rule-positioning calculation.</summary>
public sealed class CheckPointRulePositioningStrategy : IRulePositioningStrategy
{
    private const int kCheckPointR8xDeviceTypeId = 9;

    private readonly IRulebaseGraphSnapshotReader graphReader;
    private readonly IDesignatedZoneMatrixReader zoneMatrixReader;
    private readonly ICreationRuleZoneResolver zoneResolver;
    private readonly ICheckPointPositioningRecipeCatalog recipeCatalog;
    private readonly PositioningRecipeExecutor<CheckPointPositioningContext, CheckPointBlockValue> recipeExecutor = new();

    /// <summary>Creates the Check Point positioning orchestration strategy.</summary>
    public CheckPointRulePositioningStrategy(
        IRulebaseGraphSnapshotReader graphReader,
        IDesignatedZoneMatrixReader zoneMatrixReader,
        ICreationRuleZoneResolver zoneResolver,
        ICheckPointPositioningRecipeCatalog recipeCatalog)
    {
        this.graphReader = graphReader ?? throw new ArgumentNullException(nameof(graphReader));
        this.zoneMatrixReader = zoneMatrixReader ?? throw new ArgumentNullException(nameof(zoneMatrixReader));
        this.zoneResolver = zoneResolver ?? throw new ArgumentNullException(nameof(zoneResolver));
        this.recipeCatalog = recipeCatalog ?? throw new ArgumentNullException(nameof(recipeCatalog));
    }

    /// <inheritdoc />
    public bool CanHandle(DeviceType deviceType, ProvisioningPositioningAlgorithm positioningAlgorithm)
    {
        return deviceType != null
            && deviceType.Id == kCheckPointR8xDeviceTypeId
            && IsUsableAlgorithm(positioningAlgorithm)
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

            cancellationToken.ThrowIfCancellationRequested();
            RulebaseGraphReadResult graphResult = await graphReader.ReadAsync(
                context.TargetManagement.Id,
                context.TargetGateway.Id,
                cancellationToken);
            if (graphResult is not RulebaseGraphReadSuccess graphSuccess)
            {
                return MapGraphFailure(graphResult);
            }

            cancellationToken.ThrowIfCancellationRequested();
            DesignatedZoneMatrixReadResult matrixResult = await zoneMatrixReader.ReadAsync(cancellationToken);
            if (matrixResult is not DesignatedZoneMatrixReadSuccess matrixSuccess)
            {
                return MapMatrixFailure(matrixResult);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CreationRuleZoneResolutionResult zoneResult = await zoneResolver.ResolveAsync(
                context.ParentRequestTask,
                matrixSuccess.NetworkZones,
                cancellationToken);
            if (zoneResult is not CreationRuleZoneResolutionSuccess zoneSuccess)
            {
                return MapZoneFailure(zoneResult);
            }

            CheckPointRecipeSelectionResult selectionResult = recipeCatalog.SelectRecipe(algorithm, zoneSuccess.Input);
            if (selectionResult is not CheckPointRecipeSelectionSuccess selectionSuccess)
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

    /// <summary>Validates the minimum context required for safe direct invocation.</summary>
    private static RulePositioningFailure? ValidateContext(RulePositioningContext context)
    {
        if (context == null
            || context.TargetGateway == null
            || context.TargetGateway.DeviceType == null
            || context.TargetManagement == null
            || context.ParentRequestTask == null
            || context.EffectiveSettings == null
            || context.TargetManagement.Id <= 0
            || context.TargetGateway.Id <= 0)
        {
            return Failure(RulePositioningFailureCategory.InvalidInput);
        }

        return IsUsableAlgorithm(context.EffectiveSettings.PositioningAlgorithm)
            ? null
            : Failure(RulePositioningFailureCategory.ConfigurationInvalid);
    }

    /// <summary>Checks whether an algorithm is defined and non-default.</summary>
    private static bool IsUsableAlgorithm(ProvisioningPositioningAlgorithm algorithm)
    {
        return algorithm != ProvisioningPositioningAlgorithm.Undefined && Enum.IsDefined(algorithm);
    }

    /// <summary>Maps graph-reader failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapGraphFailure(RulebaseGraphReadResult result)
    {
        RulePositioningFailureCategory category = result is RulebaseGraphReadFailure failure
            && failure.Category is RulebaseGraphReadFailureCategory.InvalidInput or RulebaseGraphReadFailureCategory.ScopeMismatch
                ? RulePositioningFailureCategory.InvalidInput
                : RulePositioningFailureCategory.CalculationFailed;
        return Failure(category);
    }

    /// <summary>Maps designated-matrix-reader failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapMatrixFailure(DesignatedZoneMatrixReadResult result)
    {
        RulePositioningFailureCategory category = result is DesignatedZoneMatrixReadFailure failure
            && failure.Category is DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid
                or DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable
                ? RulePositioningFailureCategory.ConfigurationInvalid
                : RulePositioningFailureCategory.CalculationFailed;
        return Failure(category);
    }

    /// <summary>Maps zone-resolution failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapZoneFailure(CreationRuleZoneResolutionResult result)
    {
        RulePositioningFailureCategory category = result is CreationRuleZoneResolutionFailure failure
            ? failure.Category switch
            {
                CreationRuleZoneResolutionFailureCategory.InvalidInput
                    or CreationRuleZoneResolutionFailureCategory.UnsupportedObject
                    or CreationRuleZoneResolutionFailureCategory.InvalidAddress => RulePositioningFailureCategory.InvalidInput,
                CreationRuleZoneResolutionFailureCategory.UnsupportedTask => RulePositioningFailureCategory.UnsupportedTask,
                CreationRuleZoneResolutionFailureCategory.InvalidZoneData => RulePositioningFailureCategory.ConfigurationInvalid,
                _ => RulePositioningFailureCategory.CalculationFailed
            }
            : RulePositioningFailureCategory.CalculationFailed;
        return Failure(category);
    }

    /// <summary>Maps recipe-selection failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapSelectionFailure(CheckPointRecipeSelectionResult result)
    {
        RulePositioningFailureCategory category = result is CheckPointRecipeSelectionFailure failure
            ? failure.Category == CheckPointRecipeSelectionFailureCategory.NoMatchingRecipe
                ? RulePositioningFailureCategory.PositioningMethodNotImplemented
                : RulePositioningFailureCategory.ConfigurationInvalid
            : RulePositioningFailureCategory.CalculationFailed;
        return Failure(category);
    }

    /// <summary>Maps recipe-execution failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapExecutionFailure(PositioningBlockResult<CheckPointBlockValue> result)
    {
        RulePositioningFailureCategory category = result is PositioningBlockFailure<CheckPointBlockValue> failure
            && failure.Category == PositioningBlockFailureCategory.InvalidRecipe
                ? RulePositioningFailureCategory.ConfigurationInvalid
                : RulePositioningFailureCategory.CalculationFailed;
        return Failure(category);
    }

    /// <summary>Creates a Phase 1 failure with its fixed safe diagnostic.</summary>
    private static RulePositioningFailure Failure(RulePositioningFailureCategory category)
    {
        return new RulePositioningFailure(category);
    }
}
