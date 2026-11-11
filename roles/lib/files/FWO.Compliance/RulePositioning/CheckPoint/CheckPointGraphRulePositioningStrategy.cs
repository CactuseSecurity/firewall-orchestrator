using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;
using System.Diagnostics;
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
        bool supported = deviceType != null
            && deviceType.Id == DeviceTypeIds.kCheckPointR8x
            && recipeCatalog.SupportsAlgorithm(positioningAlgorithm);
        RulePositioningDiagnosticLog.WriteInfo("recipe.capability", "summary",
            new Dictionary<string, object?>
            {
                ["strategy"] = nameof(CheckPointGraphRulePositioningStrategy),
                ["device_type_id"] = deviceType?.Id,
                ["algorithm"] = positioningAlgorithm.ToString(),
                ["supported"] = supported
            });
        return supported;
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
                return LoggedMapping("validation", validationFailure.Value.ToString(), validationFailure.Value, null, 0);
            }

            ProvisioningPositioningAlgorithm algorithm = context.EffectiveSettings.PositioningAlgorithm;
            if (!CanHandle(context.TargetGateway.DeviceType, algorithm))
            {
                return LoggedMapping("capability", "unsupported",
                    RulePositioningFailureCategory.PositioningMethodNotImplemented, algorithm, 0);
            }

            Stopwatch operation = Stopwatch.StartNew();
            RulebaseGraphReadResult graphResult = await graphReader.ReadAsync(
                context.TargetManagement.Id,
                context.TargetGateway.Id,
                cancellationToken);
            if (!graphResult.TryGetValue(out RulebaseGraphReadValue? graphValue))
            {
                return MapGraphFailure(graphResult, algorithm, operation.ElapsedMilliseconds);
            }

            operation.Restart();
            CheckPointGraphRecipeSelectionResult selectionResult = recipeCatalog.SelectRecipe(algorithm);
            if (!selectionResult.TryGetValue(out PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>? recipe))
            {
                return MapSelectionFailure(selectionResult, algorithm, operation.ElapsedMilliseconds);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CheckPointPositioningContext checkPointContext = new(context, graphValue.Snapshot);
            operation.Restart();
            PositioningBlockResult<CheckPointBlockValue> executionResult = await recipeExecutor.ExecuteAsync(
                recipe,
                checkPointContext,
                cancellationToken);
            if (executionResult is not PositioningBlockSuccess<CheckPointBlockValue> executionSuccess)
            {
                return MapExecutionFailure(executionResult, algorithm, operation.ElapsedMilliseconds);
            }
            if (executionSuccess.Value is not CheckPointPositionValue positionValue
                || positionValue.Position.PositioningAlgorithm != algorithm)
            {
                return LoggedMapping("execution.result", executionSuccess.Value.GetType().Name,
                    RulePositioningFailureCategory.CalculationFailed, algorithm, operation.ElapsedMilliseconds);
            }

            WritePositionSuccess(positionValue.Position, algorithm);
            return new RulePositioningSuccess(positionValue.Position);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested
                || cancellationToken.CanBeCanceled && exception.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (Exception exception)
        {
            RulePositioningDiagnosticLog.WriteException("strategy.exception", exception, new Dictionary<string, object?>
            {
                ["strategy"] = nameof(CheckPointGraphRulePositioningStrategy),
                ["public_category"] = RulePositioningFailureCategory.CalculationFailed.ToString()
            });
            return Failure(RulePositioningFailureCategory.CalculationFailed);
        }
    }

    /// <summary>Maps graph-reader failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapGraphFailure(
        RulebaseGraphReadResult result,
        ProvisioningPositioningAlgorithm algorithm,
        long durationMs)
    {
        RulePositioningFailureCategory category = result.TryGetFailure(out RulebaseGraphReadFailureCategory failureCategory)
            && failureCategory is RulebaseGraphReadFailureCategory.InvalidInput or RulebaseGraphReadFailureCategory.ScopeMismatch
                ? RulePositioningFailureCategory.InvalidInput
                : RulePositioningFailureCategory.CalculationFailed;
        result.TryGetFailure(out RulebaseGraphReadFailureCategory internalCategory);
        return LoggedMapping("graph", internalCategory.ToString(), category, algorithm, durationMs);
    }

    /// <summary>Maps graph-only recipe-selection failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapSelectionFailure(
        CheckPointGraphRecipeSelectionResult result,
        ProvisioningPositioningAlgorithm algorithm,
        long durationMs)
    {
        RulePositioningFailureCategory category = result.TryGetFailure(
            out CheckPointGraphRecipeSelectionFailureCategory failureCategory)
            && failureCategory == CheckPointGraphRecipeSelectionFailureCategory.NoMatchingRecipe
                ? RulePositioningFailureCategory.PositioningMethodNotImplemented
                : RulePositioningFailureCategory.ConfigurationInvalid;
        result.TryGetFailure(out CheckPointGraphRecipeSelectionFailureCategory internalCategory);
        return LoggedMapping("recipe.selection", internalCategory.ToString(), category, algorithm, durationMs);
    }

    /// <summary>Maps block failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapExecutionFailure(
        PositioningBlockResult<CheckPointBlockValue> result,
        ProvisioningPositioningAlgorithm algorithm,
        long durationMs)
    {
        RulePositioningFailureCategory category = result is PositioningBlockFailure<CheckPointBlockValue> failure
            && failure.Category == PositioningBlockFailureCategory.InvalidRecipe
                ? RulePositioningFailureCategory.ConfigurationInvalid
                : RulePositioningFailureCategory.CalculationFailed;
        string internalCategory = result is PositioningBlockFailure<CheckPointBlockValue> blockFailure
            ? blockFailure.Category.ToString()
            : "UnexpectedResultType";
        return LoggedMapping("recipe.execution", internalCategory, category, algorithm, durationMs);
    }

    /// <summary>Creates a safe typed failure.</summary>
    private static RulePositioningFailure Failure(RulePositioningFailureCategory category)
    {
        return new RulePositioningFailure(category);
    }

    private static RulePositioningFailure LoggedMapping(
        string component,
        string internalCategory,
        RulePositioningFailureCategory publicCategory,
        ProvisioningPositioningAlgorithm? algorithm,
        long durationMs)
    {
        RulePositioningDiagnosticLog.WriteFailure("strategy.mapping", new Dictionary<string, object?>
        {
            ["strategy"] = nameof(CheckPointGraphRulePositioningStrategy),
            ["component"] = component,
            ["internal_category"] = internalCategory,
            ["public_category"] = publicCategory.ToString(),
            ["algorithm"] = algorithm?.ToString(),
            ["duration_ms"] = durationMs
        });
        return Failure(publicCategory);
    }

    private static void WritePositionSuccess(
        CheckPointRulePosition position,
        ProvisioningPositioningAlgorithm algorithm)
    {
        RulePositioningDiagnosticLog.WriteInfo("strategy.position", "success", new Dictionary<string, object?>
        {
            ["strategy"] = nameof(CheckPointGraphRulePositioningStrategy),
            ["algorithm"] = algorithm.ToString(),
            ["anchor_kind"] = position.AnchorKind.ToString(),
            ["layer_uid"] = RulePositioningDiagnosticLog.Fingerprint(position.LayerUid),
            ["anchor_uid"] = RulePositioningDiagnosticLog.Fingerprint(position.AnchorRuleUid)
        });
    }
}
