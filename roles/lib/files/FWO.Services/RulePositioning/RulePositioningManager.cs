using System.Diagnostics;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;

namespace FWO.Services.RulePositioning;

/// <summary>
/// Validates rule-positioning input and selects exactly one registered strategy.
/// </summary>
public sealed class RulePositioningManager
{
    private readonly IReadOnlyList<IRulePositioningStrategy> strategies;

    /// <summary>
    /// Creates a manager from the available strategies. An empty collection is valid.
    /// </summary>
    public RulePositioningManager(IEnumerable<IRulePositioningStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        this.strategies = strategies.ToList();
    }

    /// <summary>
    /// Validates the supplied context and calculates a position using one exact strategy match.
    /// </summary>
    public async Task<RulePositioningResult> CalculateAsync(
        RulePositioningContext context,
        CancellationToken cancellationToken)
    {
        RulePositioningFailureCategory? validationFailure =
            RulePositioningContextValidator.ValidateManagerContext(context);
        RulePositioningDiagnosticLog.WriteInfo(
            "manager.validation",
            validationFailure.HasValue ? "failure" : "success",
            Facts(
                ("registered_strategy_count", strategies.Count),
                ("registered_strategy_types", strategies.Select(StrategyType).ToList()),
                ("failure_category", validationFailure?.ToString() ?? "None")));
        if (validationFailure.HasValue)
        {
            RulePositioningDiagnosticLog.WriteFailure(
                "manager.validation",
                Facts(("failure_category", validationFailure.Value.ToString())));
            return Failure(validationFailure.Value);
        }

        List<IRulePositioningStrategy> matchingStrategies = [];
        try
        {
            foreach (IRulePositioningStrategy strategy in strategies)
            {
                bool canHandle = strategy.CanHandle(
                    context.TargetGateway.DeviceType,
                    context.EffectiveSettings.PositioningAlgorithm);
                RulePositioningDiagnosticLog.WriteInfo(
                    "manager.strategy.capability",
                    "summary",
                    Facts(
                        ("strategy_type", StrategyType(strategy)),
                        ("can_handle", canHandle),
                        ("algorithm", context.EffectiveSettings.PositioningAlgorithm.ToString()),
                        ("device_type_id", context.TargetGateway.DeviceType.Id)));
                if (canHandle)
                {
                    matchingStrategies.Add(strategy);
                }
            }
        }
        catch (Exception exception)
        {
            RulePositioningDiagnosticLog.WriteException("manager.strategy.capability", exception);
            return Failure(RulePositioningFailureCategory.CalculationFailed);
        }

        RulePositioningDiagnosticLog.WriteInfo(
            "manager.strategy.selection",
            matchingStrategies.Count == 1 ? "success" : "failure",
            Facts(
                ("matching_count", matchingStrategies.Count),
                ("selected_type", matchingStrategies.Count == 1 ? StrategyType(matchingStrategies[0]) : "None")));
        if (matchingStrategies.Count == 0)
        {
            return Failure(RulePositioningFailureCategory.PositioningMethodNotImplemented);
        }
        if (matchingStrategies.Count > 1)
        {
            return Failure(RulePositioningFailureCategory.StrategyRegistrationAmbiguous);
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            long started = Stopwatch.GetTimestamp();
            RulePositioningResult? result = await matchingStrategies[0].CalculateAsync(context, cancellationToken);
            RulePositioningDiagnosticLog.WriteInfo(
                "manager.strategy.calculate",
                result is RulePositioningSuccess ? "success" : "failure",
                Facts(
                    ("duration_ms", (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds),
                    ("result_type", result?.GetType().FullName ?? "null"),
                    ("failure_category", (result as RulePositioningFailure)?.Category.ToString() ?? "None"),
                    ("null_result", result == null)));
            return result ?? Failure(RulePositioningFailureCategory.CalculationFailed);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested
                || cancellationToken.CanBeCanceled && exception.CancellationToken == cancellationToken)
        {
            RulePositioningDiagnosticLog.WriteException("manager.strategy.calculate", exception);
            throw;
        }
        catch (Exception exception)
        {
            RulePositioningDiagnosticLog.WriteException("manager.strategy.calculate", exception);
            return Failure(RulePositioningFailureCategory.CalculationFailed);
        }
    }

    /// <summary>
    /// Creates a failure with the contract's safe category-specific diagnostic.
    /// </summary>
    private static RulePositioningFailure Failure(RulePositioningFailureCategory category)
    {
        return new RulePositioningFailure(category);
    }

    /// <summary>Returns a stable CLR type name for registered strategy diagnostics.</summary>
    private static string StrategyType(IRulePositioningStrategy strategy) =>
        strategy.GetType().FullName ?? strategy.GetType().Name;

    /// <summary>Builds one event's bounded fact map.</summary>
    private static Dictionary<string, object?> Facts(params (string Key, object? Value)[] values) =>
        values.ToDictionary(Value => Value.Key, Value => Value.Value);
}
