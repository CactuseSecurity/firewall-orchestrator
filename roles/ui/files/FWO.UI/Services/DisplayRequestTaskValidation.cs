using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Services;

namespace FWO.Ui.Pages.Request;

/// <summary>Validation and approval-editability logic for <see cref="DisplayRequestTask"/>.</summary>
public partial class DisplayRequestTask
{
    private bool RejectInvalidTaskFormat()
    {
        if (WfHandler.ActReqTask.Title == null || WfHandler.ActReqTask.Title == ""
            || (actTaskType == WfTaskType.new_interface && (actOwner == null || !IsRequestingOwnerAllowed())))
        {
            DisplayMessageInUi(null, userConfig.GetText("save_task"), userConfig.GetText("E5102"), true);
            return false;
        }

        return true;
    }

    private bool RejectInvalidTargetDates()
    {
        if (!targetDatesValid
            || (WfHandler.ActReqTask.TargetBeginDate.HasValue && WfHandler.ActReqTask.TargetEndDate.HasValue
                && WfHandler.ActReqTask.TargetBeginDate.Value > WfHandler.ActReqTask.TargetEndDate.Value))
        {
            DisplayMessageInUi(null, userConfig.GetText("save_task"), userConfig.GetText("E5119"), true);
            return false;
        }

        return true;
    }

    private bool IsRequestingOwnerAllowed()
    {
        return actRequestingOwner == null || RequestingOwnerOptions.Any(owner => owner.Id == actRequestingOwner.Id);
    }

    private bool RejectInvalidAccessTask()
    {
        if (elementEditor is null || elementEditor.SourceCount == 0
            || elementEditor.DestinationCount == 0 || elementEditor.ServiceCount == 0
            || (metadataEditor?.NeedsManualDeviceSelection == true && !selectedDevices.Any()))
        {
            DisplayMessageInUi(null, userConfig.GetText("save_task"), userConfig.GetText("E5102"), true);
            return false;
        }
        foreach (WfReqElement reqElem in WfHandler.ActReqTask.Elements)
        {
            if (!HasObjectReference(reqElem) && IsInvalidServicePort(reqElem))
            {
                DisplayMessageInUi(null, userConfig.GetText("save_element"), userConfig.GetText("E5103"), true);
                return false;
            }
        }
        return true;
    }

    private static bool HasObjectReference(WfReqElement element)
    {
        return HasId(element.NetworkId) || HasId(element.ServiceId) || HasId(element.FlowNetworkObjectId)
            || HasId(element.FlowNetworkGroupId) || HasId(element.FlowServiceObjectId)
            || HasId(element.FlowServiceGroupId);
    }

    private static bool HasId(long? id)
    {
        return id.HasValue && id.Value != 0;
    }

    private static bool IsInvalidServicePort(WfReqElement element)
    {
        if (element.Field != ElemFieldType.service.ToString() || !IpProtocol.HasPorts(element.ProtoId))
        {
            return false;
        }

        return element.Port < 1 || element.Port > GlobalConst.kMaxPortNumber;
    }

    private async Task<bool> RejectInvalidRuleDeleteTask()
    {
        if (actRuleDevice == null || elementEditor is null || elementEditor.RuleCount == 0)
        {
            DisplayMessageInUi(null, userConfig.GetText("save_task"), userConfig.GetText("E5102"), true);
            return false;
        }
        foreach (WfReqElement elem in WfHandler.ActReqTask.Elements.Where(x => x.Field == ElemFieldType.rule.ToString()))
        {
            if (!await WfHandler.CheckRuleUid(actRuleDevice.Id, elem.RuleUid))
            {
                DisplayMessageInUi(null, userConfig.GetText("save_task"), userConfig.GetText("E8013") + elem.RuleUid, true);
                return false;
            }
        }
        return true;
    }

    private bool RejectInvalidGroupCreateTask()
    {
        if (string.IsNullOrEmpty(actGrpName) || elementEditor is null
            || (elementEditor.SourceCount == 0 && elementEditor.ServiceCount == 0))
        {
            DisplayMessageInUi(null, userConfig.GetText("save_task"), userConfig.GetText("E5102"), true);
            return false;
        }

        return true;
    }

    private bool RejectInvalidGroupModifyTask()
    {
        string? groupName = elementEditor?.CurrentGroupName;
        if (string.IsNullOrWhiteSpace(groupName))
        {
            groupName = actGrpName;
        }

        if (string.IsNullOrWhiteSpace(groupName) || elementEditor is null
            || (elementEditor.SourceCount == 0 && elementEditor.ServiceCount == 0))
        {
            DisplayMessageInUi(null, userConfig.GetText("save_task"), userConfig.GetText("E5102"), true);
            return false;
        }

        return true;
    }

