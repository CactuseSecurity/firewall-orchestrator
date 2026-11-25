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
        private static readonly List<string> kRequestTaskKeys =
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
        public static List<TaskListColumn> VisibleColumns(TaskListTable table, PlanningTaskListMode mode,
            AccessTaskFieldVisibilityConfig visibility, ObjectTaskFieldVisibilityConfig? objectVisibility = null)
        {
            bool allTasks = mode == PlanningTaskListMode.AllTasks;
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
                AddObjectColumns(columns, table, allTasks);
            }
            AddIf(columns, allTasks && metadataVisibility.ShowReason, new(ApprovalTaskListRowBuilder.kReason, "reason"));
            columns.Add(new(ApprovalTaskListRowBuilder.kFreeText, "free_text"));
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
            List<PlanningTaskListRow> rows = [];
            foreach (ApprovalTaskListRow requestRow in requestRows)
            {
                if (mode == PlanningTaskListMode.AllTasks)
                {
                    rows.Add(BuildRequestTaskRow(requestRow));
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

        private static void AddObjectColumns(List<TaskListColumn> columns, TaskListTable table, bool allTasks)
        {
            columns.Add(new(ApprovalTaskListRowBuilder.kTaskType, "task_type"));
            columns.Add(new(ApprovalTaskListRowBuilder.kState, "state"));
            columns.Add(new(ApprovalTaskListRowBuilder.kTitle, "title"));
            columns.Add(new(ApprovalTaskListRowBuilder.kObject, "object"));
            AddIf(columns, table != TaskListTable.Other, new(ApprovalTaskListRowBuilder.kContent, "content"));
            AddIf(columns, allTasks && table == TaskListTable.Address, new(ApprovalTaskListRowBuilder.kZone, "zone"));
            columns.Add(new(ApprovalTaskListRowBuilder.kManagement, "management"));
        }

        private PlanningTaskListRow BuildRequestTaskRow(ApprovalTaskListRow requestRow)
        {
            Dictionary<string, TaskListCell> cells = new()
            {
                [kLevel] = ReadOnly(userConfig.GetText("RequestTask"))
            };
            foreach (string key in kRequestTaskKeys)
            {
                cells[key] = requestRow.Cell(key);
            }
            return new PlanningTaskListRow(requestRow.Task, null, cells) { TaskGroup = requestRow.TaskGroup };
        }

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

        private static TaskListCell Values(IEnumerable<PlanningImplElementRow> elements) =>
            new([.. elements.Select(NameWithDetail).Where(value => !string.IsNullOrWhiteSpace(value))], null);

        /// <summary>Formats an element as "name (content)", or the non-empty one of both.</summary>
        private static string NameWithDetail(PlanningImplElementRow element) => TaskListHelper.NameWithDetail(element.Name, element.Content);
    }
}
