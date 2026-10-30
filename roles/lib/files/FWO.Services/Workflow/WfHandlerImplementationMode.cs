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
        /// implementation tasks: the implementation mode of its management (the global one without management) is
        /// external ticket system. Only rule and object tasks are handled by an external ticket system. Without a
        /// selected external ticket system nobody can implement the task, which is reported with an alert.
        /// </summary>
        /// <param name="reqTask">Request task to check.</param>
        private async Task<bool> IsHandedToExternalTicketSystem(WfReqTask reqTask)
        {
            bool isRuleTask = IsRuleTaskType(reqTask.TaskType);
            if (!isRuleTask && !WfObjectTaskHelper.IsManagementObjectTask(reqTask.TaskType))
            {
                return false;
            }
            int? managementId = reqTask.ManagementId > 0 ? reqTask.ManagementId : null;
            List<int> managementIds = managementId is int id ? [id] : [];
            await EnsureAutoPlanSettings(kNoIds, managementIds);
            GlobalDeviceSpecificSettings settings = GetResolvedSettings(DeviceSpecificScopeType.Management, managementId);
            if (!settings.UsesExternalTicketSystem(isRuleTask))
            {
                return false;
            }
            if (settings.RerouteToExternalTicketSystem == null)
            {
                await ReportUnimplementedTarget(reqTask, managementId, null,
                    $"its implementation mode is external ticket system, but no external ticket system is selected for management {managementId?.ToString() ?? "(global)"}.");
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
        /// Keeps the target managements of an object task whose implementation mode is Automatic.
        /// </summary>
        /// <param name="reqTask">Object task the targets were determined for.</param>
        /// <param name="targets">Target managements of the object task.</param>
        private async Task<List<Management>> FilterAutomaticObjectTargets(WfReqTask reqTask, List<Management> targets)
        {
            List<int> targetManagementIds = [.. targets.Select(target => target.Id)];
            await EnsureAutoPlanSettings(kNoIds, targetManagementIds);
            List<Management> automaticTargets = [.. targets.Where(target =>
                GetResolvedSettings(DeviceSpecificScopeType.Management, target.Id).GetImplementationMode(false) == ProvisioningImplementationMode.Automatic)];
            if (automaticTargets.Count < targets.Count)
            {
                Log.WriteDebug(kImplementationModeLogTitle, $"Request task {reqTask.Id}: {targets.Count - automaticTargets.Count} target managements " +
                    "get no implementation task, their implementation mode is manual or external ticket system.");
            }
            return automaticTargets;
        }

        private async Task HandleExternalTicketSystemGateway(WfReqTask reqTask, int? deviceId)
        {
            int? managementId = deviceId is int id ? GetManagementIdOfDevice(id) : null;
            if (managementId is int gatewayManagementId && IsHandedToExternalTicketSystemByTicket(gatewayManagementId))
            {
                Log.WriteDebug(kImplementationModeLogTitle, $"No implementation task created for request task {reqTask.Id} on gateway {deviceId}: " +
                    $"the ticket hands management {gatewayManagementId} to an external ticket system.");
                return;
            }
            await HandleExternalTicketSystemGatewayWithoutRequestTask(reqTask, deviceId, managementId);
        }

        /// <summary>
        /// Returns whether the ticket hands the rules of a management to an external ticket system: it contains a
        /// rule task of the management, whose implementation mode for rules is external ticket system with a selected
        /// system.
        /// </summary>
        private bool IsHandedToExternalTicketSystemByTicket(int managementId)
        {
            GlobalDeviceSpecificSettings settings = GetResolvedSettings(DeviceSpecificScopeType.Management, managementId);
            return settings.UsesExternalTicketSystem(true) && settings.RerouteToExternalTicketSystem != null
                && ActTicket.Tasks.Any(task => IsRuleTaskType(task.TaskType) && task.ManagementId == managementId);
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
            await ReportUnimplementedTarget(reqTask, managementId, deviceId,
                $"gateway {deviceId?.ToString() ?? "(none)"} of management {managementId?.ToString() ?? "(unknown)"} is set to external ticket system, " +
                "but the ticket hands no rule task of this management to it (not implemented yet).");
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
