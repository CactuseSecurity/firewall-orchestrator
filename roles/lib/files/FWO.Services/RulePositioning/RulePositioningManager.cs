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
        if (validationFailure.HasValue)
        {
            return Failure(validationFailure.Value);
        }

        List<IRulePositioningStrategy> matchingStrategies;
        try
        {
            matchingStrategies =
            [
                .. strategies.Where(strategy => strategy.CanHandle(
                    context.TargetGateway.DeviceType,
                    context.EffectiveSettings.PositioningAlgorithm))
            ];
        }
        catch (Exception)
        {
            return Failure(RulePositioningFailureCategory.CalculationFailed);
        }

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
            return await matchingStrategies[0].CalculateAsync(context, cancellationToken)
                ?? Failure(RulePositioningFailureCategory.CalculationFailed);
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

    /// <summary>
    /// Creates a failure with the contract's safe category-specific diagnostic.
    /// </summary>
    private static RulePositioningFailure Failure(RulePositioningFailureCategory category)
    {
        return new RulePositioningFailure(category);
    }
}
