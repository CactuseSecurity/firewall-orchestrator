using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.Workflow;

namespace FWO.Services.RulePositioning;

/// <summary>Central validation rules for workflow and direct rule-positioning entry points.</summary>
public static class RulePositioningContextValidator
{
    /// <summary>Checks task shape and identity before external context is loaded.</summary>
    public static bool IsApplicable(WfReqTask? requestTask, WfImplTask? implementationTask)
    {
        if (!HasValidTaskIdentity(requestTask, implementationTask) ||
            !IsNewAccessRule(implementationTask!, requestTask!))
        {
            return false;
        }
        return HasCompatibleGatewaySelection(requestTask!, implementationTask!.DeviceId!.Value) &&
            HasConsistentRequestManagementScope(requestTask!);
    }

    /// <summary>Validates the complete manager contract, including workflow identity relationships.</summary>
    public static RulePositioningFailureCategory? ValidateManagerContext(RulePositioningContext? context)
    {
        if (context == null
            || context.ImplementationTask == null
            || context.ParentRequestTask == null
            || context.TargetGateway == null
            || context.TargetManagement == null
            || context.TargetGateway.DeviceType == null
            || context.TargetGateway.DeviceType.Id <= 0)
        {
            return RulePositioningFailureCategory.InvalidInput;
        }
        if (!IsNewAccessRule(context.ImplementationTask, context.ParentRequestTask))
        {
            return RulePositioningFailureCategory.UnsupportedTask;
        }
        if (!HasConsistentTaskAndTargetReferences(context))
        {
            return RulePositioningFailureCategory.InvalidInput;
        }
        return HasUsableConfiguration(context.EffectiveSettings)
            ? null
            : RulePositioningFailureCategory.ConfigurationInvalid;
    }

    /// <summary>Validates the inputs required by graph-only direct strategy invocation.</summary>
    public static RulePositioningFailureCategory? ValidateGraphContext(RulePositioningContext? context)
    {
        RulePositioningFailureCategory? common = ValidateDirectContext(context, requireImplementationTask: true);
        return common ?? ValidateAlgorithm(context!.EffectiveSettings);
    }

    /// <summary>Validates the inputs required by ticket-aware direct strategy invocation.</summary>
    public static RulePositioningFailureCategory? ValidateTicketAwareContext(RulePositioningContext? context)
    {
        RulePositioningFailureCategory? common = ValidateDirectContext(context, requireImplementationTask: false);
        if (common != null)
        {
            return common;
        }
        if (context!.TicketRequestTasks == null || context.Configuration == null)
        {
            return RulePositioningFailureCategory.InvalidInput;
        }
        return ValidateAlgorithm(context.EffectiveSettings);
    }

    /// <summary>Validates fields shared by direct strategy entry points.</summary>
    private static RulePositioningFailureCategory? ValidateDirectContext(
        RulePositioningContext? context,
        bool requireImplementationTask)
    {
        return context == null
            || requireImplementationTask && context.ImplementationTask == null
            || context.ParentRequestTask == null
            || context.TargetGateway == null
            || context.TargetGateway.DeviceType == null
            || context.TargetManagement == null
            || context.EffectiveSettings == null
            || context.TargetManagement.Id <= 0
            || context.TargetGateway.Id <= 0
                ? RulePositioningFailureCategory.InvalidInput
                : null;
    }

    /// <summary>Checks that the effective positioning algorithm is configured and supported.</summary>
    private static RulePositioningFailureCategory? ValidateAlgorithm(GatewayDeviceSpecificSettings settings) =>
        HasUsableConfiguration(settings)
            ? null
            : RulePositioningFailureCategory.ConfigurationInvalid;

    /// <summary>Checks that both workflow tasks describe creation of a new access rule.</summary>
    private static bool IsNewAccessRule(WfImplTask implementationTask, WfReqTask requestTask)
    {
        string accessTaskType = WfTaskType.access.ToString();
        string createAction = RequestAction.create.ToString();
        return implementationTask.TaskType == accessTaskType
            && requestTask.TaskType == accessTaskType
            && implementationTask.ImplAction == createAction
            && requestTask.RequestAction == createAction;
    }

    /// <summary>Checks workflow identity, gateway, and management relationships.</summary>
    private static bool HasConsistentTaskAndTargetReferences(RulePositioningContext context)
    {
        if (!HasValidTaskIdentity(context.ParentRequestTask, context.ImplementationTask))
        {
            return false;
        }

        int gatewayId = context.ImplementationTask.DeviceId!.Value;
        if (!HasCompatibleGatewaySelection(context.ParentRequestTask, gatewayId) ||
            context.TargetGateway.Id != gatewayId)
        {
            return false;
        }
        return HasConsistentTargetManagement(context) &&
            HasConsistentRequestManagementScope(context.ParentRequestTask);
    }

    /// <summary>Checks workflow task identifiers and the effective target identifiers.</summary>
    private static bool HasValidTaskIdentity(WfReqTask? requestTask, WfImplTask? implementationTask) =>
        requestTask != null && implementationTask != null &&
        requestTask.Id > 0 && implementationTask.Id > 0 &&
        implementationTask.ReqTaskId == requestTask.Id && implementationTask.DeviceId is > 0;

    /// <summary>Checks an implementation gateway against an optional finite request selection.</summary>
    private static bool HasCompatibleGatewaySelection(WfReqTask requestTask, int gatewayId)
    {
        List<int> requestGatewayIds = [.. requestTask.GetDeviceList().Distinct()];
        bool hasExplicitFiniteGatewaySelection = requestGatewayIds.Count > 0 &&
            !requestGatewayIds.Contains(WfReqTaskBase.kAllDevicesId);
        return !hasExplicitFiniteGatewaySelection || requestGatewayIds.Contains(gatewayId);
    }

    /// <summary>Checks loaded target management against the canonical gateway ownership contract.</summary>
    private static bool HasConsistentTargetManagement(RulePositioningContext context)
    {
        return WfImplTaskTargetManagementResolver.TryResolveManagementId(
                context.ParentRequestTask,
                context.ImplementationTask,
                context.TargetGateway,
                out int managementId) &&
            context.TargetManagement.Id == managementId;
    }

    /// <summary>Checks the optional request management object against its identifier.</summary>
    private static bool HasConsistentRequestManagementScope(WfReqTask requestTask) =>
        requestTask.OnManagement == null || requestTask.OnManagement.Id == requestTask.ManagementId;

    /// <summary>Checks whether the effective settings select a supported positioning algorithm.</summary>
    private static bool HasUsableConfiguration(GatewayDeviceSpecificSettings? settings)
    {
        if (settings == null)
        {
            return false;
        }
        ProvisioningPositioningAlgorithm algorithm = settings.PositioningAlgorithm;
        return algorithm != ProvisioningPositioningAlgorithm.Undefined && Enum.IsDefined(algorithm);
    }
}
