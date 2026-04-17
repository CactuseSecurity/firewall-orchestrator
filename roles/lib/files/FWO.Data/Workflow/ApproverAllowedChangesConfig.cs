using System.Text.Json;

namespace FWO.Data.Workflow
{
    public static class WorkflowEditableFieldKeys
    {
        public const string Title = "title";
        public const string Priority = "priority";
        public const string Deadline = "deadline";
        public const string Reason = "reason";
        public const string FreeText = "free_text";
        public const string Management = "management";
        public const string Gateways = "gateways";
        public const string Gateway = "gateway";
        public const string RuleAction = "rule_action";
        public const string Track = "track";
        public const string ValidFrom = "valid_from";
        public const string ValidTo = "valid_to";
        public const string Source = "source";
        public const string Destination = "destination";
        public const string Services = "services";
        public const string Rules = "rules";
        public const string Owner = "owner";
        public const string RequestingOwner = "requesting_owner";
        public const string Name = "name";
    }

    public class WorkflowEditableFieldDefinition
    {
        public string Key { get; set; } = "";

        public WorkflowEditableFieldDefinition()
        { }

        public WorkflowEditableFieldDefinition(string key)
        {
            Key = key;
        }
    }

    public class ApproverAllowedChangesConfig
    {
        public List<string> TicketFields { get; set; } = [];
        public Dictionary<string, List<string>> TaskTypeFields { get; set; } = [];

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

        public string ToConfigValue()
        {
            return JsonSerializer.Serialize(this);
        }

        public bool IsTicketFieldAllowed(string fieldKey)
        {
            return TicketFields.Contains(fieldKey);
        }

        public bool IsTaskFieldAllowed(WfTaskType taskType, string fieldKey)
        {
            return TaskTypeFields.TryGetValue(taskType.ToString(), out List<string>? fields)
                && fields.Contains(fieldKey);
        }

        public bool HasTicketFieldChanges()
        {
            return TicketFields.Count > 0;
        }

        public bool HasTaskFieldChanges(WfTaskType taskType)
        {
            return TaskTypeFields.TryGetValue(taskType.ToString(), out List<string>? fields)
                && fields.Count > 0;
        }

        public void SetTicketField(string fieldKey, bool allowed)
        {
            SetField(TicketFields, fieldKey, allowed);
        }

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

    public static class ApproverAllowedChangesAccess
    {
        public class TaskFieldEditContext
        {
            public bool IsApprovalPhase { get; set; }
            public bool EditReqTaskMode { get; set; }
            public bool ApproveReqTaskMode { get; set; }
            public bool ReadOnlyMode { get; set; }
            public WfTaskType TaskType { get; set; }
            public bool HasImplementationTasks { get; set; }
        }

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

        public static bool CanEditTicketField(ApproverAllowedChangesConfig config, bool isApprovalPhase, bool editTicketMode, bool readOnlyMode, string fieldKey)
        {
            return editTicketMode || isApprovalPhase && !readOnlyMode && config.IsTicketFieldAllowed(fieldKey);
        }

        public static bool CanSaveTicketChanges(ApproverAllowedChangesConfig config, bool isApprovalPhase, bool editTicketMode, bool readOnlyMode)
        {
            return editTicketMode || isApprovalPhase && !readOnlyMode && config.HasTicketFieldChanges();
        }

        public static bool CanEditTaskField(ApproverAllowedChangesConfig config, TaskFieldEditContext context, string fieldKey)
        {
            return context.EditReqTaskMode || context.IsApprovalPhase && context.ApproveReqTaskMode && !context.ReadOnlyMode
                && config.IsTaskFieldAllowed(context.TaskType, fieldKey)
                && (!context.HasImplementationTasks || !IsCopiedToImplementationTask(context.TaskType, fieldKey));
        }

        public static bool CanSaveTaskChanges(ApproverAllowedChangesConfig config, TaskFieldEditContext context)
        {
            return context.EditReqTaskMode || context.IsApprovalPhase && context.ApproveReqTaskMode && !context.ReadOnlyMode
                && config.HasTaskFieldChanges(context.TaskType);
        }
    }

    public static class ApproverAllowedChangesCatalog
    {
        public static readonly List<WorkflowEditableFieldDefinition> TicketFields =
        [
            new(WorkflowEditableFieldKeys.Title),
            new(WorkflowEditableFieldKeys.Priority),
            new(WorkflowEditableFieldKeys.Deadline),
            new(WorkflowEditableFieldKeys.Reason)
        ];

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
                new(WorkflowEditableFieldKeys.Management),
                new(WorkflowEditableFieldKeys.Gateways),
                new(WorkflowEditableFieldKeys.RuleAction),
                new(WorkflowEditableFieldKeys.Track),
                new(WorkflowEditableFieldKeys.ValidFrom),
                new(WorkflowEditableFieldKeys.ValidTo),
                new(WorkflowEditableFieldKeys.Reason),
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
                new(WorkflowEditableFieldKeys.Reason)
            ]
        };
    }
}
