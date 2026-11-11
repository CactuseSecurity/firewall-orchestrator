using System.Text.Json;

namespace FWO.Data.Workflow
{
    /// <summary>Contains the serialized keys for fields approvers may change.</summary>
    public static class WorkflowEditableFieldKeys
    {
        /// <summary>The ticket or task title field key.</summary>
        public const string Title = "title";
        /// <summary>The ticket priority field key.</summary>
        public const string Priority = "priority";
        /// <summary>The ticket deadline field key.</summary>
        public const string Deadline = "deadline";
        /// <summary>The reason field key.</summary>
        public const string Reason = "reason";
        /// <summary>The free-text field key.</summary>
        public const string FreeText = "free_text";
        /// <summary>The management field key.</summary>
        public const string Management = "management";
        /// <summary>The gateways field key.</summary>
        public const string Gateways = "gateways";
        /// <summary>The gateway field key.</summary>
        public const string Gateway = "gateway";
        /// <summary>The rule-action field key.</summary>
        public const string RuleAction = "rule_action";
        /// <summary>The tracking field key.</summary>
        public const string Track = "track";
        /// <summary>The validity-start field key.</summary>
        public const string ValidFrom = "valid_from";
        /// <summary>The validity-end field key.</summary>
        public const string ValidTo = "valid_to";
        /// <summary>The source field key.</summary>
        public const string Source = "source";
        /// <summary>The destination field key.</summary>
        public const string Destination = "destination";
        /// <summary>The services field key.</summary>
        public const string Services = "services";
        /// <summary>The object or service content field key.</summary>
        public const string Content = "content";
        /// <summary>The group-members field key.</summary>
        public const string GroupMembers = "group_members";
        /// <summary>The rules field key.</summary>
        public const string Rules = "rules";
        /// <summary>The owner field key.</summary>
        public const string Owner = "owner";
        /// <summary>The requesting-owner field key.</summary>
        public const string RequestingOwner = "requesting_owner";
        /// <summary>The object or service name field key.</summary>
        public const string Name = "name";
    }

    /// <summary>Describes one field that can be enabled for approval-phase editing.</summary>
    public class WorkflowEditableFieldDefinition
    {
        /// <summary>Gets or sets the serialized field key.</summary>
        public string Key { get; set; } = "";

        /// <summary>Initializes an empty field definition.</summary>
        public WorkflowEditableFieldDefinition()
        { }

        /// <summary>Initializes a field definition with a serialized key.</summary>
        /// <param name="key">The serialized field key.</param>
        public WorkflowEditableFieldDefinition(string key)
        {
            Key = key;
        }
    }

    /// <summary>Stores the fields enabled for approver changes on tickets and tasks.</summary>
    public class ApproverAllowedChangesConfig
    {
        /// <summary>Gets or sets the enabled ticket field keys.</summary>
        public List<string> TicketFields { get; set; } = [];
        /// <summary>Gets or sets enabled field keys grouped by task type.</summary>
        public Dictionary<string, List<string>> TaskTypeFields { get; set; } = [];

        /// <summary>Parses a serialized configuration and fills missing task-type entries.</summary>
        /// <param name="serializedConfig">The serialized configuration, if available.</param>
        /// <returns>The parsed configuration.</returns>
        public static ApproverAllowedChangesConfig Parse(string? serializedConfig)
        {
            ApproverAllowedChangesConfig config;
            if (string.IsNullOrWhiteSpace(serializedConfig))
            {
                config = new();
            }
            else
            {
                try
                {
                    config = JsonSerializer.Deserialize<ApproverAllowedChangesConfig>(serializedConfig) ?? new();
                }
                catch (JsonException)
                {
                    config = new();
                }
            }
            config.TicketFields ??= [];
            config.TaskTypeFields ??= [];
            foreach (WfTaskType taskType in ApproverAllowedChangesCatalog.TaskTypeFields.Keys)
            {
                if (!config.TaskTypeFields.ContainsKey(taskType.ToString()))
                {
                    config.TaskTypeFields[taskType.ToString()] = [];
                }
            }
            return config;
        }

        /// <summary>Serializes this configuration for storage.</summary>
        /// <returns>The serialized configuration.</returns>
        public string ToConfigValue()
        {
            return JsonSerializer.Serialize(this);
        }

