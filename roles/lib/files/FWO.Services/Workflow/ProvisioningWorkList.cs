using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Way an entry of the provisioning work list is implemented.
    /// </summary>
    public enum ProvisioningWay
    {
        /// <summary>The whole request task is handed to the external ticket system of its management, e.g. Tufin SecureChange.</summary>
        ExternalTicketSystem,

        /// <summary>The implementation task is written directly to the firewall.</summary>
        FirewallWrite
    }

    /// <summary>
    /// One entry of the provisioning work list: a request task handed to an external ticket system, or an
    /// implementation task written to the firewall.
    /// </summary>
    /// <param name="Way">How the entry is implemented.</param>
    /// <param name="ReqTask">Request task of the entry.</param>
    /// <param name="ImplTask">Implementation task written to the firewall; null for an external ticket system.</param>
    /// <param name="ExternalTicketSystem">External ticket system the request task is handed to; null for a firewall write.</param>
    /// <param name="IsFinished">Whether the task has reached the end state of the implementation phase, e.g. implemented,
    /// set to done by hand or rejected; a finished entry is not provisioned again.</param>
    /// <param name="WaitsForPlanning">Whether the request task of a firewall write is still being planned. Such an entry is
    /// not written yet and holds back the entries behind it, until the request task reaches the end of the planning phase.</param>
    /// <param name="ReleasedByButton">Whether a firewall write on a Manual target is only written because a person released
    /// it by a button.</param>
    /// <param name="Rule">For a new rule shared by several implementation tasks: whether this task writes it for all of
    /// them or is covered by the task writing it; null if the task writes its rule alone.</param>
    public sealed record ProvisioningWorkItem(ProvisioningWay Way, WfReqTask ReqTask, WfImplTask? ImplTask, ExternalTicketSystemKind? ExternalTicketSystem, bool IsFinished,
        bool WaitsForPlanning = false, bool ReleasedByButton = false, SharedRule? Rule = null);

    /// <summary>
    /// A new rule several implementation tasks of one request task would write to the same rulebase (Check Point: the
    /// same layer), e.g. for gateways sharing a policy: only the task with the lowest number writes it, installed on the
    /// gateways of all of them; the others are covered and take over its result.
    /// </summary>
    /// <param name="WriterTaskNumber">Number of the implementation task writing the rule.</param>
    /// <param name="InstallOnGatewayIds">Gateways of all tasks sharing the rule.</param>
    /// <param name="CoveredTaskNumbers">Numbers of the implementation tasks covered by the rule.</param>
    public sealed record SharedRule(int WriterTaskNumber, IReadOnlyList<int> InstallOnGatewayIds, IReadOnlyList<int> CoveredTaskNumbers);

    /// <summary>
    /// Release of a scope by a person pressing a button: implementation tasks on targets set to Manual are written like
    /// those on Automatic targets, as the person decides instead of the settings. The release is remembered with the
    /// external requests it creates, so the provisioning keeps it when it continues after a finished request.
    /// </summary>
    /// <param name="ScopeTaskNumber">Number of the request task the button was pressed on, which includes its bundle;
    /// null for the whole ticket.</param>
    public sealed record ProvisioningRelease(int? ScopeTaskNumber);

    /// <summary>
    /// A request or implementation task nobody implements, as its implementation mode hands it to an external ticket
    /// system that does not take it over; reported with an alert.
    /// </summary>
    /// <param name="ReqTask">Request task concerned.</param>
    /// <param name="ImplTask">Implementation task concerned; null if the whole request task is concerned.</param>
    /// <param name="ManagementId">Management whose settings cause the gap.</param>
    /// <param name="Reason">Why the task is not provisioned.</param>
    public sealed record ProvisioningGap(WfReqTask ReqTask, WfImplTask? ImplTask, int ManagementId, string Reason);

    /// <summary>
    /// Effective device specific settings and states the provisioning work list is built from.
    /// </summary>
    public interface IProvisioningModeSource
    {
        /// <summary>Implementation mode of a management for rules or for objects.</summary>
        ProvisioningImplementationMode GetImplementationMode(int managementId, bool isRuleTask);

        /// <summary>Implementation mode for rules of a gateway.</summary>
        ProvisioningImplementationMode GetRuleImplementationMode(int gatewayId);

        /// <summary>Whether rules are written to the management of a gateway, see FirewallVendorProfile.WritesRules.</summary>
        bool WritesRules(int gatewayId);

        /// <summary>
        /// Gateways the existing rules of a rule_modify or rule_delete task take effect on according to the import; empty
        /// for other tasks or rules not found.
        /// </summary>
        IReadOnlyList<int> GetRuleEnforcingGateways(WfImplTask implTask);

        /// <summary>
        /// Target management of an implementation task, see WfImplTaskTargetManagementResolver: the management of its
        /// gateway, without gateway the one of the task, else of the request task; null if it cannot be resolved
        /// consistently, e.g. for an unknown gateway or a stored management that differs from the one of the gateway.
        /// </summary>
        int? GetTargetManagement(WfReqTask reqTask, WfImplTask implTask);

        /// <summary>External ticket system selected for a management; undefined if none is selected.</summary>
        ExternalTicketSystemKind GetExternalTicketSystem(int managementId);

        /// <summary>Lowest end state of the implementation phase for a task type.</summary>
        int GetImplementationEndState(string taskType);

        /// <summary>Lowest end state of the planning phase for a task type; null if the planning phase is not active.</summary>
        int? GetPlanningEndState(string taskType);
    }

    /// <summary>
    /// Ordered list of what a ticket provisions, built anew from the current tasks and settings each time, so it is
    /// never stored: a request task whose management hands it to an external ticket system as a whole, otherwise each
    /// implementation task whose target management is Automatic. Manual targets are left to a person and do not block
    /// the entries behind them. The entries are ordered by request task number, then implementation task number; they
    /// are provisioned one after another, the next one is the first entry not finished. With an active planning phase an
    /// implementation task is only written once its request task is planned; a request task handed to an external ticket
    /// system is handed over in any state, as the external ticket system approves and plans it itself.
    /// The implementation mode of a rule task is the one of its gateway, for a rule to modify or delete the strictest one of
    /// all gateways the rule takes effect on; a rule is only written to a vendor that writes
    /// rules, and a new rule only with its calculated position, otherwise the task is reported.
    /// </summary>
    public sealed class ProvisioningWorkList
    {
        private const string kLogTitle = "Provisioning";

        /// <summary>Entries in the order of provisioning, finished ones included.</summary>
        public List<ProvisioningWorkItem> Items { get; } = [];

        /// <summary>
        /// Tasks to be reported with an alert: tasks nobody implements, and rules left to a person because they also take
        /// effect on a gateway set to manual.
        /// </summary>
        public List<ProvisioningGap> Gaps { get; } = [];

        /// <summary>The entry to provision next; null if every entry is finished.</summary>
        public ProvisioningWorkItem? Next => Items.FirstOrDefault(item => !item.IsFinished);

        /// <summary>
        /// Builds the work list of the given request tasks with their implementation tasks.
        /// </summary>
        /// <param name="reqTasks">Request tasks in the scope of the provisioning.</param>
        /// <param name="modes">Effective settings and states.</param>
        /// <param name="releasedReqTaskIds">Request tasks released by a button, whose implementation tasks on Manual targets
        /// are written as well; null or empty without release.</param>
        public static ProvisioningWorkList Build(IEnumerable<WfReqTask> reqTasks, IProvisioningModeSource modes, IReadOnlySet<long>? releasedReqTaskIds = null)
        {
            ProvisioningWorkList workList = new() { releasedReqTaskIds = releasedReqTaskIds ?? kNoReleasedReqTasks };
            foreach (WfReqTask reqTask in reqTasks.OrderBy(task => task.TaskNumber))
            {
                workList.AddRequestTask(reqTask, modes);
            }
            workList.ShareNewRulesOfTheSameRulebase(modes);
            return workList;
        }

        /// <summary>
        /// Lets the new rules of the implementation tasks of a request task that are written to the same rulebase of the
        /// same management be written once, see <see cref="SharedRule"/>. Only tasks the work list writes take part; a
        /// task left to a person keeps its own rule.
        /// </summary>
        private void ShareNewRulesOfTheSameRulebase(IProvisioningModeSource modes)
        {
            List<IGrouping<(long ReqTaskId, int ManagementId, string RulebaseKey), int>> groups = [.. Enumerable.Range(0, Items.Count)
                .Where(index => RulebaseKeyOf(Items[index]) != null)
                .GroupBy(index => (Items[index].ReqTask.Id, ManagementOf(Items[index], modes), RulebaseKeyOf(Items[index])!))
                .Where(group => group.Count() > 1)];
            foreach (IGrouping<(long ReqTaskId, int ManagementId, string RulebaseKey), int> group in groups)
            {
                List<int> indexes = [.. group.OrderBy(index => Items[index].ImplTask!.TaskNumber)];
                int writerTaskNumber = Items[indexes[0]].ImplTask!.TaskNumber;
                List<int> gatewayIds = [.. indexes.Select(index => Items[index].ImplTask!.DeviceId).OfType<int>().Distinct()];
                List<int> coveredTaskNumbers = [.. indexes.Skip(1).Select(index => Items[index].ImplTask!.TaskNumber)];
                SharedRule sharedRule = new(writerTaskNumber, gatewayIds, coveredTaskNumbers);
                foreach (int index in indexes)
                {
                    Items[index] = Items[index] with { Rule = sharedRule };
                }
            }
        }

        /// <summary>The rulebase a new rule of a firewall write goes to; null for other entries or without position.</summary>
        private static string? RulebaseKeyOf(ProvisioningWorkItem item)
        {
            if (item.Way != ProvisioningWay.FirewallWrite || item.ImplTask is not WfImplTask implTask
                || implTask.TaskType != nameof(WfTaskType.access) || implTask.DeviceId is not > 0)
            {
                return null;
            }
            string? rulebaseKey = implTask.RulePosition?.GetRulebaseKey();
            return string.IsNullOrWhiteSpace(rulebaseKey) ? null : rulebaseKey;
        }

        /// <summary>The target management of an entry: of its implementation task along the shared resolver, of its request task for an entry without implementation task; 0 if unknown.</summary>
        private static int ManagementOf(ProvisioningWorkItem item, IProvisioningModeSource modes) =>
            item.ImplTask == null ? item.ReqTask.ManagementId ?? 0 : modes.GetTargetManagement(item.ReqTask, item.ImplTask) ?? 0;

        private static readonly HashSet<long> kNoReleasedReqTasks = [];

        private IReadOnlySet<long> releasedReqTaskIds = kNoReleasedReqTasks;

        /// <summary>Adds the entries of a request task: as a whole for an external ticket system, else one per implementation task.</summary>
        private void AddRequestTask(WfReqTask reqTask, IProvisioningModeSource modes)
        {
            bool isRuleTask = WfHandler.IsRuleTaskType(reqTask.TaskType);
            if (!isRuleTask && !WfObjectTaskHelper.IsManagementObjectTask(reqTask.TaskType))
            {
                Log.WriteDebug(kLogTitle, $"Request task {reqTask.Id} ({reqTask.TaskType}) has no target to provision.");
                return;
            }
            if (TryAddExternalTicketSystemEntry(reqTask, isRuleTask, modes))
            {
                return;
            }
            foreach (WfImplTask implTask in reqTask.ImplementationTasks.OrderBy(task => task.TaskNumber))
            {
                AddImplTask(reqTask, implTask, isRuleTask, modes);
            }
        }

        /// <summary>
        /// Adds the request task as a whole if the implementation mode of its management is external ticket system.
        /// Without a selected system the task is reported instead. Returns whether the request task is handled this way.
        /// </summary>
        private bool TryAddExternalTicketSystemEntry(WfReqTask reqTask, bool isRuleTask, IProvisioningModeSource modes)
        {
            if (reqTask.ManagementId is not int managementId || managementId <= 0
                || modes.GetImplementationMode(managementId, isRuleTask) != ProvisioningImplementationMode.RerouteToExternalTicketSystem)
            {
                return false;
            }
            bool isFinished = reqTask.StateId >= modes.GetImplementationEndState(reqTask.TaskType);
            ExternalTicketSystemKind system = modes.GetExternalTicketSystem(managementId);
            if (system == ExternalTicketSystemKind.Undefined)
            {
                if (!isFinished)
                {
                    Gaps.Add(new(reqTask, null, managementId, NoExternalTicketSystemSelectedReason(managementId)));
                }
                return true;
            }
            Items.Add(new(ProvisioningWay.ExternalTicketSystem, reqTask, null, system, isFinished));
            return true;
        }

        /// <summary>Adds an implementation task along the implementation mode of its target, or reports it as gap.</summary>
        private void AddImplTask(WfReqTask reqTask, WfImplTask implTask, bool isRuleTask, IProvisioningModeSource modes)
        {
            if (isRuleTask)
            {
                AddRuleImplTask(reqTask, implTask, modes);
                return;
            }
            // an object implementation task names its target itself; the management of its request task is no fallback
            if (implTask.ManagementId is not > 0 && implTask.DeviceId is not > 0)
            {
                Log.WriteDebug(kLogTitle, $"Implementation task {implTask.Id} of request task {reqTask.Id} has no target management to provision.");
                return;
            }
            if (!TryGetTargetManagement(reqTask, implTask, modes, out int managementId))
            {
                return;
            }
            bool isFinished = implTask.StateId >= modes.GetImplementationEndState(implTask.TaskType);
            switch (modes.GetImplementationMode(managementId, false))
            {
                case ProvisioningImplementationMode.Automatic:
                    Items.Add(new(ProvisioningWay.FirewallWrite, reqTask, implTask, null, isFinished, IsBeingPlanned(reqTask, modes)));
                    break;
                case ProvisioningImplementationMode.Manual when releasedReqTaskIds.Contains(reqTask.Id):
                    Items.Add(new(ProvisioningWay.FirewallWrite, reqTask, implTask, null, isFinished, IsBeingPlanned(reqTask, modes), true));
                    break;
                case ProvisioningImplementationMode.RerouteToExternalTicketSystem when !isFinished:
                    Gaps.Add(new(reqTask, implTask, managementId, $"target management {managementId} is set to external ticket system, " +
                        "but the implementation task can only be written to the firewall or implemented manually."));
                    break;
                default:
                    Log.WriteDebug(kLogTitle, $"Implementation task {implTask.Id} of request task {reqTask.Id} is implemented manually.");
                    break;
            }
        }

        /// <summary>
        /// Adds a rule implementation task along the implementation mode for rules of its gateway. A task without gateway
        /// is left to a person, as nothing tells where to write the rule.
        /// </summary>
        private void AddRuleImplTask(WfReqTask reqTask, WfImplTask implTask, IProvisioningModeSource modes)
        {
            if (implTask.DeviceId is not int gatewayId || gatewayId <= 0)
            {
                Log.WriteDebug(kLogTitle, $"Implementation task {implTask.Id} of request task {reqTask.Id} has no gateway to write the rule to.");
                return;
            }
            if (!TryGetTargetManagement(reqTask, implTask, modes, out int managementId))
            {
                return;
            }
            bool isFinished = implTask.StateId >= modes.GetImplementationEndState(implTask.TaskType);
            (ProvisioningImplementationMode mode, int decidingGatewayId) = StrictestRuleMode(gatewayId, implTask, modes);
            bool released = mode == ProvisioningImplementationMode.Manual && releasedReqTaskIds.Contains(reqTask.Id);
            if (mode == ProvisioningImplementationMode.RerouteToExternalTicketSystem)
            {
                if (!isFinished)
                {
                    Gaps.Add(new(reqTask, implTask, managementId, $"gateway {decidingGatewayId} is set to external ticket system, " +
                        "but the implementation task can only be written to the firewall or implemented manually."));
                }
                return;
            }
            if (mode != ProvisioningImplementationMode.Automatic && !released)
            {
                if (!isFinished && decidingGatewayId != gatewayId)
                {
                    Gaps.Add(new(reqTask, implTask, managementId, $"the rule also takes effect on gateway {decidingGatewayId}, which is set to manual, " +
                        "so it is not written automatically; release it by button or implement it manually."));
                    return;
                }
                Log.WriteDebug(kLogTitle, $"Implementation task {implTask.Id} of request task {reqTask.Id} is implemented manually.");
                return;
            }
            string? notWrittenReason = isFinished ? null : RuleNotWrittenReason(implTask, gatewayId, modes);
            if (notWrittenReason != null)
            {
                Gaps.Add(new(reqTask, implTask, managementId, notWrittenReason));
                return;
            }
            Items.Add(new(ProvisioningWay.FirewallWrite, reqTask, implTask, null, isFinished, IsBeingPlanned(reqTask, modes), released));
        }

        /// <summary>
        /// The implementation mode for rules that applies to a task, with the gateway it comes from: the mode of its
        /// gateway, and for an existing rule the strictest mode of all gateways the rule takes effect on, as changing the
        /// rule changes it on all of them. External ticket system is the strictest, then manual, then automatic; on equal
        /// modes the gateway of the task decides.
        /// </summary>
        private static (ProvisioningImplementationMode Mode, int GatewayId) StrictestRuleMode(int gatewayId, WfImplTask implTask,
            IProvisioningModeSource modes)
        {
            List<int> gatewayIds = [gatewayId, .. modes.GetRuleEnforcingGateways(implTask).Where(id => id != gatewayId).Distinct()];
            return gatewayIds
                .Select(id => (Mode: modes.GetRuleImplementationMode(id), GatewayId: id))
                .MaxBy(entry => Strictness(entry.Mode));
        }

        /// <summary>Order of the implementation modes for rules from automatic (least strict) to external ticket system (strictest).</summary>
        private static int Strictness(ProvisioningImplementationMode mode) => mode switch
        {
            ProvisioningImplementationMode.Automatic => 0,
            ProvisioningImplementationMode.RerouteToExternalTicketSystem => 2,
            _ => 1
        };

        /// <summary>Why a rule cannot be written; null if it can.</summary>
        private static string? RuleNotWrittenReason(WfImplTask implTask, int gatewayId, IProvisioningModeSource modes)
        {
            if (!modes.WritesRules(gatewayId))
            {
                return $"rules are not written to the vendor of gateway {gatewayId} yet.";
            }
            if (implTask.TaskType == nameof(WfTaskType.access) && implTask.RulePosition == null)
            {
                return $"no rule position was calculated for gateway {gatewayId}, so the new rule is not written.";
            }
            return null;
        }

        /// <summary>
        /// Resolves the target management of an implementation task. A task with a gateway whose management cannot be
        /// resolved consistently is reported unless it is finished, as writing it would guess the target; a task without
        /// any management is left out.
        /// </summary>
        private bool TryGetTargetManagement(WfReqTask reqTask, WfImplTask implTask, IProvisioningModeSource modes, out int managementId)
        {
            if (modes.GetTargetManagement(reqTask, implTask) is int resolvedManagementId)
            {
                managementId = resolvedManagementId;
                return true;
            }
            managementId = implTask.ManagementId ?? reqTask.ManagementId ?? 0;
            if (implTask.DeviceId is not > 0)
            {
                Log.WriteDebug(kLogTitle, $"Implementation task {implTask.Id} of request task {reqTask.Id} has no target management to provision.");
            }
            else if (implTask.StateId < modes.GetImplementationEndState(implTask.TaskType))
            {
                Gaps.Add(new(reqTask, implTask, managementId, InconsistentTargetReason(implTask)));
            }
            return false;
        }

        /// <summary>The reason of a gap for an implementation task whose management cannot be resolved consistently with its gateway.</summary>
        private static string InconsistentTargetReason(WfImplTask implTask)
        {
            return implTask.ManagementId == null
                ? $"its gateway {implTask.DeviceId} is not known or has no management."
                : $"its management {implTask.ManagementId} does not match the management of its gateway {implTask.DeviceId}, " +
                    "or the gateway is not known; correct the implementation task.";
        }

        /// <summary>Whether the request task is still in an active planning phase, so its implementation tasks are not written yet.</summary>
        private static bool IsBeingPlanned(WfReqTask reqTask, IProvisioningModeSource modes)
        {
            int? planningEndState = modes.GetPlanningEndState(reqTask.TaskType);
            return planningEndState != null && reqTask.StateId < planningEndState;
        }

        /// <summary>The reason of a gap for a management set to external ticket system without a selected system.</summary>
        private static string NoExternalTicketSystemSelectedReason(int managementId)
        {
            return $"management {managementId} is set to external ticket system, but no external ticket system is selected for it.";
        }
    }
}