    private async Task<bool> RejectInvalidObjectTask()
    {
        string? error;
        if (!WfHandler.EditReqTaskMode && !WfHandler.ApproveReqTaskMode && !IsClassicApprovalEditMode)
        {
            if (WfObjectTaskHelper.HasValidElementStructure(WfHandler.ActReqTask))
            {
                error = null;
            }
            else
            {
                error = "E5124";
            }
        }
        else if (elementEditor == null)
        {
            error = "E5124";
        }
        else
        {
            error = elementEditor.ApplyObjectTask();
        }
        if (error != null)
        {
            DisplayMessageInUi(null, userConfig.GetText("save_task"), userConfig.GetText(error), true);
            return false;
        }
        if (await WfHandler.HasObjectTargets(WfHandler.ActReqTask) == false)
        {
            DisplayMessageInUi(null, userConfig.GetText("save_task"), userConfig.GetText("E5129"), true);
            return false;
        }
        return true;
    }

    private async Task<bool> CheckTaskValues()
    {
        if (!RejectInvalidTaskFormat())
        {
            return false;
        }
        switch (actTaskType)
        {
            case WfTaskType.access when !RejectInvalidAccessTask():
            case WfTaskType.rule_delete when !(await RejectInvalidRuleDeleteTask()):
            case WfTaskType.group_create when !RejectInvalidGroupCreateTask():
            case WfTaskType.group_modify when !RejectInvalidGroupModifyTask():
            case WfTaskType.object_create or WfTaskType.object_modify when !(await RejectInvalidObjectTask()):
                return false;
            default:
                return true;
        }
    }

    private bool CanEditReqTaskField(string fieldKey)
    {
        if (WfHandler.AddReqTaskMode)
        {
            return !WfHandler.ReadOnlyMode;
        }

        if (WfObjectTaskHelper.IsObjectTask(actTaskType) && WfObjectTaskHelper.HasFlowDbReference(WfHandler.ActReqTask)
            && (fieldKey == WorkflowEditableFieldKeys.Name || fieldKey == WorkflowEditableFieldKeys.Content))
        {
            return false;
        }

        if (WfHandler.ReadOnlyMode || (!IsClassicApprovalEditMode && FocusField != null && FocusField != fieldKey))
        {
            return false;
        }

        bool approvalEditMode = WfHandler.ApproveReqTaskMode || IsClassicApprovalEditMode;
        if (!WfHandler.EditReqTaskMode && !approvalEditMode)
        {
            return false;
        }

        return ApproverAllowedChangesAccess.CanEditTaskField(GetApproverAllowedChanges(), GetTaskFieldEditContext(), fieldKey);
    }

    private bool CanSaveReqTaskChanges()
    {
        return ApproverAllowedChangesAccess.CanSaveTaskChanges(GetApproverAllowedChanges(), GetTaskFieldEditContext());
    }

    private void SetTargetDatesValid(bool datesValid)
    {
        targetDatesValid = datesValid;
    }

    private ApproverAllowedChangesConfig GetApproverAllowedChanges()
    {
        return ApproverAllowedChangesConfig.Parse(userConfig.ReqAllowedChangesByApprover);
    }

    private AccessTaskFieldVisibilityConfig AccessTaskVisibility => TicketFieldVisibilityConfig.Parse(userConfig.ReqTicketFieldVisibility).AccessTask;

    private bool ShowTaskField(string fieldKey)
    {
        if (actTaskType != WfTaskType.access)
        {
            return true;
        }

        return fieldKey switch
        {
            WorkflowEditableFieldKeys.Management => AccessTaskVisibility.ShowManagement,
            WorkflowEditableFieldKeys.Owner => AccessTaskVisibility.ShowOwner,
            WorkflowEditableFieldKeys.ValidFrom => AccessTaskVisibility.ShowValidFrom,
            WorkflowEditableFieldKeys.ValidTo => AccessTaskVisibility.ShowValidTo,
            _ => true
        };
    }

    private ApproverAllowedChangesAccess.TaskFieldEditContext GetTaskFieldEditContext()
    {
        return new()
        {
            IsApprovalPhase = Phase == WorkflowPhases.approval,
            EditReqTaskMode = WfHandler.EditReqTaskMode,
            ApproveReqTaskMode = WfHandler.ApproveReqTaskMode || IsClassicApprovalEditMode,
            ReadOnlyMode = WfHandler.ReadOnlyMode,
            TaskType = actTaskType,
            HasImplementationTasks = WfHandler.ActReqTask.ImplementationTasks.Count > 0
        };
    }

    private bool IsClassicApprovalEditMode => Phase == WorkflowPhases.approval
        && userConfig.ReqApprovalEditMode == ApprovalEditMode.Classic
        && WfHandler.EditTicketMode
        && !WfHandler.ReadOnlyMode;
}