        /// <summary>Determines whether a ticket field is enabled.</summary>
        /// <param name="fieldKey">The serialized field key.</param>
        /// <returns><see langword="true"/> when the field is enabled.</returns>
        public bool IsTicketFieldAllowed(string fieldKey)
        {
            return TicketFields.Contains(fieldKey);
        }

        /// <summary>Determines whether a task field is enabled for a task type.</summary>
        /// <param name="taskType">The task type.</param>
        /// <param name="fieldKey">The serialized field key.</param>
        /// <returns><see langword="true"/> when the field is enabled.</returns>
        public bool IsTaskFieldAllowed(WfTaskType taskType, string fieldKey)
        {
            return TaskTypeFields.TryGetValue(taskType.ToString(), out List<string>? fields)
                && fields.Contains(fieldKey);
        }

        /// <summary>Determines whether any ticket field changes are enabled.</summary>
        /// <returns><see langword="true"/> when at least one ticket field is enabled.</returns>
        public bool HasTicketFieldChanges()
        {
            return TicketFields.Count > 0;
        }

        /// <summary>Determines whether any changes are enabled for a task type.</summary>
        /// <param name="taskType">The task type.</param>
        /// <returns><see langword="true"/> when at least one field is enabled.</returns>
        public bool HasTaskFieldChanges(WfTaskType taskType)
        {
            return TaskTypeFields.TryGetValue(taskType.ToString(), out List<string>? fields)
                && fields.Count > 0;
        }

        /// <summary>Enables or disables a ticket field.</summary>
        /// <param name="fieldKey">The serialized field key.</param>
        /// <param name="allowed">Whether the field should be enabled.</param>
        public void SetTicketField(string fieldKey, bool allowed)
        {
            SetField(TicketFields, fieldKey, allowed);
        }

        /// <summary>Enables or disables a task field for a task type.</summary>
        /// <param name="taskType">The task type.</param>
        /// <param name="fieldKey">The serialized field key.</param>
        /// <param name="allowed">Whether the field should be enabled.</param>
        public void SetTaskField(WfTaskType taskType, string fieldKey, bool allowed)
        {
            string taskTypeKey = taskType.ToString();
            if (!TaskTypeFields.ContainsKey(taskTypeKey))
            {
                TaskTypeFields[taskTypeKey] = [];
            }
            SetField(TaskTypeFields[taskTypeKey], fieldKey, allowed);
        }

        private static void SetField(List<string> fields, string fieldKey, bool allowed)
        {
            if (allowed)
            {
                if (!fields.Contains(fieldKey))
                {
                    fields.Add(fieldKey);
                }
            }
            else
            {
                fields.Remove(fieldKey);
            }
        }
    }

    /// <summary>Provides approval-phase editability checks.</summary>
    public static class ApproverAllowedChangesAccess
    {
        /// <summary>Describes the current task context used for editability checks.</summary>
        public class TaskFieldEditContext
        {
            /// <summary>Gets or sets whether the current phase is approval.</summary>
            public bool IsApprovalPhase { get; set; }
            /// <summary>Gets or sets whether the request-task editor is active.</summary>
            public bool EditReqTaskMode { get; set; }
            /// <summary>Gets or sets whether approval-task editing is active.</summary>
            public bool ApproveReqTaskMode { get; set; }
            /// <summary>Gets or sets whether the current view is read-only.</summary>
            public bool ReadOnlyMode { get; set; }
            /// <summary>Gets or sets the task type.</summary>
            public WfTaskType TaskType { get; set; }
            /// <summary>Gets or sets whether implementation tasks already exist.</summary>
            public bool HasImplementationTasks { get; set; }
        }

