using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Display;
using static FWO.Ui.Services.TaskListHelper;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Builds the rows of the approval task list view, one row per request task or bundle displayed as one.
    /// Every table shows only the information relevant for its tasks.
    /// Editors are offered only for fields enabled by the approver-allowed changes.
    /// </summary>
    public sealed class ApprovalTaskListRowBuilder
    {
        /// <summary>Column key of the task id.</summary>
        public const string kId = "id";
        /// <summary>Column key of the compliance status of access tasks.</summary>
        public const string kCompliance = "compliance";
        /// <summary>Column key of the task title.</summary>
        public const string kTitle = "title";
        /// <summary>Column key of the source zones.</summary>
        public const string kSourceZone = "source_zone";
        /// <summary>Column key of the destination zones.</summary>
        public const string kDestinationZone = "destination_zone";
        /// <summary>Column key of the source objects (name and ip address).</summary>
        public const string kSource = "source";
        /// <summary>Column key of the destination objects (name and ip address).</summary>
        public const string kDestination = "destination";
        /// <summary>Column key of the services.</summary>
        public const string kService = "service";
        /// <summary>Column key of the ordered object or group name.</summary>
        public const string kObject = "object";
        /// <summary>Column key of the object content (ip address, service definition or group members).</summary>
        public const string kContent = "content";
        /// <summary>Column key of the zones of an address object or group.</summary>
        public const string kZone = "zone";
        /// <summary>Column key of the task type.</summary>
        public const string kTaskType = "task_type";
        /// <summary>Column key of the task state.</summary>
        public const string kState = "state";
        /// <summary>Column key of the current handler.</summary>
        public const string kHandler = "handler";
        /// <summary>Column key of the assigned group.</summary>
        public const string kAssigned = "assigned";
        /// <summary>Column key of the owner.</summary>
        public const string kOwner = "owner";
        /// <summary>Column key of the management.</summary>
        public const string kManagement = "management";
        /// <summary>Column key of the rule action.</summary>
        public const string kRuleAction = "rule_action";
        /// <summary>Column key of the tracking.</summary>
        public const string kTrack = "track";
        /// <summary>Column key of the time restriction.</summary>
        public const string kTimeRestriction = "time_restriction";
        /// <summary>Column key of the reason.</summary>
        public const string kReason = "reason";
        /// <summary>Column key of the free text.</summary>
        public const string kFreeText = "free_text";

        private const int kAccessRank = 0;
        private const int kObjectRank = 1;
        private const int kGroupRank = 2;
        private const int kOtherRank = 3;

        private readonly ApprovalTaskViewContext context;
        private readonly UserConfig userConfig;
        private readonly WfStateDict states;

        /// <summary>Initializes the row builder.</summary>
        /// <param name="context">Display logic, visibility and editor callbacks of the approval task view.</param>
        /// <param name="userConfig">The current user configuration.</param>
        /// <param name="states">The workflow state names.</param>
        public ApprovalTaskListRowBuilder(ApprovalTaskViewContext context, UserConfig userConfig, WfStateDict states)
        {
            this.context = context;
            this.userConfig = userConfig;
            this.states = states;
        }

        /// <summary>Compliance results per task id, as returned by the conditional policy check; null if not checked.</summary>
        public IReadOnlyDictionary<long, RequestTaskComplianceResult>? ComplianceResults { get; init; }

        /// <summary>Returns the table a task is displayed in.</summary>
        /// <param name="task">The request task.</param>
        public static TaskListTable TableOf(WfReqTask task)
        {
            if (ApprovalTaskDetailsEditorService.IsAccessTask(task))
            {
                return TaskListTable.Access;
            }
            if (ApprovalTaskDetailsEditorService.IsGroupTask(task))
            {
                return WfObjectTaskHelper.OrdersServiceObject(task) ? TaskListTable.Service : TaskListTable.Address;
            }
            if (WfObjectTaskHelper.IsObjectTask(task.TaskType))
            {
                return WfObjectTaskHelper.GetKind(RequestedElement(task)) switch
                {
                    ObjectTaskKind.Network => TaskListTable.Address,
                    ObjectTaskKind.Service => TaskListTable.Service,
                    _ => TaskListTable.Other
                };
            }
            return TaskListTable.Other;
        }

        /// <summary>Returns the tables containing at least one of the task groups, in display order.</summary>
        /// <param name="taskGroups">The task groups: single tasks or bundles displayed as one.</param>
        public static List<TaskListTable> UsedTables(IEnumerable<List<WfReqTask>> taskGroups)
        {
            HashSet<TaskListTable> usedTables = [.. taskGroups
                .Where(taskGroup => taskGroup.Count > 0)
                .Select(taskGroup => TableOf(taskGroup[0]))];
            return [.. Enum.GetValues<TaskListTable>().Where(usedTables.Contains)];
        }

        /// <summary>Returns the visible columns of a table in display order.</summary>
        /// <param name="table">The table.</param>
        /// <param name="visibility">The configured field visibility.</param>
        /// <param name="showCompliance">Shows the compliance status of access tasks directly after the id.</param>
        /// <param name="objectVisibility">The configured visibility of handler, assigned, management and reason of
        /// object and group tasks; null if the table is not restricted to object and group tasks (see
        /// <see cref="ObjectVisibilityFor"/>), then the columns of other tasks apply.</param>
        public static List<TaskListColumn> VisibleColumns(TaskListTable table, AccessTaskFieldVisibilityConfig visibility,
            bool showCompliance = false, ObjectTaskFieldVisibilityConfig? objectVisibility = null)
        {
            List<TaskListColumn> columns = [new(kId, "id")];
            AddIf(columns, showCompliance && table == TaskListTable.Access, new(kCompliance, "approval_list_compliance"));
            // access tasks show their rule content first; title, task type and state follow just before the handler
            if (table != TaskListTable.Access)
            {
                columns.Add(new(kTaskType, "task_type"));
                columns.Add(new(kState, "state"));
                columns.Add(new(kTitle, "title"));
            }
            switch (table)
            {
                case TaskListTable.Access:
                    AddAccessColumns(columns, visibility);
                    break;
                case TaskListTable.Address:
                    columns.Add(new(kObject, "object"));
                    columns.Add(new(kContent, "content"));
                    columns.Add(new(kZone, "zone"));
                    break;
                case TaskListTable.Service:
                    columns.Add(new(kObject, "object"));
                    columns.Add(new(kContent, "content"));
                    break;
                default:
                    columns.Add(new(kObject, "object"));
                    break;
            }
            bool isAccess = table == TaskListTable.Access;
            ObjectTaskFieldVisibilityConfig metadataVisibility = MetadataVisibility(table, visibility, objectVisibility);
            AddIf(columns, !isAccess && metadataVisibility.ShowManagement, new(kManagement, "management"));
            AddIf(columns, metadataVisibility.ShowReason, new(kReason, "reason"));
            AddIf(columns, visibility.ShowFreeText && table is TaskListTable.Access or TaskListTable.Other,
                new(kFreeText, "free_text"));
            if (isAccess)
            {
                AddIf(columns, visibility.ShowTitle, new(kTitle, "title"));
                columns.Add(new(kTaskType, "task_type"));
                columns.Add(new(kState, "state"));
            }
            AddIf(columns, metadataVisibility.ShowHandler, new(kHandler, "handler"));
            AddIf(columns, metadataVisibility.ShowAssigned, new(kAssigned, "assigned"));
            return columns;
        }

        /// <summary>
        /// Returns the object and group task visibility if all tasks of the table are object or group tasks, otherwise
        /// null: the address and service tables only hold those, the other tasks table may mix them with e.g. generic
        /// or interface tasks, which keep their columns.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="taskGroups">All request task groups of the ticket.</param>
        /// <param name="objectVisibility">The configured object and group task visibility.</param>
        public static ObjectTaskFieldVisibilityConfig? ObjectVisibilityFor(TaskListTable table,
            IEnumerable<List<WfReqTask>> taskGroups, ObjectTaskFieldVisibilityConfig objectVisibility)
        {
            bool onlyObjectTasks = taskGroups
                .Where(taskGroup => taskGroup.Count > 0 && TableOf(taskGroup[0]) == table)
                .All(taskGroup => IsObjectOrGroupTask(taskGroup[0]));
            return table != TaskListTable.Access && onlyObjectTasks ? objectVisibility : null;
        }

        /// <summary>Returns whether a task is an object or group task.</summary>
        /// <param name="task">The request task.</param>
        public static bool IsObjectOrGroupTask(WfReqTask task)
        {
            return WfObjectTaskHelper.IsObjectTask(task.TaskType) || ApprovalTaskDetailsEditorService.IsGroupTask(task);
        }

        /// <summary>
        /// Visibility of handler, assigned, management and reason: access tasks use their own settings, tables of
        /// object and group tasks the object task settings; other tasks always show handler and assigned and follow
        /// the access task settings for management and reason.
        /// </summary>
        private static ObjectTaskFieldVisibilityConfig MetadataVisibility(TaskListTable table,
            AccessTaskFieldVisibilityConfig visibility, ObjectTaskFieldVisibilityConfig? objectVisibility)
        {
            if (table == TaskListTable.Access)
            {
                return new() { ShowHandler = visibility.ShowHandler, ShowAssigned = visibility.ShowAssigned, ShowManagement = false, ShowReason = visibility.ShowReason };
            }
            return objectVisibility ?? new() { ShowManagement = visibility.ShowManagement, ShowReason = visibility.ShowReason };
        }

        private static void AddAccessColumns(List<TaskListColumn> columns, AccessTaskFieldVisibilityConfig visibility)
        {
            columns.Add(new(kSourceZone, "source_zone"));
            columns.Add(new(kDestinationZone, "destination_zone"));
            columns.Add(new(kSource, "source"));
            columns.Add(new(kDestination, "destination"));
            columns.Add(new(kService, "service"));
            AddIf(columns, visibility.ShowValidFrom || visibility.ShowValidTo, new(kTimeRestriction, "time_restriction"));
            AddIf(columns, visibility.ShowOwner, new(kOwner, "owner"));
            AddIf(columns, visibility.ShowManagement, new(kManagement, "management"));
            AddIf(columns, visibility.ShowRuleAction, new(kRuleAction, "rule_action"));
            AddIf(columns, visibility.ShowTracking, new(kTrack, "track"));
        }

        /// <summary>
        /// Builds the rows of one table, one row per task group (a single task or a bundle displayed as one),
        /// ordered by task type (access, object, group, other tasks) and then by task id.
        /// </summary>
        /// <param name="taskGroups">All request task groups of the ticket.</param>
        /// <param name="table">The table to build the rows for.</param>
        public List<ApprovalTaskListRow> Build(IEnumerable<List<WfReqTask>> taskGroups, TaskListTable table)
        {
            return [.. taskGroups
                .Where(taskGroup => taskGroup.Count > 0 && TableOf(taskGroup[0]) == table)
                .OrderBy(taskGroup => TaskTypeRank(taskGroup[0]))
                .ThenBy(taskGroup => taskGroup.Min(task => task.Id))
                .Select(taskGroup => BuildRow(taskGroup, table))];
        }

        /// <summary>Returns the display rank of a task type: access, object, group, then all other tasks.</summary>
        /// <param name="task">The request task.</param>
        public static int TaskTypeRank(WfReqTask task)
        {
            if (ApprovalTaskDetailsEditorService.IsAccessTask(task))
            {
                return kAccessRank;
            }
            if (WfObjectTaskHelper.IsObjectTask(task.TaskType))
            {
                return kObjectRank;
            }
            return ApprovalTaskDetailsEditorService.IsGroupTask(task) ? kGroupRank : kOtherRank;
        }

        private ApprovalTaskListRow BuildRow(List<WfReqTask> taskGroup, TaskListTable table)
        {
            WfReqTask task = taskGroup[0];
            Dictionary<string, TaskListCell> cells = new()
            {
                [kId] = ReadOnly(ApprovalTaskDetailsEditorService.TaskIds(taskGroup)),
                [kTitle] = Text(task, taskGroup, WorkflowEditableFieldKeys.Title, member => member.Title),
                [kTaskType] = ReadOnly(string.IsNullOrWhiteSpace(task.TaskType) ? "" : userConfig.GetText(task.TaskType)),
                [kState] = ReadOnly(string.Join(", ", taskGroup.Select(member => states.GetName(member.StateId)).Distinct())),
                [kHandler] = ReadOnly(context.DisplayLogic.Handlers(taskGroup)),
                [kAssigned] = ReadOnly(context.DisplayLogic.AssignedGroups(taskGroup)),
                [kManagement] = Metadata(context.DisplayLogic.ManagementIds(taskGroup), taskGroup, WorkflowEditableFieldKeys.Management, () => context.EditManagement(task)),
                [kReason] = Text(task, taskGroup, WorkflowEditableFieldKeys.Reason, member => member.Reason),
                [kFreeText] = Text(task, taskGroup, WorkflowEditableFieldKeys.FreeText, member => member.FreeText)
            };
            switch (table)
            {
                case TaskListTable.Access:
                    AddAccessCells(cells, task, taskGroup);
                    break;
                case TaskListTable.Address:
                case TaskListTable.Service:
                    AddObjectCells(cells, task, table);
                    break;
                default:
                    cells[kObject] = new(NonEmptyValues(task.Elements.Select(element => element.Name ?? "")), ObjectTaskEdit(task));
                    break;
            }
            if (IsObjectOrGroupTask(task))
            {
                ClearHiddenObjectTaskCells(cells);
            }
            return new ApprovalTaskListRow(task, cells)
            {
                TaskGroup = taskGroup,
                Compliance = table == TaskListTable.Access ? ApprovalTaskCompliance.Of(taskGroup, ComplianceResults) : null
            };
        }

        /// <summary>Empties the cells an object or group task hides, as its row may share the other tasks table with other tasks.</summary>
        private void ClearHiddenObjectTaskCells(Dictionary<string, TaskListCell> cells)
        {
            ObjectTaskFieldVisibilityConfig objectVisibility = context.ObjectVisibility;
            if (!objectVisibility.ShowHandler)
            {
                cells[kHandler] = ReadOnly("");
            }
            if (!objectVisibility.ShowAssigned)
            {
                cells[kAssigned] = ReadOnly("");
            }
            if (!objectVisibility.ShowManagement)
            {
                cells[kManagement] = ReadOnly("");
            }
            if (!objectVisibility.ShowReason)
            {
                cells[kReason] = ReadOnly("");
            }
        }

        private void AddAccessCells(Dictionary<string, TaskListCell> cells, WfReqTask task, List<WfReqTask> taskGroup)
        {
            cells[kOwner] = Metadata(context.DisplayLogic.Owners(taskGroup), taskGroup, WorkflowEditableFieldKeys.Owner, () => context.EditOwner(task));
            cells[kRuleAction] = Metadata(context.DisplayLogic.RuleActions(taskGroup), taskGroup, WorkflowEditableFieldKeys.RuleAction, () => context.EditRuleAction(task));
            cells[kTrack] = Metadata(context.DisplayLogic.Trackings(taskGroup), taskGroup, WorkflowEditableFieldKeys.Track, () => context.EditTracking(task));
            cells[kTimeRestriction] = TimeRestriction(task, taskGroup);

            List<ApprovalTaskNetworkElementRow> sources = [.. context.DisplayLogic.NetworkElementRows(taskGroup, ElemFieldType.source)];
            List<ApprovalTaskNetworkElementRow> destinations = [.. context.DisplayLogic.NetworkElementRows(taskGroup, ElemFieldType.destination)];
            List<ApprovalTaskServiceElementRow> services = [.. context.DisplayLogic.ServiceElementRows(taskGroup, ElemFieldType.service)];
            Func<Task>? editSource = AccessFieldEdit(task, taskGroup, WorkflowEditableFieldKeys.Source);
            Func<Task>? editDestination = AccessFieldEdit(task, taskGroup, WorkflowEditableFieldKeys.Destination);

            cells[kSourceZone] = new(DistinctValues(sources.Select(row => row.Zone)), editSource);
            cells[kDestinationZone] = new(DistinctValues(destinations.Select(row => row.Zone)), editDestination);
            cells[kSource] = new(NonEmptyValues(sources.Select(row => NameWithDetail(row.Name, row.Ip))), editSource);
            cells[kDestination] = new(NonEmptyValues(destinations.Select(row => NameWithDetail(row.Name, row.Ip))), editDestination);
            cells[kService] = new(NonEmptyValues(services.Select(row => NameWithDetail(row.Name, row.Content))),
                AccessFieldEdit(task, taskGroup, WorkflowEditableFieldKeys.Services));
        }

        private Func<Task>? AccessFieldEdit(WfReqTask task, List<WfReqTask> taskGroup, string fieldKey)
        {
            return context.CanEdit(taskGroup, fieldKey) ? () => context.EditField(task, fieldKey) : null;
        }

        private void AddObjectCells(Dictionary<string, TaskListCell> cells, WfReqTask task, TaskListTable table)
        {
            if (ApprovalTaskDetailsEditorService.IsGroupTask(task))
            {
                AddGroupCells(cells, task, table);
                return;
            }

            Func<Task>? edit = ObjectTaskEdit(task);
            WfReqElement? requested = RequestedElement(task);
            WfReqElement? original = WfObjectTaskHelper.GetOriginalElement(task);
            cells[kObject] = new(SingleValue(Change(original?.Name, requested?.Name)), edit);
            cells[kContent] = new(SingleValue(Change(original == null ? null : ObjectContent(original, table),
                requested == null ? null : ObjectContent(requested, table))), edit);
            cells[kZone] = new(SingleValue(requested == null ? "" : context.DisplayLogic.ZonesOf(requested)), edit);
        }

        /// <summary>Opens the object-task editor of an object task (network, service or time object), if editable.</summary>
        private Func<Task>? ObjectTaskEdit(WfReqTask task)
        {
            List<WfReqTask> taskAsList = [task];
            bool editable = WfObjectTaskHelper.IsObjectTask(task.TaskType)
                && (context.CanEdit(taskAsList, WorkflowEditableFieldKeys.Name) || context.CanEdit(taskAsList, WorkflowEditableFieldKeys.Content));
            return editable ? () => context.EditObjectTask(task) : null;
        }

        private void AddGroupCells(Dictionary<string, TaskListCell> cells, WfReqTask task, TaskListTable table)
        {
            List<WfReqTask> taskAsList = [task];
            string memberField = table == TaskListTable.Service ? ElemFieldType.service.ToString() : ElemFieldType.source.ToString();
            List<WfReqElement> members = [.. task.Elements.Where(element => element.Field == memberField)];
            string groupName = FirstNonEmpty(task.GetAddInfoValue(AdditionalInfoKeys.GrpName),
                members.Select(member => member.GroupName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)));
            Func<Task>? editName = context.CanEdit(taskAsList, WorkflowEditableFieldKeys.Name)
                ? () => context.EditText(task, WorkflowEditableFieldKeys.Name, groupName)
                : null;
            Func<Task>? editMembers = context.CanEdit(taskAsList, WorkflowEditableFieldKeys.GroupMembers)
                ? () => context.EditGroupMembers(task)
                : null;

            cells[kObject] = new(SingleValue(groupName), editName);
            List<WfReqElement> displayedMembers = [.. members.Where(member => !string.IsNullOrWhiteSpace(MemberValue(member, table)))];
            // Only a modified group mixes added, removed and unchanged members; a new group consists of added members only.
            bool showMemberChanges = task.TaskType == WfTaskType.group_modify.ToString();
            cells[kContent] = new(displayedMembers.Select(member => MemberValue(member, table)).ToList(), editMembers)
            {
                Badges = showMemberChanges ? [.. displayedMembers.Select(member => MemberChangeBadge(member.RequestAction, userConfig))] : null
            };
            cells[kZone] = new(DistinctValues(members.Where(member => !IsRemoved(member))
                .SelectMany(member => context.DisplayLogic.ZonesOf(member).Split(", ", StringSplitOptions.RemoveEmptyEntries))), editMembers);
        }

        private string MemberValue(WfReqElement member, TaskListTable table)
        {
            return NameWithDetail(member.Name ?? "", ObjectContent(member, table));
        }

        /// <summary>Shows a modification as "old -> new" (with an arrow); unchanged or new values are shown once.</summary>
        private static string Change(string? oldValue, string? newValue)
        {
            if (string.IsNullOrWhiteSpace(oldValue) || oldValue == newValue)
            {
                return newValue ?? "";
            }
            return $"{oldValue} \u2192 {newValue}";
        }

        private string ObjectContent(WfReqElement element, TaskListTable table)
        {
            return table == TaskListTable.Service
                ? context.DisplayLogic.ServiceContent(element)
                : NwObjDisplay.DisplayIp(element.IpString, element.IpEnd);
        }

        private static WfReqElement? RequestedElement(WfReqTask task)
        {
            return WfObjectTaskHelper.GetRequestedElement(task) ?? task.Elements.FirstOrDefault();
        }

        private static bool IsRemoved(WfReqElement element) =>
            string.Equals(element.RequestAction, RequestAction.delete.ToString(), StringComparison.OrdinalIgnoreCase);

        private TaskListCell Text(WfReqTask task, List<WfReqTask> taskGroup, string fieldKey, Func<WfReqTask, string?> selector)
        {
            string value = selector(task) ?? "";
            Func<Task>? edit = context.CanEdit(taskGroup, fieldKey) ? () => context.EditText(task, fieldKey, value) : null;
            return new TaskListCell(DistinctValues(taskGroup.Select(member => selector(member) ?? "")), edit);
        }

        private TaskListCell Metadata(string value, List<WfReqTask> taskGroup, string fieldKey, Func<Task> edit)
        {
            return new TaskListCell(SingleValue(value), context.CanEdit(taskGroup, fieldKey) ? edit : null);
        }

        private TaskListCell TimeRestriction(WfReqTask task, List<WfReqTask> taskGroup)
        {
            string value = context.DisplayLogic.TimeObjectDisplay(taskGroup, context.Visibility);
            Func<Task>? edit = context.CanEditTimeObject(taskGroup)
                ? () => context.EditTargetDates(task, context.EditableTimeObjectField(taskGroup))
                : null;
            return new TaskListCell(SingleValue(value), edit);
        }

        private static string FirstNonEmpty(string? first, string? second) =>
            !string.IsNullOrWhiteSpace(first) ? first : second ?? "";

    }
}
