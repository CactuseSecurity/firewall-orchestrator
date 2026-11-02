using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Ui.Services;

/// <summary>
/// Handles approval editing of task metadata that is not an access-element collection.
/// </summary>
internal sealed class ApprovalTaskDetailsMetadataEditor
{
    private readonly ApprovalTaskDetailsEditorContext context;
    private readonly ApprovalTaskDetailsEditorState state;
    private readonly Func<Task> refreshAfterEditorOpen;

    internal ApprovalTaskDetailsMetadataEditor(
        ApprovalTaskDetailsEditorContext context,
        ApprovalTaskDetailsEditorState state,
        Func<Task> refreshAfterEditorOpen)
    {
        this.context = context;
        this.state = state;
        this.refreshAfterEditorOpen = refreshAfterEditorOpen;
    }

    internal List<FwoOwner> OwnerOptions => context.WfHandler.AllOwners.Where(owner => owner.Id != 0).ToList();

    internal List<Management> ManagementOptions => context.WfHandler.Managements;

    internal Task OpenOwnerEditor(WfReqTask task)
    {
        state.EditingOwnerTask = task;
        FwoOwner? owner = task.Owners.FirstOrDefault()?.Owner;
        state.SelectedOwner = OwnerOptions.FirstOrDefault(candidate => candidate.Id == owner?.Id) ?? new FwoOwner();
        state.DisplayOwnerEditor = true;
        return refreshAfterEditorOpen();
    }

    internal Task OpenRuleActionEditor(WfReqTask task)
    {
        state.EditingRuleActionTask = task;
        state.SelectedRuleAction = state.RuleActions.FirstOrDefault(action => action.Id == task.RuleAction);
        state.DisplayRuleActionEditor = true;
        return refreshAfterEditorOpen();
    }

    internal Task OpenTrackingEditor(WfReqTask task)
    {
        state.EditingTrackingTask = task;
        state.SelectedTracking = state.Trackings.FirstOrDefault(tracking => tracking.Id == task.Tracking);
        state.DisplayTrackingEditor = true;
        return refreshAfterEditorOpen();
    }

    internal Task OpenManagementEditor(WfReqTask task)
    {
        state.EditingManagementTask = task;
        state.SelectedManagement = ManagementOptions.FirstOrDefault(management => management.Id == task.ManagementId);
        state.DisplayManagementEditor = true;
        return refreshAfterEditorOpen();
    }

    internal Task OpenTextEditor(WfReqTask task, string fieldKey, string value)
    {
        state.EditingTextTask = task;
        state.TextEditorKey = fieldKey;
        state.TextEditorValue = value;
        state.TextEditorMultiline = fieldKey != WorkflowEditableFieldKeys.Title;
        state.DisplayTextEditor = true;
        return refreshAfterEditorOpen();
    }

    internal async Task SaveRuleActionCore(RuleAction? ruleAction)
    {
        if (state.EditingRuleActionTask == null)
        {
            return;
        }

        context.WfHandler.SetReqTaskEnv(state.EditingRuleActionTask);
        context.WfHandler.ActReqTask.RuleAction = ruleAction?.Id;
        await context.WfHandler.ChangeReqTask(allowLockedApprovalUpdate: true);
        state.EditingRuleActionTask = null;
        await context.RefreshUi();
    }

    internal async Task SaveOwnerCore(FwoOwner owner)
    {
        if (state.EditingOwnerTask == null)
        {
            return;
        }

        context.WfHandler.SetReqTaskEnv(state.EditingOwnerTask);
        FwoOwner? oldOwner = state.EditingOwnerTask.Owners.FirstOrDefault()?.Owner;
        context.WfHandler.ActReqTask.RemovedOwners = oldOwner == null || oldOwner.Id == owner.Id ? [] : [oldOwner];
        context.WfHandler.ActReqTask.NewOwners = oldOwner != null && oldOwner.Id == owner.Id ? [] : [owner];
        context.WfHandler.ActReqTask.Owners = oldOwner == null ? [] : [new FwoOwnerDataHelper { Owner = oldOwner }];
        await context.WfHandler.ChangeReqTask(allowLockedApprovalUpdate: true);
        state.EditingOwnerTask = null;
        await context.RefreshUi();
    }

    internal async Task SaveTrackingCore(Tracking? tracking)
    {
        if (state.EditingTrackingTask == null)
        {
            return;
        }

        context.WfHandler.SetReqTaskEnv(state.EditingTrackingTask);
        context.WfHandler.ActReqTask.Tracking = tracking?.Id;
        await context.WfHandler.ChangeReqTask(allowLockedApprovalUpdate: true);
        state.EditingTrackingTask = null;
        await context.RefreshUi();
    }

    internal async Task SaveManagementCore(Management? management)
    {
        if (state.EditingManagementTask == null)
        {
            return;
        }

        context.WfHandler.SetReqTaskEnv(state.EditingManagementTask);
        context.WfHandler.ActReqTask.ManagementId = management?.Id;
        await context.WfHandler.ChangeReqTask(allowLockedApprovalUpdate: true);
        state.EditingManagementTask = null;
        await context.RefreshUi();
    }

    internal async Task SaveTextCore(string value)
    {
        if (state.EditingTextTask == null)
        {
            return;
        }

        context.WfHandler.SetReqTaskEnv(state.EditingTextTask);
        switch (state.TextEditorKey)
        {
            case WorkflowEditableFieldKeys.Title:
                context.WfHandler.ActReqTask.Title = value;
                break;
            case WorkflowEditableFieldKeys.Reason:
                context.WfHandler.ActReqTask.Reason = value;
                break;
            case WorkflowEditableFieldKeys.FreeText:
                context.WfHandler.ActReqTask.FreeText = value;
                break;
            case WorkflowEditableFieldKeys.Name:
                context.WfHandler.ActReqTask.SetAddInfo(AdditionalInfoKeys.GrpName, value);
                context.WfHandler.ActReqTask.Title = value;
                break;
        }

        await context.WfHandler.ChangeReqTask(allowLockedApprovalUpdate: true);
        state.EditingTextTask = null;
        await context.RefreshUi();
    }
}
