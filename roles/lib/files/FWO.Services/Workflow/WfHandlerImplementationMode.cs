using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Applies the device specific settings implementationMode (objects) and implementationModeOverrideForRules
    /// (rules) to the creation of implementation tasks: only targets with implementation mode Automatic get one.
    /// Manual gets none. External ticket system gets none either, the request task is handed to that system instead.
    /// </summary>
    public partial class WfHandler
    {
        private const string kImplementationModeLogTitle = "Implementation Mode";
        private const string kAlertSource = "workflow";

        /// <summary>
        /// Returns whether a request task is handed to an external ticket system as a whole, so that it gets no
        /// implementation tasks: the implementation mode of its management is external ticket system. Only rule and
        /// object tasks with a management are handled by an external ticket system, which takes over whole
        /// managements; a task without management, e.g. "All" or from the REST interface, is decided per gateway or
        /// target management instead. Without a selected external ticket system nobody can implement the task, which
        /// is reported with an alert.
        /// </summary>
        /// <param name="reqTask">Request task to check.</param>
        private async Task<bool> IsHandedToExternalTicketSystem(WfReqTask reqTask)
        {
            bool isRuleTask = IsRuleTaskType(reqTask.TaskType);
            if ((!isRuleTask && !WfObjectTaskHelper.IsManagementObjectTask(reqTask.TaskType))
                || reqTask.ManagementId is not int managementId || managementId <= 0)
            {
                return false;
            }
            List<int> managementIds = [managementId];
            await EnsureAutoPlanSettings(kNoIds, managementIds);
            GlobalDeviceSpecificSettings settings = GetResolvedSettings(DeviceSpecificScopeType.Management, managementId);
            if (!settings.UsesExternalTicketSystem(isRuleTask))
            {
                return false;
            }
            if (settings.RerouteToExternalTicketSystem == null)
            {
                await ReportUnimplementedTarget(reqTask, managementId, null, NoExternalTicketSystemSelectedReason(managementId));
            }
            else
            {
                Log.WriteInfo(kImplementationModeLogTitle, $"No implementation task created for request task {reqTask.Id}: it is handed to an external ticket system.");
            }
            return true;
        }

        /// <summary>
        /// Keeps the gateways of a rule task whose implementation mode for rules is Automatic. A null entry stands for
        /// one implementation task without gateway and follows the global settings. A gateway set to external ticket
        /// system is left to that system when the ticket hands a rule task of the gateway's management to it;
        /// otherwise nobody implements it yet, see <see cref="HandleExternalTicketSystemGatewayWithoutRequestTask"/>.
        /// </summary>
        /// <param name="reqTask">Rule task the gateways were determined for.</param>
        /// <param name="targets">Gateways determined for the rule task.</param>
        private async Task<List<int?>> FilterAutomaticRuleTargets(WfReqTask reqTask, List<int?> targets)
        {
            List<int> deviceIds = [.. targets.OfType<int>()];
            if (deviceIds.Count > 0)
            {
                await EnsureDevicesLoaded();
            }
            List<int> gatewayManagementIds = [.. deviceIds.Select(GetManagementIdOfDevice).OfType<int>().Distinct()];
            await EnsureAutoPlanSettings(deviceIds, gatewayManagementIds);
            List<int?> automaticTargets = [];
            foreach (int? deviceId in targets)
            {
                ProvisioningImplementationMode mode = GetResolvedSettings(DeviceSpecificScopeType.Gateway, deviceId).GetImplementationMode(true);
                if (mode == ProvisioningImplementationMode.Automatic)
                {
                    automaticTargets.Add(deviceId);
                }
                else if (mode == ProvisioningImplementationMode.RerouteToExternalTicketSystem)
                {
                    await HandleExternalTicketSystemGateway(reqTask, deviceId);
                }
                else
                {
                    Log.WriteDebug(kImplementationModeLogTitle, $"No implementation task created for request task {reqTask.Id} on gateway {deviceId}: implementation mode manual.");
                }
            }
            return automaticTargets;
        }

        /// <summary>
        /// Keeps the target managements of an object task whose implementation mode is Automatic. A target set to
        /// external ticket system is left to that system when the ticket hands an object task of that management to
        /// it; otherwise nobody implements it, which is reported with an alert like for gateways.
        /// </summary>
        /// <param name="reqTask">Object task the targets were determined for.</param>
        /// <param name="targets">Target managements of the object task.</param>
        private async Task<List<Management>> FilterAutomaticObjectTargets(WfReqTask reqTask, List<Management> targets)
        {
            List<int> targetManagementIds = [.. targets.Select(target => target.Id)];
            await EnsureAutoPlanSettings(kNoIds, targetManagementIds);
            List<Management> automaticTargets = [];
            foreach (Management target in targets)
            {
                ProvisioningImplementationMode mode = GetResolvedSettings(DeviceSpecificScopeType.Management, target.Id).GetImplementationMode(false);
                if (mode == ProvisioningImplementationMode.Automatic)
                {
                    automaticTargets.Add(target);
                }
                else if (mode == ProvisioningImplementationMode.RerouteToExternalTicketSystem && !IsHandedToExternalTicketSystemByTicket(target.Id, false))
                {
                    await ReportUnimplementedTarget(reqTask, target.Id, null, GetMissingExternalTicketSystemReason(target.Id)
                        ?? $"target management {target.Id} is set to external ticket system, but the ticket hands no object task of this management to it.");
                }
                else
                {
                    Log.WriteDebug(kImplementationModeLogTitle, $"No implementation task created for request task {reqTask.Id} on management {target.Id}: " +
                        "implementation mode manual or handed to an external ticket system.");
                }
            }
            return automaticTargets;
        }

        private async Task HandleExternalTicketSystemGateway(WfReqTask reqTask, int? deviceId)
        {
            int? managementId = deviceId is int id ? GetManagementIdOfDevice(id) : null;
            if (managementId is int gatewayManagementId && IsHandedToExternalTicketSystemByTicket(gatewayManagementId, true))
            {
                Log.WriteDebug(kImplementationModeLogTitle, $"No implementation task created for request task {reqTask.Id} on gateway {deviceId}: " +
                    $"the ticket hands management {gatewayManagementId} to an external ticket system.");
                return;
            }
            await HandleExternalTicketSystemGatewayWithoutRequestTask(reqTask, deviceId, managementId);
        }

        /// <summary>
        /// Returns whether the ticket hands the rules resp. objects of a management to an external ticket system: it
        /// contains a rule resp. object or group task of the management, whose implementation mode for that kind of
        /// task is external ticket system with a selected system.
        /// </summary>
        /// <param name="managementId">Management of the gateway or object target.</param>
        /// <param name="isRuleTask">True for rules, false for objects and groups.</param>
        private bool IsHandedToExternalTicketSystemByTicket(int managementId, bool isRuleTask)
        {
            GlobalDeviceSpecificSettings settings = GetResolvedSettings(DeviceSpecificScopeType.Management, managementId);
            return settings.UsesExternalTicketSystem(isRuleTask) && settings.RerouteToExternalTicketSystem != null
                && ActTicket.Tasks.Any(task => task.ManagementId == managementId
                    && (isRuleTask ? IsRuleTaskType(task.TaskType) : WfObjectTaskHelper.IsManagementObjectTask(task.TaskType)));
        }

        /// <summary>
        /// Placeholder, not implemented yet: a gateway found for a rule task is set to external ticket system, but the
        /// ticket hands no rule task of the gateway's management to that system, e.g. a ticket ordered through the
        /// REST interface or entered manually. No implementation task is created, as the gateway is not to be written
        /// by the workflow, and the gap is reported with an alert. Which solution replaces this placeholder is open:
        /// an additional request task for the gateway's management handed to the external ticket system, or an
        /// implementation task processed manually.
        /// </summary>
        /// <param name="reqTask">Rule task the gateway was found for.</param>
        /// <param name="deviceId">The gateway; null for one task without gateway.</param>
        /// <param name="managementId">Management of the gateway, if known.</param>
        private async Task HandleExternalTicketSystemGatewayWithoutRequestTask(WfReqTask reqTask, int? deviceId, int? managementId)
        {
            string? missingSystemReason = managementId is int id ? GetMissingExternalTicketSystemReason(id) : null;
            await ReportUnimplementedTarget(reqTask, managementId, deviceId, missingSystemReason ??
                $"gateway {deviceId?.ToString() ?? "(none)"} of management {managementId?.ToString() ?? "(unknown)"} is set to external ticket system, " +
                "but the ticket hands no rule task of this management to it (not implemented yet).");
        }

        /// <summary>
        /// Returns the alert reason for a management without a selected external ticket system, so that a target set to
        /// external ticket system points to the missing system instead of to the ticket; null if a system is selected.
        /// </summary>
        /// <param name="managementId">Management of the gateway or object target.</param>
        private string? GetMissingExternalTicketSystemReason(int managementId)
        {
            return GetResolvedSettings(DeviceSpecificScopeType.Management, managementId).RerouteToExternalTicketSystem == null
                ? NoExternalTicketSystemSelectedReason(managementId)
                : null;
        }

        private static string NoExternalTicketSystemSelectedReason(int managementId)
        {
            return $"management {managementId} is set to external ticket system, but no external ticket system is selected for it.";
        }

        /// <summary>
        /// Logs a target that gets no implementation task while nobody else implements it, and raises a workflow
        /// alert for it.
        /// </summary>
        private async Task ReportUnimplementedTarget(WfReqTask reqTask, int? managementId, int? deviceId, string reason)
        {
            string description = $"Ticket {reqTask.TicketId}, request task {reqTask.Id}: no implementation task created, {reason}";
            Log.WriteWarning(kImplementationModeLogTitle, description);
            if (apiConnection == null)
            {
                return;
            }
            await AlertHelper.SetAlert(apiConnection, kImplementationModeLogTitle, description, kAlertSource, AlertCode.WorkflowAlert,
                new AlertHelper.AdditionalAlertData { MgmtId = managementId, DevId = deviceId, CompareDesc = true });
        }

        private int? GetManagementIdOfDevice(int deviceId)
        {
            int? managementId = Devices.FirstOrDefault(device => device.Id == deviceId)?.Management.Id;
            return managementId > 0 ? managementId : null;
        }

        /// <summary>
        /// Returns the effective settings of a level resolved in the current creation run. A level that was not
        /// resolved inherits the global settings; without any resolved settings the compiled defaults apply.
        /// </summary>
        private GlobalDeviceSpecificSettings GetResolvedSettings(DeviceSpecificScopeType scopeType, int? id)
        {
            if (AutoPlanSettingsOverride != null)
            {
                return AutoPlanSettingsOverride;
            }
            if (id != null && autoPlanCache.TryGetValue((scopeType, id), out GlobalDeviceSpecificSettings? settings))
            {
                return settings;
            }
            if (autoPlanCache.TryGetValue((DeviceSpecificScopeType.Global, null), out GlobalDeviceSpecificSettings? globalSettings))
            {
                return globalSettings;
            }
            return kDefaultAutoPlanSettings;
        }
    }
}
