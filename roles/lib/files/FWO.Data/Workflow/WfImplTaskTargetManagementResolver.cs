namespace FWO.Data.Workflow;

/// <summary>
/// Resolves the effective target management of an implementation task.
/// </summary>
public static class WfImplTaskTargetManagementResolver
{
    /// <summary>
    /// Tries to resolve one positive target management ID from the implementation target.
    /// </summary>
    /// <param name="requestTask">The optional parent request task.</param>
    /// <param name="implementationTask">The implementation task whose target is resolved.</param>
    /// <param name="gateway">The gateway already loaded for a gateway-bound task.</param>
    /// <param name="managementId">The resolved positive management ID, or zero on failure.</param>
    /// <returns>True when a consistent positive target management ID was resolved.</returns>
    public static bool TryResolveManagementId(
        WfReqTask? requestTask,
        WfImplTask implementationTask,
        Device? gateway,
        out int managementId)
    {
        managementId = 0;

        if (implementationTask.DeviceId is > 0)
        {
            if (gateway?.Id != implementationTask.DeviceId || gateway.Management?.Id is not > 0)
            {
                return false;
            }

            int gatewayManagementId = gateway.Management.Id;
            if (implementationTask.ManagementId.HasValue &&
                implementationTask.ManagementId.Value != gatewayManagementId)
            {
                return false;
            }

            managementId = gatewayManagementId;
            return true;
        }

        int? fallbackManagementId = implementationTask.ManagementId ?? requestTask?.ManagementId;
        if (fallbackManagementId is not > 0)
        {
            return false;
        }

        managementId = fallbackManagementId.Value;
        return true;
    }
}
