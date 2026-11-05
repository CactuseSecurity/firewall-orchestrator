using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Request;

namespace FWO.Ui.Services
{
    /// <summary>Callbacks used by the approval task body components.</summary>
    public sealed class ApprovalTaskViewActions
    {
        /// <summary>Determines whether a task field can be edited.</summary>
        public Func<IEnumerable<WfReqTask>, string, bool> CanEdit { get; init; } = (_, _) => false;
        /// <summary>Determines whether a time object can be edited.</summary>
        public Func<IEnumerable<WfReqTask>, bool> CanEditTimeObject { get; init; } = _ => false;
        /// <summary>Returns the editable time-object field.</summary>
        public Func<IEnumerable<WfReqTask>, string> EditableTimeObjectField { get; init; } = _ => "";
        /// <summary>Opens the owner editor.</summary>
        public Func<WfReqTask, Task> EditOwner { get; init; } = _ => Task.CompletedTask;
        /// <summary>Opens the rule-action editor.</summary>
        public Func<WfReqTask, Task> EditRuleAction { get; init; } = _ => Task.CompletedTask;
        /// <summary>Opens the tracking editor.</summary>
        public Func<WfReqTask, Task> EditTracking { get; init; } = _ => Task.CompletedTask;
        /// <summary>Opens the management editor.</summary>
        public Func<WfReqTask, Task> EditManagement { get; init; } = _ => Task.CompletedTask;
        /// <summary>Opens an element editor.</summary>
        public Func<WfReqTask, string, Task> EditField { get; init; } = (_, _) => Task.CompletedTask;
        /// <summary>Opens the group-member editor.</summary>
        public Func<WfReqTask, Task> EditGroupMembers { get; init; } = _ => Task.CompletedTask;
        /// <summary>Opens the object-task editor.</summary>
        public Func<WfReqTask, Task> EditObjectTask { get; init; } = _ => Task.CompletedTask;
        /// <summary>Opens a text-field editor.</summary>
        public Func<WfReqTask, string, string, Task> EditText { get; init; } = (_, _, _) => Task.CompletedTask;
        /// <summary>Opens the target-date editor.</summary>
        public Func<WfReqTask, string, Task> EditTargetDates { get; init; } = (_, _) => Task.CompletedTask;
    }

    /// <summary>Provides display data and callbacks to approval task body components.</summary>
    public sealed class ApprovalTaskViewContext
    {
        /// <summary>Presentation logic for resolving task values.</summary>
        public ApprovalTaskDisplayLogic DisplayLogic { get; }
        /// <summary>Fields enabled for display in the current view.</summary>
        public AccessTaskFieldVisibilityConfig Visibility { get; }
        /// <summary>Protocols available for technical element display.</summary>
        public List<IpProtocol> DisplayIpProtos { get; }
        /// <summary>Determines whether a field can be edited.</summary>
        public Func<IEnumerable<WfReqTask>, string, bool> CanEdit { get; }
        /// <summary>Determines whether the time object can be edited.</summary>
        public Func<IEnumerable<WfReqTask>, bool> CanEditTimeObject { get; }
        /// <summary>Returns the editable time-object field name.</summary>
        public Func<IEnumerable<WfReqTask>, string> EditableTimeObjectField { get; }
        /// <summary>Opens the owner editor.</summary>
        public Func<WfReqTask, Task> EditOwner { get; }
        /// <summary>Opens the rule-action editor.</summary>
        public Func<WfReqTask, Task> EditRuleAction { get; }
        /// <summary>Opens the tracking editor.</summary>
        public Func<WfReqTask, Task> EditTracking { get; }
        /// <summary>Opens the management editor.</summary>
        public Func<WfReqTask, Task> EditManagement { get; }
        /// <summary>Opens an element editor.</summary>
        public Func<WfReqTask, string, Task> EditField { get; }
        /// <summary>Opens the group-member editor.</summary>
        public Func<WfReqTask, Task> EditGroupMembers { get; }
        /// <summary>Opens the object-task editor.</summary>
        public Func<WfReqTask, Task> EditObjectTask { get; }
        /// <summary>Opens a text-field editor.</summary>
        public Func<WfReqTask, string, string, Task> EditText { get; }
        /// <summary>Opens the target-date editor.</summary>
        public Func<WfReqTask, string, Task> EditTargetDates { get; }

        /// <summary>Initializes the approval task view context.</summary>
        public ApprovalTaskViewContext(
            ApprovalTaskDisplayLogic displayLogic,
            AccessTaskFieldVisibilityConfig visibility,
            List<IpProtocol> displayIpProtos,
            ApprovalTaskViewActions actions)
        {
            DisplayLogic = displayLogic;
            Visibility = visibility;
            DisplayIpProtos = displayIpProtos;
            CanEdit = actions.CanEdit;
            CanEditTimeObject = actions.CanEditTimeObject;
            EditableTimeObjectField = actions.EditableTimeObjectField;
            EditOwner = actions.EditOwner;
            EditRuleAction = actions.EditRuleAction;
            EditTracking = actions.EditTracking;
            EditManagement = actions.EditManagement;
            EditField = actions.EditField;
            EditGroupMembers = actions.EditGroupMembers;
            EditObjectTask = actions.EditObjectTask;
            EditText = actions.EditText;
            EditTargetDates = actions.EditTargetDates;
        }
    }
}