        /// <summary>Determines whether a field is copied to implementation tasks.</summary>
        /// <param name="taskType">The request-task type.</param>
        /// <param name="fieldKey">The serialized field key.</param>
        /// <returns><see langword="true"/> when the field is copied.</returns>
        public static bool IsCopiedToImplementationTask(WfTaskType taskType, string fieldKey)
        {
            return fieldKey switch
            {
                WorkflowEditableFieldKeys.Title => true,
                WorkflowEditableFieldKeys.Gateways => taskType == WfTaskType.access,
                WorkflowEditableFieldKeys.Gateway => taskType == WfTaskType.rule_modify || taskType == WfTaskType.rule_delete,
                WorkflowEditableFieldKeys.RuleAction => taskType == WfTaskType.access || taskType == WfTaskType.rule_modify || taskType == WfTaskType.rule_delete,
                WorkflowEditableFieldKeys.Track => taskType == WfTaskType.access || taskType == WfTaskType.rule_modify || taskType == WfTaskType.rule_delete,
                WorkflowEditableFieldKeys.ValidFrom => taskType == WfTaskType.access || taskType == WfTaskType.rule_modify || taskType == WfTaskType.rule_delete,
                WorkflowEditableFieldKeys.ValidTo => taskType == WfTaskType.access || taskType == WfTaskType.rule_modify || taskType == WfTaskType.rule_delete,
                WorkflowEditableFieldKeys.FreeText => taskType == WfTaskType.generic,
                WorkflowEditableFieldKeys.Source => taskType == WfTaskType.access,
                WorkflowEditableFieldKeys.Destination => taskType == WfTaskType.access,
                WorkflowEditableFieldKeys.Services => taskType == WfTaskType.access,
                WorkflowEditableFieldKeys.Rules => taskType == WfTaskType.rule_modify || taskType == WfTaskType.rule_delete,
                _ => false
            };
        }

        /// <summary>Determines whether a ticket field can be edited in the current view.</summary>
        /// <param name="config">The approver edit configuration.</param>
        /// <param name="isApprovalPhase">Whether the ticket is in approval.</param>
        /// <param name="editTicketMode">Whether normal ticket editing is active.</param>
        /// <param name="readOnlyMode">Whether the view is read-only.</param>
        /// <param name="fieldKey">The serialized field key.</param>
        /// <returns><see langword="true"/> when editing is allowed.</returns>
        public static bool CanEditTicketField(ApproverAllowedChangesConfig config, bool isApprovalPhase, bool editTicketMode, bool readOnlyMode, string fieldKey)
        {
            return editTicketMode || isApprovalPhase && !readOnlyMode && config.IsTicketFieldAllowed(fieldKey);
        }

        /// <summary>Determines whether ticket changes can be saved in the current view.</summary>
        /// <param name="config">The approver edit configuration.</param>
        /// <param name="isApprovalPhase">Whether the ticket is in approval.</param>
        /// <param name="editTicketMode">Whether normal ticket editing is active.</param>
        /// <param name="readOnlyMode">Whether the view is read-only.</param>
        /// <returns><see langword="true"/> when saving is allowed.</returns>
        public static bool CanSaveTicketChanges(ApproverAllowedChangesConfig config, bool isApprovalPhase, bool editTicketMode, bool readOnlyMode)
        {
            return editTicketMode || isApprovalPhase && !readOnlyMode && config.HasTicketFieldChanges();
        }

        /// <summary>Determines whether a task field can be edited in the current view.</summary>
        /// <param name="config">The approver edit configuration.</param>
        /// <param name="context">The current task edit context.</param>
        /// <param name="fieldKey">The serialized field key.</param>
        /// <returns><see langword="true"/> when editing is allowed.</returns>
        public static bool CanEditTaskField(ApproverAllowedChangesConfig config, TaskFieldEditContext context, string fieldKey)
        {
            return context.EditReqTaskMode || context.IsApprovalPhase && context.ApproveReqTaskMode && !context.ReadOnlyMode
                && config.IsTaskFieldAllowed(context.TaskType, fieldKey)
                && (!context.HasImplementationTasks || !IsCopiedToImplementationTask(context.TaskType, fieldKey));
        }

        /// <summary>Determines whether task changes can be saved in the current view.</summary>
        /// <param name="config">The approver edit configuration.</param>
        /// <param name="context">The current task edit context.</param>
        /// <returns><see langword="true"/> when saving is allowed.</returns>
        public static bool CanSaveTaskChanges(ApproverAllowedChangesConfig config, TaskFieldEditContext context)
        {
            return context.EditReqTaskMode || context.IsApprovalPhase && context.ApproveReqTaskMode && !context.ReadOnlyMode
                && config.TaskTypeFields.TryGetValue(context.TaskType.ToString(), out List<string>? fields)
                && fields.Any(fieldKey => !context.HasImplementationTasks || !IsCopiedToImplementationTask(context.TaskType, fieldKey));
        }
    }

