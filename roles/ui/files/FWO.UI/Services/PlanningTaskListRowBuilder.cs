using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using static FWO.Ui.Services.TaskListHelper;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Builds the rows and columns of the planning task list view. The tables and request-task cells are those of the
    /// approval task list view (without compliance); implementation tasks are shown in the table of their request task.
    /// Bundled tasks displayed as one form one row; their implementation tasks, usually created once for the whole
    /// bundle, belong to all tasks of the bundle. With all tasks, every request task (or bundle) is followed by its
    /// implementation tasks; with only implementation tasks, the columns holding request-task information only (zones,
    /// owner, reason) are replaced by the ids of the request tasks.
    /// </summary>
    public sealed class PlanningTaskListRowBuilder
    {
        /// <summary>Column key of the task level (request or implementation task), shown with all tasks.</summary>
        public const string kLevel = "level";
        /// <summary>Column key of the request task ids of an implementation task, shown with only implementation tasks.</summary>
        public const string kRequestTask = "request_task";
        /// <summary>Column key of the device of an implementation task.</summary>
        public const string kDevice = "device";
        /// <summary>Column key of the rule position of an implementation task.</summary>
        public const string kRulePosition = "rule_position";

        /// <summary>Columns whose request-task cells are taken from the approval task list view.</summary>
        private static readonly IReadOnlyList<string> kRequestTaskKeys =
        [
            ApprovalTaskListRowBuilder.kId, ApprovalTaskListRowBuilder.kTitle, ApprovalTaskListRowBuilder.kTaskType,
            ApprovalTaskListRowBuilder.kState, ApprovalTaskListRowBuilder.kHandler, ApprovalTaskListRowBuilder.kAssigned,
            ApprovalTaskListRowBuilder.kManagement, ApprovalTaskListRowBuilder.kReason, ApprovalTaskListRowBuilder.kFreeText,
            ApprovalTaskListRowBuilder.kSourceZone, ApprovalTaskListRowBuilder.kDestinationZone, ApprovalTaskListRowBuilder.kSource,
            ApprovalTaskListRowBuilder.kDestination, ApprovalTaskListRowBuilder.kService, ApprovalTaskListRowBuilder.kTimeRestriction,
            ApprovalTaskListRowBuilder.kOwner, ApprovalTaskListRowBuilder.kRuleAction, ApprovalTaskListRowBuilder.kTrack,
            ApprovalTaskListRowBuilder.kObject, ApprovalTaskListRowBuilder.kContent, ApprovalTaskListRowBuilder.kZone
        ];

        private readonly ApprovalTaskViewContext context;
        private readonly UserConfig userConfig;
        private readonly WfStateDict states;
        private readonly PlanningImplTaskDisplayLogic implLogic;
        private readonly PlanningImplTaskListActions implActions;

        /// <summary>Initializes the row builder.</summary>
        /// <param name="context">Display logic, visibility and editor callbacks of the request tasks.</param>
        /// <param name="userConfig">The current user configuration.</param>
        /// <param name="states">The workflow state names.</param>
        /// <param name="implLogic">Display logic of the implementation tasks.</param>
        /// <param name="implActions">Edit callbacks of the implementation tasks.</param>
        public PlanningTaskListRowBuilder(ApprovalTaskViewContext context, UserConfig userConfig, WfStateDict states,
            PlanningImplTaskDisplayLogic implLogic, PlanningImplTaskListActions implActions)
        {
            this.context = context;
            this.userConfig = userConfig;
            this.states = states;
            this.implLogic = implLogic;
            this.implActions = implActions;
        }

        /// <summary>Returns the visible columns of a table in display order.</summary>
        /// <param name="table">The table.</param>
        /// <param name="mode">The shown tasks.</param>
        /// <param name="visibility">The configured field visibility of access tasks.</param>
        /// <param name="objectVisibility">The configured visibility of object and group tasks if the table only holds those
        /// (see <see cref="ApprovalTaskListRowBuilder.ObjectVisibilityFor"/>), otherwise null.</param>
        /// <param name="implColumns">The columns needed by the implementation tasks of the table (see
        /// <see cref="ImplementationColumnsFor"/>); null for a table without implementation tasks.</param>
        public static List<TaskListColumn> VisibleColumns(TaskListTable table, PlanningTaskListMode mode,
            AccessTaskFieldVisibilityConfig visibility, ObjectTaskFieldVisibilityConfig? objectVisibility = null,
            PlanningImplColumns? implColumns = null)
        {
            bool allTasks = mode == PlanningTaskListMode.AllTasks;
            implColumns ??= PlanningImplColumns.None;
            List<TaskListColumn> columns =
            [
                new(ApprovalTaskListRowBuilder.kId, "id"),
                allTasks ? new(kLevel, "planning_list_level") : new(kRequestTask, "RequestTask")
            ];
            ObjectTaskFieldVisibilityConfig metadataVisibility = MetadataVisibility(table, visibility, objectVisibility);
            if (table == TaskListTable.Access)
            {
                AddAccessColumns(columns, allTasks, visibility);
            }
            else
            {
                AddObjectColumns(columns, table, allTasks, implColumns.RuleColumns, visibility);
            }
            AddIf(columns, allTasks && metadataVisibility.ShowReason, new(ApprovalTaskListRowBuilder.kReason, "reason"));
            // the free text of request tasks follows the approval task list view; that of implementation tasks is
            // shown (and edited) in the same column, so the column stays as long as an implementation task needs it
            AddIf(columns, (allTasks && ApprovalTaskListRowBuilder.ShowsRequestFreeText(table, visibility)) || implColumns.FreeText,
                new(ApprovalTaskListRowBuilder.kFreeText, "free_text"));
            if (table == TaskListTable.Access)
            {
                AddIf(columns, visibility.ShowTitle, new(ApprovalTaskListRowBuilder.kTitle, "title"));
                columns.Add(new(ApprovalTaskListRowBuilder.kTaskType, "task_type"));
                columns.Add(new(ApprovalTaskListRowBuilder.kState, "state"));
            }
            AddIf(columns, metadataVisibility.ShowHandler, new(ApprovalTaskListRowBuilder.kHandler, "handler"));
            AddIf(columns, metadataVisibility.ShowAssigned, new(ApprovalTaskListRowBuilder.kAssigned, "assigned"));
            return columns;
        }

        /// <summary>
        /// Builds the rows of one table, ordered like the approval task list view; with all tasks every request task
        /// (or bundle displayed as one) is followed by the implementation tasks of all its tasks, ordered by id.
        /// </summary>
        /// <param name="taskGroups">All request task groups of the ticket: single tasks or bundles displayed as one.</param>
        /// <param name="table">The table to build the rows for.</param>
        /// <param name="mode">The shown tasks.</param>
        public List<PlanningTaskListRow> Build(IEnumerable<List<WfReqTask>> taskGroups, TaskListTable table, PlanningTaskListMode mode)
        {
            List<ApprovalTaskListRow> requestRows = new ApprovalTaskListRowBuilder(context, userConfig, states).Build(taskGroups, table);
            bool showRequestFreeText = ApprovalTaskListRowBuilder.ShowsRequestFreeText(table, context.Visibility);
            List<PlanningTaskListRow> rows = [];
            foreach (ApprovalTaskListRow requestRow in requestRows)
            {
                if (mode == PlanningTaskListMode.AllTasks)
                {
                    rows.Add(BuildRequestTaskRow(requestRow, showRequestFreeText));
                }
                rows.AddRange(ImplementationTasksOf(requestRow.TaskGroup)
                    .Select(owned => BuildImplementationTaskRow(requestRow.TaskGroup, owned.RequestTask, owned.ImplementationTask)));
            }
            return rows;
        }

        /// <summary>Returns the implementation tasks of all tasks of a group, each with the request task it belongs to, ordered by id.</summary>
        private static IEnumerable<(WfReqTask RequestTask, WfImplTask ImplementationTask)> ImplementationTasksOf(IReadOnlyList<WfReqTask> taskGroup)
        {
            return taskGroup
                .SelectMany(requestTask => requestTask.ImplementationTasks.Select(implementationTask => (RequestTask: requestTask, ImplementationTask: implementationTask)))
                .OrderBy(owned => owned.ImplementationTask.Id);
        }

        /// <summary>
        /// Visibility of handler, assigned and reason, as in the approval task list view: access tasks use their own
        /// settings, tables of object and group tasks the object task settings, other tasks always show handler and assigned.
        /// </summary>
        private static ObjectTaskFieldVisibilityConfig MetadataVisibility(TaskListTable table,
            AccessTaskFieldVisibilityConfig visibility, ObjectTaskFieldVisibilityConfig? objectVisibility)
        {
            if (table == TaskListTable.Access)
            {
                return new() { ShowHandler = visibility.ShowHandler, ShowAssigned = visibility.ShowAssigned, ShowReason = visibility.ShowReason };
            }
            return objectVisibility ?? new() { ShowReason = visibility.ShowReason };
        }

        /// <summary>
        /// Adds the rule columns of the access table; the zone and owner columns hold request-task information only
        /// and are shown with all tasks.
        /// </summary>
        /// <param name="columns">The columns to add to.</param>
        /// <param name="allTasks">Whether request tasks are shown too.</param>
        /// <param name="visibility">The configured field visibility of access tasks.</param>
        private static void AddAccessColumns(List<TaskListColumn> columns, bool allTasks, AccessTaskFieldVisibilityConfig visibility)
        {
            AddIf(columns, allTasks, new(ApprovalTaskListRowBuilder.kSourceZone, "source_zone"));
            AddIf(columns, allTasks, new(ApprovalTaskListRowBuilder.kDestinationZone, "destination_zone"));
            columns.Add(new(ApprovalTaskListRowBuilder.kSource, "source"));
            columns.Add(new(ApprovalTaskListRowBuilder.kDestination, "destination"));
            columns.Add(new(ApprovalTaskListRowBuilder.kService, "service"));
            AddIf(columns, visibility.ShowValidFrom || visibility.ShowValidTo, new(ApprovalTaskListRowBuilder.kTimeRestriction, "time_restriction"));
            AddIf(columns, allTasks && visibility.ShowOwner, new(ApprovalTaskListRowBuilder.kOwner, "owner"));
            columns.Add(new(ApprovalTaskListRowBuilder.kManagement, "management"));
            columns.Add(new(kDevice, "device"));
            columns.Add(new(kRulePosition, "rule_position"));
            AddIf(columns, visibility.ShowRuleAction, new(ApprovalTaskListRowBuilder.kRuleAction, "rule_action"));
            AddIf(columns, visibility.ShowTracking, new(ApprovalTaskListRowBuilder.kTrack, "track"));
        }

        /// <summary>
        /// Returns the columns needed by the implementation tasks of a table (for a bundle: of all its tasks): the rule
        /// columns of rule implementation tasks, and the free text if a task has one or the user may actually add one.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="taskGroups">All request task groups of the ticket: single tasks or bundles displayed as one.</param>
        /// <param name="canEdit">Returns whether the user may change an implementation task and its request task.</param>
        public static PlanningImplColumns ImplementationColumnsFor(TaskListTable table, IEnumerable<List<WfReqTask>> taskGroups,
            Func<WfImplTask, WfReqTask, bool> canEdit)
        {
            List<(WfImplTask Task, WfReqTask RequestTask)> implementationTasks = [.. taskGroups
                .Where(taskGroup => taskGroup.Count > 0 && ApprovalTaskListRowBuilder.TableOf(taskGroup[0]) == table)
                .SelectMany(taskGroup => taskGroup.SelectMany(requestTask =>
                    requestTask.ImplementationTasks.Select(implementationTask => (implementationTask, requestTask))))];
            bool freeText = implementationTasks.Any(pair => !string.IsNullOrWhiteSpace(pair.Task.FreeText)
                || (PlanningImplTaskDisplayLogic.CanEditFreeText(pair.Task) && canEdit(pair.Task, pair.RequestTask)));
            return new(RuleColumns([.. implementationTasks.Select(pair => pair.Task)]), freeText);
        }

        /// <summary>Returns the rule columns needed by implementation tasks.</summary>
        private static PlanningRuleColumns RuleColumns(List<WfImplTask> implementationTasks)
        {
            if (implementationTasks.Any(PlanningImplTaskDisplayLogic.IsRuleTask))
            {
                return PlanningRuleColumns.All;
            }
            return implementationTasks.Any(PlanningImplTaskDisplayLogic.IsRuleImplementationTask) ? PlanningRuleColumns.Device : PlanningRuleColumns.None;
        }

        /// <summary>
        /// Adds the columns of the address, service and other tables: content only for objects and groups, the zone
        /// (request-task information only) for addresses with all tasks, and the rule columns needed by the
        /// implementation tasks of the table, as in the access table.
        /// </summary>
        /// <param name="columns">The columns to add to.</param>
        /// <param name="table">The table.</param>
        /// <param name="allTasks">Whether request tasks are shown too.</param>
        /// <param name="ruleColumns">The rule columns needed by the implementation tasks of the table.</param>
        /// <param name="visibility">The configured field visibility of access tasks, applied to time restriction, rule action and tracking.</param>
        private static void AddObjectColumns(List<TaskListColumn> columns, TaskListTable table, bool allTasks,
            PlanningRuleColumns ruleColumns, AccessTaskFieldVisibilityConfig visibility)
        {
            bool ruleContent = ruleColumns == PlanningRuleColumns.All;
            columns.Add(new(ApprovalTaskListRowBuilder.kTaskType, "task_type"));
            columns.Add(new(ApprovalTaskListRowBuilder.kState, "state"));
            columns.Add(new(ApprovalTaskListRowBuilder.kTitle, "title"));
            columns.Add(new(ApprovalTaskListRowBuilder.kObject, "object"));
            AddIf(columns, table != TaskListTable.Other, new(ApprovalTaskListRowBuilder.kContent, "content"));
            AddIf(columns, allTasks && table == TaskListTable.Address, new(ApprovalTaskListRowBuilder.kZone, "zone"));
            AddIf(columns, ruleContent, new(ApprovalTaskListRowBuilder.kSource, "source"));
            AddIf(columns, ruleContent, new(ApprovalTaskListRowBuilder.kDestination, "destination"));
            AddIf(columns, ruleContent, new(ApprovalTaskListRowBuilder.kService, "service"));
            AddIf(columns, ruleContent && (visibility.ShowValidFrom || visibility.ShowValidTo),
                new(ApprovalTaskListRowBuilder.kTimeRestriction, "time_restriction"));
            columns.Add(new(ApprovalTaskListRowBuilder.kManagement, "management"));
            AddIf(columns, ruleColumns != PlanningRuleColumns.None, new(kDevice, "device"));
            AddIf(columns, ruleContent, new(kRulePosition, "rule_position"));
            AddIf(columns, ruleContent && visibility.ShowRuleAction, new(ApprovalTaskListRowBuilder.kRuleAction, "rule_action"));
            AddIf(columns, ruleContent && visibility.ShowTracking, new(ApprovalTaskListRowBuilder.kTrack, "track"));
        }

        /// <summary>
        /// Builds the row of a request task from its approval task list cells. The free text column also holds the free
        /// text of the implementation tasks, so the free text of the request task stays empty where the approval task
        /// list view hides it.
        /// </summary>
        /// <param name="requestRow">The approval task list row of the request task (or bundle displayed as one).</param>
        /// <param name="showFreeText">Whether the free text of the request task is shown.</param>
        private PlanningTaskListRow BuildRequestTaskRow(ApprovalTaskListRow requestRow, bool showFreeText)
        {
            Dictionary<string, TaskListCell> cells = new()
            {
                [kLevel] = ReadOnly(userConfig.GetText("RequestTask"))
            };
            foreach (string key in kRequestTaskKeys.Where(key => showFreeText || key != ApprovalTaskListRowBuilder.kFreeText))
            {
                cells[key] = requestRow.Cell(key);
            }
            return new PlanningTaskListRow(requestRow.Task, null, cells) { TaskGroup = requestRow.TaskGroup };
        }

        /// <summary>Builds the row of an implementation task; management, device, free text and name open their editor if the task may be changed.</summary>
        /// <param name="taskGroup">The request tasks of the row group, whose ids are shown with only implementation tasks.</param>
        /// <param name="requestTask">The request task the implementation task belongs to.</param>
        /// <param name="implementationTask">The implementation task.</param>
        private PlanningTaskListRow BuildImplementationTaskRow(IReadOnlyList<WfReqTask> taskGroup, WfReqTask requestTask, WfImplTask implementationTask)
        {
            bool canEdit = implActions.CanEdit(implementationTask, requestTask);
            Dictionary<string, TaskListCell> cells = new()
            {
                [ApprovalTaskListRowBuilder.kId] = ReadOnly(implementationTask.Id.ToString()),
                [kLevel] = ReadOnly(userConfig.GetText("ImplementationTask")),
                [kRequestTask] = ReadOnly(ApprovalTaskDetailsEditorService.TaskIds(taskGroup)),
                [ApprovalTaskListRowBuilder.kTitle] = ReadOnly(implementationTask.Title),
                [ApprovalTaskListRowBuilder.kTaskType] = ReadOnly(string.IsNullOrWhiteSpace(implementationTask.TaskType) ? "" : userConfig.GetText(implementationTask.TaskType)),
                [ApprovalTaskListRowBuilder.kState] = ReadOnly(states.GetName(implementationTask.StateId)),
                [ApprovalTaskListRowBuilder.kHandler] = ReadOnly(implementationTask.CurrentHandler?.Name),
                [ApprovalTaskListRowBuilder.kAssigned] = ReadOnly(implementationTask.AssignedGroup),
                [ApprovalTaskListRowBuilder.kManagement] = Cell(implLogic.ManagementName(implementationTask, requestTask),
                    canEdit ? () => implActions.EditManagement(implementationTask, requestTask) : null),
                [ApprovalTaskListRowBuilder.kFreeText] = Cell(implementationTask.FreeText,
                    canEdit && PlanningImplTaskDisplayLogic.CanEditFreeText(implementationTask) ? () => implActions.EditFreeText(implementationTask, requestTask) : null),
                [ApprovalTaskListRowBuilder.kTimeRestriction] = ReadOnly(implLogic.TimeRestriction(implementationTask, requestTask))
            };
            AddRuleCells(cells, requestTask, implementationTask, canEdit);
            AddObjectCells(cells, requestTask, implementationTask, canEdit);
            return new PlanningTaskListRow(requestTask, implementationTask, cells) { TaskGroup = taskGroup };
        }

        /// <summary>Adds the device of rule implementation tasks and the rule position, action, tracking, source, destination and services of rule tasks.</summary>
        /// <param name="cells">The cells of the row.</param>
        /// <param name="requestTask">The request task the implementation task belongs to.</param>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="canEdit">Whether the implementation task may be changed.</param>
        private void AddRuleCells(Dictionary<string, TaskListCell> cells, WfReqTask requestTask, WfImplTask implementationTask, bool canEdit)
        {
            if (PlanningImplTaskDisplayLogic.IsRuleImplementationTask(implementationTask))
            {
                cells[kDevice] = Cell(implLogic.DeviceName(implementationTask, requestTask),
                    canEdit ? () => implActions.EditDevice(implementationTask, requestTask) : null);
            }
            if (!PlanningImplTaskDisplayLogic.IsRuleTask(implementationTask))
            {
                return;
            }
            cells[kRulePosition] = ReadOnly(RulePositionDisplayHelper.Format(implementationTask.RulePosition, userConfig));
            cells[ApprovalTaskListRowBuilder.kRuleAction] = ReadOnly(implLogic.RuleActionName(implementationTask));
            cells[ApprovalTaskListRowBuilder.kTrack] = ReadOnly(implLogic.TrackingName(implementationTask));
            cells[ApprovalTaskListRowBuilder.kSource] = Values(implLogic.NetworkDisplayRows(implementationTask, ElemFieldType.source));
            cells[ApprovalTaskListRowBuilder.kDestination] = Values(implLogic.NetworkDisplayRows(implementationTask, ElemFieldType.destination));
            cells[ApprovalTaskListRowBuilder.kService] = Values(implLogic.ServiceDisplayRows(implementationTask));
        }

        /// <summary>Adds the object and content of group and object tasks, and the rule uid of rule delete tasks.</summary>
        /// <param name="cells">The cells of the row.</param>
        /// <param name="requestTask">The request task the implementation task belongs to.</param>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="canEdit">Whether the implementation task may be changed.</param>
        private void AddObjectCells(Dictionary<string, TaskListCell> cells, WfReqTask requestTask, WfImplTask implementationTask, bool canEdit)
        {
            Func<Task>? editName = canEdit && PlanningImplTaskDisplayLogic.HasEditableName(implementationTask)
                ? () => implActions.EditName(implementationTask, requestTask)
                : null;
            if (PlanningImplTaskDisplayLogic.IsGroupTask(implementationTask))
            {
                cells[ApprovalTaskListRowBuilder.kObject] = Cell(PlanningImplTaskDisplayLogic.GroupName(implementationTask, requestTask), editName);
                cells[ApprovalTaskListRowBuilder.kContent] = GroupMembers(implementationTask);
            }
            else if (WfObjectTaskHelper.IsObjectTask(implementationTask.TaskType))
            {
                cells[ApprovalTaskListRowBuilder.kObject] = Cell(PlanningImplTaskDisplayLogic.ObjectName(implementationTask), editName);
                cells[ApprovalTaskListRowBuilder.kContent] = ReadOnly(PlanningImplTaskDisplayLogic.IsTimeObjectTask(implementationTask)
                    ? ""
                    : implLogic.ObjectContent(implementationTask));
            }
            else if (implementationTask.TaskType == WfTaskType.rule_delete.ToString())
            {
                cells[ApprovalTaskListRowBuilder.kObject] = ReadOnly(PlanningImplTaskDisplayLogic.RuleUid(implementationTask));
            }
        }

        /// <summary>Shows the members of a group; members of a modified group are marked as added, removed or unchanged.</summary>
        private TaskListCell GroupMembers(WfImplTask implementationTask)
        {
            List<PlanningImplElementRow> members = [.. implLogic.GroupMemberRows(implementationTask)
                .Where(member => !string.IsNullOrWhiteSpace(NameWithDetail(member)))];
            bool showMemberChanges = implementationTask.TaskType == WfTaskType.group_modify.ToString();
            return new TaskListCell([.. members.Select(NameWithDetail)], null)
            {
                Badges = showMemberChanges ? [.. members.Select(member => MemberChangeBadge(member.Action, userConfig))] : null
            };
        }

        /// <summary>Returns a read-only cell with one "name (content)" value per element; empty elements are left out.</summary>
        /// <param name="elements">The displayed elements.</param>
        private static TaskListCell Values(IEnumerable<PlanningImplElementRow> elements) =>
            new([.. elements.Select(NameWithDetail).Where(value => !string.IsNullOrWhiteSpace(value))], null);

        /// <summary>Formats an element as "name (content)", or the non-empty one of both.</summary>
        private static string NameWithDetail(PlanningImplElementRow element) => TaskListHelper.NameWithDetail(element.Name, element.Content);
    }
}
