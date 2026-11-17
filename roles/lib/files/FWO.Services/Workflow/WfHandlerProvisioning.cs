using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Builds the provisioning work list of a ticket from the current implementation modes of the device specific
    /// settings, see <see cref="ProvisioningWorkList"/>.
    /// </summary>
    public partial class WfHandler
    {
        private const string kProvisioningLogTitle = "Provisioning";

        /// <summary>Gateways the rules of each rule_modify and rule_delete task take effect on, by implementation task id.</summary>
        private Dictionary<long, List<int>> ruleEnforcingGatewayIds = [];

        /// <summary>
        /// Builds the provisioning work list of a ticket with the device specific settings in effect now. Tasks nobody
        /// implements are reported with an alert. If the settings cannot be resolved, nothing is provisioned, as the
        /// defaults would write to firewalls that are left to an external ticket system or to a person.
        /// </summary>
        /// <param name="ticket">Ticket with its request and implementation tasks.</param>
        /// <param name="scopeTask">Request task in the request task scope: only this task, a bundled task together with
        /// its bundle once every member has reached the state of this task. Null in the ticket scope, where every
        /// request task of the ticket is provisioned.</param>
        /// <param name="release">Scope released by a button, whose implementation tasks on Manual targets are written as
        /// well; null without release.</param>
        /// <exception cref="InvalidOperationException">The device specific settings could not be resolved.</exception>
        public async Task<ProvisioningWorkList> BuildProvisioningWorkList(WfTicket ticket, WfReqTask? scopeTask = null, ProvisioningRelease? release = null)
        {
            SetTicketEnv(ticket);
            List<WfReqTask> ticketTasks = [.. ActTicket.Tasks];
            List<WfReqTask> reqTasks = scopeTask == null ? ticketTasks : TasksOfRequestTaskScope(ticketTasks, scopeTask);
            ProvisioningWorkList workList = await BuildWorkList(ticket, reqTasks, ReleasedReqTaskIds(ticketTasks, release));
            foreach (ProvisioningGap gap in workList.Gaps)
            {
                await ReportProvisioningGap(gap);
            }
            return workList;
        }

        /// <summary>
        /// The work list entry of a single implementation task, with the device specific settings in effect now; its
        /// bundle and the other tasks of the ticket do not matter. A gap of the task is reported with an alert.
        /// </summary>
        /// <param name="ticket">Ticket with its request and implementation tasks.</param>
        /// <param name="implTaskId">Implementation task to write.</param>
        /// <param name="byButton">True if a person pressed a button: the task is written on a Manual target as well.</param>
        /// <returns>The entry; null if the task is not in the ticket or not written by FWO.</returns>
        /// <exception cref="InvalidOperationException">The device specific settings could not be resolved.</exception>
        public async Task<ProvisioningWorkItem?> BuildProvisioningOfImplTask(WfTicket ticket, long implTaskId, bool byButton = false)
        {
            SetTicketEnv(ticket);
            WfReqTask? reqTask = ActTicket.Tasks.FirstOrDefault(task => task.ImplementationTasks.Any(implTask => implTask.Id == implTaskId));
            if (reqTask == null)
            {
                return null;
            }
            List<WfReqTask> reqTasks = [reqTask];
            HashSet<long> releasedReqTaskIds = byButton ? [reqTask.Id] : [];
            ProvisioningWorkList workList = await BuildWorkList(ticket, reqTasks, releasedReqTaskIds);
            foreach (ProvisioningGap gap in workList.Gaps.Where(gap => gap.ImplTask?.Id == implTaskId))
            {
                await ReportProvisioningGap(gap);
            }
            return workList.Items.FirstOrDefault(item => item.ImplTask?.Id == implTaskId);
        }

        /// <summary>Builds the work list of the request tasks with the settings, gateways and rule gateways resolved for this run.</summary>
        private async Task<ProvisioningWorkList> BuildWorkList(WfTicket ticket, List<WfReqTask> reqTasks, HashSet<long> releasedReqTaskIds)
        {
            ResetAutoPlanCache();
            await EnsureDevicesLoaded();
            await LoadRuleEnforcingGateways(ticket, reqTasks);
            List<int> gatewayIds = [.. ProvisioningGatewayIds(reqTasks).Concat(ruleEnforcingGatewayIds.Values.SelectMany(ids => ids)).Distinct()];
            await EnsureAutoPlanSettings(gatewayIds, ProvisioningManagementIds(reqTasks));
            if (AutoPlanSettingsOverride == null && (autoPlanResolveFailed || !autoPlanCache.ContainsKey((DeviceSpecificScopeType.Global, null))))
            {
                throw new InvalidOperationException($"The device specific settings could not be resolved; nothing of ticket {ticket.Id} is provisioned.");
            }
            return ProvisioningWorkList.Build(reqTasks, new ResolvedProvisioningModes(this), releasedReqTaskIds);
        }

        /// <summary>
        /// Device specific settings in effect for a management, e.g. the response timeout and retries of its external
        /// requests. Without management, or if the settings cannot be resolved, the global settings or the defaults apply.
        /// </summary>
        /// <param name="managementId">Management of the request task or implementation task.</param>
        public async Task<GlobalDeviceSpecificSettings> GetManagementSettings(int? managementId)
        {
            List<int> managementIds = managementId is int id && id > 0 ? [id] : kNoIds;
            await EnsureAutoPlanSettings(kNoIds, managementIds);
            return GetResolvedSettings(DeviceSpecificScopeType.Management, managementId);
        }

        /// <summary>
        /// The request tasks a release by a button covers: the whole ticket, or the request task it was pressed on with
        /// its bundle. Empty without release or if the request task no longer exists.
        /// </summary>
        private HashSet<long> ReleasedReqTaskIds(List<WfReqTask> ticketTasks, ProvisioningRelease? release)
        {
            if (release == null)
            {
                return [];
            }
            if (release.ScopeTaskNumber == null)
            {
                return [.. ticketTasks.Select(task => task.Id)];
            }
            WfReqTask? scopeTask = ticketTasks.FirstOrDefault(task => task.TaskNumber == release.ScopeTaskNumber);
            return scopeTask == null ? [] : [.. TasksOfRequestTaskScope(ticketTasks, scopeTask).Select(task => task.Id)];
        }

        /// <summary>The gateways of the rule implementation tasks, whose implementation mode for rules applies.</summary>
        private static List<int> ProvisioningGatewayIds(List<WfReqTask> reqTasks)
        {
            IEnumerable<WfImplTask> ruleImplTasks = reqTasks.Where(task => IsRuleTaskType(task.TaskType)).SelectMany(task => task.ImplementationTasks);
            return [.. ruleImplTasks.Select(implTask => implTask.DeviceId).OfType<int>().Where(id => id > 0).Distinct()];
        }

        /// <summary>
        /// Loads the gateways the existing rules of the rule_modify and rule_delete tasks take effect on, whose
        /// implementation modes apply as well. Without them a rule could be changed on a gateway left to a person, so the
        /// work list is not built if they cannot be loaded.
        /// </summary>
        /// <exception cref="InvalidOperationException">The gateways of the rules could not be loaded.</exception>
        private async Task LoadRuleEnforcingGateways(WfTicket ticket, List<WfReqTask> reqTasks)
        {
            ruleEnforcingGatewayIds = [];
            List<WfImplTask> implTasks = [.. reqTasks.SelectMany(task => task.ImplementationTasks)
                .Where(implTask => WfTaskBase.IsRuleChangeOrDelete(implTask.TaskType))];
            if (implTasks.Count == 0 || apiConnection == null)
            {
                return;
            }
            try
            {
                foreach (IGrouping<int, WfImplTask> managementTasks in implTasks.GroupBy(ManagementOfRuleTask).Where(group => group.Key > 0))
                {
                    List<string> ruleUids = [.. managementTasks.SelectMany(implTask => implTask.GetRuleUids()).Distinct()];
                    Dictionary<string, List<Device>> gatewaysByRuleUid = await RuleEnforcingGateways.Load(apiConnection, managementTasks.Key, ruleUids);
                    foreach (WfImplTask implTask in managementTasks)
                    {
                        ruleEnforcingGatewayIds[implTask.Id] = GatewayIdsOfRules(implTask.GetRuleUids(), gatewaysByRuleUid);
                    }
                }
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"The gateways of the rules to modify or delete could not be loaded; nothing of ticket {ticket.Id} is provisioned.", exception);
            }
        }

        private int ManagementOfRuleTask(WfImplTask implTask) => ImplTaskManagementId(null, implTask) ?? 0;

        /// <summary>The gateways the given rules take effect on, each once.</summary>
        private static List<int> GatewayIdsOfRules(List<string> ruleUids, Dictionary<string, List<Device>> gatewaysByRuleUid)
        {
            List<int> gatewayIds = [];
            foreach (string ruleUid in ruleUids)
            {
                if (gatewaysByRuleUid.TryGetValue(ruleUid, out List<Device>? gateways))
                {
                    gatewayIds.AddRange(gateways.Select(gateway => gateway.Id));
                }
            }
            return [.. gatewayIds.Distinct()];
        }

        /// <summary>Whether rules are written to the management of a gateway, by the device type of the management.</summary>
        private bool WritesRulesToGateway(int gatewayId)
        {
            int deviceTypeId = Devices.FirstOrDefault(device => device.Id == gatewayId)?.Management?.DeviceType?.Id ?? 0;
            return FirewallVendors.WritesRules(deviceTypeId);
        }

        /// <summary>The managements whose settings the work list needs: of the request tasks and the targets of their implementation tasks.</summary>
        private List<int> ProvisioningManagementIds(List<WfReqTask> reqTasks)
        {
            IEnumerable<int?> reqTaskManagementIds = reqTasks.Select(task => task.ManagementId);
            IEnumerable<int?> implTaskManagementIds = reqTasks.SelectMany(task => task.ImplementationTasks.Select(implTask => ImplTaskManagementId(task, implTask)));
            return [.. reqTaskManagementIds.Concat(implTaskManagementIds).OfType<int>().Where(id => id > 0).Distinct()];
        }

        /// <summary>
        /// Logs a task nobody implements and raises a workflow alert for it.
        /// </summary>
        private async Task ReportProvisioningGap(ProvisioningGap gap)
        {
            string implTaskText = gap.ImplTask == null ? "" : $", implementation task {gap.ImplTask.Id}";
            string description = $"Ticket {gap.ReqTask.TicketId}, request task {gap.ReqTask.Id}{implTaskText}: not provisioned, {gap.Reason}";
            Log.WriteWarning(kProvisioningLogTitle, description);
            if (apiConnection == null)
            {
                return;
            }
            await AlertHelper.SetAlert(apiConnection, kProvisioningLogTitle, description, kAlertSource, AlertCode.WorkflowAlert,
                new AlertHelper.AdditionalAlertData { MgmtId = gap.ManagementId, CompareDesc = true });
        }

        /// <summary>
        /// Gives the work list the settings resolved for the current run and the end states of the state matrices.
        /// </summary>
        private sealed class ResolvedProvisioningModes(WfHandler handler) : IProvisioningModeSource
        {
            /// <inheritdoc/>
            public ProvisioningImplementationMode GetImplementationMode(int managementId, bool isRuleTask)
            {
                return handler.GetResolvedSettings(DeviceSpecificScopeType.Management, managementId).GetImplementationMode(isRuleTask);
            }

            /// <inheritdoc/>
            public ProvisioningImplementationMode GetRuleImplementationMode(int gatewayId)
            {
                return handler.GetResolvedSettings(DeviceSpecificScopeType.Gateway, gatewayId).GetImplementationMode(true);
            }

            /// <inheritdoc/>
            public bool WritesRules(int gatewayId)
            {
                return handler.WritesRulesToGateway(gatewayId);
            }

            /// <inheritdoc/>
            public IReadOnlyList<int> GetRuleEnforcingGateways(WfImplTask implTask)
            {
                return handler.ruleEnforcingGatewayIds.TryGetValue(implTask.Id, out List<int>? gatewayIds) ? gatewayIds : kNoIds;
            }

            /// <inheritdoc/>
            public int? GetTargetManagement(WfReqTask reqTask, WfImplTask implTask)
            {
                return handler.ImplTaskManagementId(reqTask, implTask);
            }

            /// <inheritdoc/>
            public ExternalTicketSystemKind GetExternalTicketSystem(int managementId)
            {
                return handler.GetResolvedSettings(DeviceSpecificScopeType.Management, managementId).RerouteToExternalTicketSystem;
            }

            /// <inheritdoc/>
            public int GetImplementationEndState(string taskType)
            {
                return handler.StateMatrix(taskType).ImplementationLowestEndState;
            }

            /// <inheritdoc/>
            public int? GetPlanningEndState(string taskType)
            {
                return handler.StateMatrix(taskType).PlanningLowestEndState;
            }
        }
    }
}
