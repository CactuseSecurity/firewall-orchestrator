using FWO.Data.Provisioning;
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
        RulePositioningFailure? validationFailure = ValidateContext(context);
        if (validationFailure != null)
        {
            return validationFailure;
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
    /// Applies validation in a deterministic order before strategies are inspected.
    /// </summary>
    private static RulePositioningFailure? ValidateContext(RulePositioningContext context)
    {
        if (context == null || HasMissingRequiredReference(context))
        {
            return Failure(RulePositioningFailureCategory.InvalidInput);
        }
        if (!IsNewAccessRule(context.ImplementationTask, context.ParentRequestTask))
        {
            return Failure(RulePositioningFailureCategory.UnsupportedTask);
        }
        if (!HasConsistentTaskAndTargetReferences(context))
        {
            return Failure(RulePositioningFailureCategory.InvalidInput);
        }
        if (!HasUsableConfiguration(context))
        {
            return Failure(RulePositioningFailureCategory.ConfigurationInvalid);
        }
        return null;
    }

    /// <summary>
    /// Detects caller-supplied references that are required before semantic validation.
    /// </summary>
    private static bool HasMissingRequiredReference(RulePositioningContext context)
    {
        return context.ImplementationTask == null
            || context.ParentRequestTask == null
            || context.TargetGateway == null
            || context.TargetManagement == null
            || context.TargetGateway.DeviceType == null
            || context.TargetGateway.DeviceType.Id <= 0;
    }

    /// <summary>
    /// Determines whether both workflow tasks represent creation of an access rule.
    /// </summary>
    private static bool IsNewAccessRule(WfImplTask implementationTask, WfReqTask requestTask)
    {
        string accessTaskType = WfTaskType.access.ToString();
        string createAction = RequestAction.create.ToString();
        return implementationTask.TaskType == accessTaskType
            && requestTask.TaskType == accessTaskType
            && implementationTask.ImplAction == createAction
            && requestTask.RequestAction == createAction;
    }

    /// <summary>
    /// Verifies task ownership and the single gateway and management relationships.
    /// </summary>
    private static bool HasConsistentTaskAndTargetReferences(RulePositioningContext context)
    {
        if (context.ParentRequestTask.Id <= 0
            || context.ImplementationTask.ReqTaskId != context.ParentRequestTask.Id)
        {
            return false;
        }

        List<int> requestGatewayIds =
        [
            .. context.ParentRequestTask.GetDeviceList()
                .Distinct()
        ];
        if (requestGatewayIds.Count != 1
            || requestGatewayIds[0] <= 0
            || context.ImplementationTask.DeviceId != requestGatewayIds[0]
            || context.TargetGateway.Id != requestGatewayIds[0])
        {
            return false;
        }

        return HasConsistentManagementReferences(context);
    }

    /// <summary>
    /// Verifies management identity on the request task, gateway, and supplied context.
    /// </summary>
    private static bool HasConsistentManagementReferences(RulePositioningContext context)
    {
        int managementId = context.TargetManagement.Id;
        if (managementId <= 0
            || context.ParentRequestTask.ManagementId != managementId
            || context.TargetGateway.Management == null
            || context.TargetGateway.Management.Id != managementId)
        {
            return false;
        }

        return context.ParentRequestTask.OnManagement == null
            || context.ParentRequestTask.OnManagement.Id == managementId;
    }

    /// <summary>
    /// Determines whether effective settings contain a defined, usable positioning algorithm.
    /// </summary>
    private static bool HasUsableConfiguration(RulePositioningContext context)
    {
        if (context.EffectiveSettings == null)
        {
            return false;
        }

        ProvisioningPositioningAlgorithm algorithm = context.EffectiveSettings.PositioningAlgorithm;
        return algorithm != ProvisioningPositioningAlgorithm.Undefined
            && Enum.IsDefined(algorithm);
    }

    /// <summary>
    /// Creates a failure with the contract's safe category-specific diagnostic.
    /// </summary>
    private static RulePositioningFailure Failure(RulePositioningFailureCategory category)
    {
        return new RulePositioningFailure(category);
    }
}