    /// <summary>Lists the editable fields available for each workflow object type.</summary>
    public static class ApproverAllowedChangesCatalog
    {
        /// <summary>Gets the editable ticket fields.</summary>
        public static readonly List<WorkflowEditableFieldDefinition> TicketFields =
        [
            new(WorkflowEditableFieldKeys.Title),
            new(WorkflowEditableFieldKeys.Priority),
            new(WorkflowEditableFieldKeys.Deadline),
            new(WorkflowEditableFieldKeys.Reason)
        ];

        /// <summary>Gets the editable fields grouped by task type.</summary>
        public static readonly Dictionary<WfTaskType, List<WorkflowEditableFieldDefinition>> TaskTypeFields = new()
        {
            [WfTaskType.generic] =
            [
                new(WorkflowEditableFieldKeys.Title),
                new(WorkflowEditableFieldKeys.FreeText)
            ],
            [WfTaskType.access] =
            [
                new(WorkflowEditableFieldKeys.Title),
                new(WorkflowEditableFieldKeys.Owner),
                new(WorkflowEditableFieldKeys.Management),
                new(WorkflowEditableFieldKeys.Gateways),
                new(WorkflowEditableFieldKeys.RuleAction),
                new(WorkflowEditableFieldKeys.Track),
                new(WorkflowEditableFieldKeys.ValidFrom),
                new(WorkflowEditableFieldKeys.ValidTo),
                new(WorkflowEditableFieldKeys.Reason),
                new(WorkflowEditableFieldKeys.FreeText),
                new(WorkflowEditableFieldKeys.Source),
                new(WorkflowEditableFieldKeys.Destination),
                new(WorkflowEditableFieldKeys.Services)
            ],
            [WfTaskType.rule_modify] =
            [
                new(WorkflowEditableFieldKeys.Title),
                new(WorkflowEditableFieldKeys.Management),
                new(WorkflowEditableFieldKeys.Gateway),
                new(WorkflowEditableFieldKeys.RuleAction),
                new(WorkflowEditableFieldKeys.Track),
                new(WorkflowEditableFieldKeys.ValidFrom),
                new(WorkflowEditableFieldKeys.ValidTo),
                new(WorkflowEditableFieldKeys.Reason),
                new(WorkflowEditableFieldKeys.Rules)
            ],
            [WfTaskType.rule_delete] =
            [
                new(WorkflowEditableFieldKeys.Title),
                new(WorkflowEditableFieldKeys.Management),
                new(WorkflowEditableFieldKeys.Gateway),
                new(WorkflowEditableFieldKeys.RuleAction),
                new(WorkflowEditableFieldKeys.Track),
                new(WorkflowEditableFieldKeys.ValidFrom),
                new(WorkflowEditableFieldKeys.ValidTo),
                new(WorkflowEditableFieldKeys.Reason),
                new(WorkflowEditableFieldKeys.Rules)
            ],
            [WfTaskType.new_interface] =
            [
                new(WorkflowEditableFieldKeys.Title),
                new(WorkflowEditableFieldKeys.Owner),
                new(WorkflowEditableFieldKeys.RequestingOwner),
                new(WorkflowEditableFieldKeys.Reason)
            ],
            [WfTaskType.group_create] =
            [
                new(WorkflowEditableFieldKeys.Title),
                new(WorkflowEditableFieldKeys.Management),
                new(WorkflowEditableFieldKeys.Name),
                new(WorkflowEditableFieldKeys.GroupMembers),
                new(WorkflowEditableFieldKeys.Reason)
            ],
            [WfTaskType.group_modify] =
            [
                new(WorkflowEditableFieldKeys.Title),
                new(WorkflowEditableFieldKeys.Management),
                new(WorkflowEditableFieldKeys.Name),
                new(WorkflowEditableFieldKeys.GroupMembers),
                new(WorkflowEditableFieldKeys.Reason)
            ],
            // the object itself, its name included, is part of the element and stays with the requester
            [WfTaskType.object_create] =
            [
                new(WorkflowEditableFieldKeys.Title),
                new(WorkflowEditableFieldKeys.Management),
                new(WorkflowEditableFieldKeys.Name),
                new(WorkflowEditableFieldKeys.Content),
                new(WorkflowEditableFieldKeys.Reason)
            ],
            // no management either: the task references an object of exactly that management
            [WfTaskType.object_modify] =
            [
                new(WorkflowEditableFieldKeys.Title),
                new(WorkflowEditableFieldKeys.Name),
                new(WorkflowEditableFieldKeys.Content),
                new(WorkflowEditableFieldKeys.Reason)
            ]
        };
    }
}
