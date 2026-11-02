using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Request;

namespace FWO.Ui.Services
{
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
            Func<IEnumerable<WfReqTask>, string, bool> canEdit,
            Func<IEnumerable<WfReqTask>, bool> canEditTimeObject,
            Func<IEnumerable<WfReqTask>, string> editableTimeObjectField,
            Func<WfReqTask, Task> editOwner,
            Func<WfReqTask, Task> editRuleAction,
            Func<WfReqTask, Task> editTracking,
            Func<WfReqTask, Task> editManagement,
            Func<WfReqTask, string, Task> editField,
            Func<WfReqTask, Task> editGroupMembers,
            Func<WfReqTask, Task> editObjectTask,
            Func<WfReqTask, string, string, Task> editText,
            Func<WfReqTask, string, Task> editTargetDates)
        {
            DisplayLogic = displayLogic;
            Visibility = visibility;
            DisplayIpProtos = displayIpProtos;
            CanEdit = canEdit;
            CanEditTimeObject = canEditTimeObject;
            EditableTimeObjectField = editableTimeObjectField;
            EditOwner = editOwner;
            EditRuleAction = editRuleAction;
            EditTracking = editTracking;
            EditManagement = editManagement;
            EditField = editField;
            EditGroupMembers = editGroupMembers;
            EditObjectTask = editObjectTask;
            EditText = editText;
            EditTargetDates = editTargetDates;
        }
    }
}
