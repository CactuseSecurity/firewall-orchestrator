using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Shared;

namespace FWO.Ui.Services;

/// <summary>
/// Handles target-date and ticket-local time-object editing.
/// </summary>
internal sealed class ApprovalTaskDetailsTimeObjectEditor
{
    private readonly ApprovalTaskDetailsEditorContext context;
    private readonly ApprovalTaskDetailsEditorState state;
    private readonly ApprovalTaskDetailsLookupService lookup;
    private readonly Func<IEnumerable<WfReqTask>, string, bool> canEdit;
    private readonly Func<Task> refreshAfterEditorOpen;

    internal ApprovalTaskDetailsTimeObjectEditor(
        ApprovalTaskDetailsEditorContext context,
        ApprovalTaskDetailsEditorState state,
        ApprovalTaskDetailsLookupService lookup,
        Func<IEnumerable<WfReqTask>, string, bool> canEdit,
        Func<Task> refreshAfterEditorOpen)
    {
        this.context = context;
        this.state = state;
        this.lookup = lookup;
        this.canEdit = canEdit;
        this.refreshAfterEditorOpen = refreshAfterEditorOpen;
    }

    private AccessTaskFieldVisibilityConfig AccessTaskVisibility =>
        TicketFieldVisibilityConfig.Parse(context.UserConfig.ReqTicketFieldVisibility).AccessTask;

    internal Task OpenTargetDatesEditor(WfReqTask task, string fieldKey)
    {
        state.DisplayElementsEditor = false;
        state.EditingElementsTask = null;
        state.EditingTargetDatesTask = task;
        WfReqElement? timeElement = TimeObjectElementHelper.Find(task.Elements);
        long? timeObjectId = timeElement == null
            ? null
            : TimeObjectElementHelper.GetSelectorId(timeElement);
        long? ticketElementId = null;
        WfReqElement? parallelTimeElement = TimeObjectReferenceResolver.FindParallelTimeElement(
            context.WfHandler.ActTicket.Tasks, task, timeObjectId, ticketElementId);
        string? timeObjectName = timeElement?.Name;
        if (parallelTimeElement != null)
        {
            timeObjectName ??= parallelTimeElement.Name;
            timeObjectId = TimeObjectReferenceResolver.GetSelectorId(parallelTimeElement);
            ticketElementId = parallelTimeElement.Id;
        }
        long? referencedObjectTaskId = timeElement?.ObjectTaskId;
        state.FormerTimeObjectTask = FindFormerTimeObjectTask(referencedObjectTaskId, parallelTimeElement);
        state.FormerTimeObjectSelectorId = timeObjectId;
        state.FormerTimeObjectElementId = parallelTimeElement?.Id;
        state.TargetDatesEditorModel = new TargetDatesEditorModel
        {
            BeginDate = parallelTimeElement?.StartTime ?? task.TargetBeginDate,
            EndDate = parallelTimeElement?.EndTime ?? task.TargetEndDate,
            FlowTimeObjectId = timeObjectId,
            TicketElementId = parallelTimeElement?.Id ?? ticketElementId,
            FlowTimeObjectName = timeObjectName,
            TicketTimeObjects = lookup.GetTicketTimeObjects(),
            TicketTimeObjectElementIds = lookup.GetTicketTimeObjectElementIds(),
            ShowBeginDate = AccessTaskVisibility.ShowValidFrom,
            ShowEndDate = AccessTaskVisibility.ShowValidTo
        };
        state.DisplayTargetDatesEditor = true;
        return refreshAfterEditorOpen();
    }

    private WfReqTask? FindFormerTimeObjectTask(long? referencedObjectTaskId, WfReqElement? parallelTimeElement)
    {
        if (referencedObjectTaskId is > 0)
        {
            return context.WfHandler.ActTicket.Tasks.FirstOrDefault(candidate => candidate.Id == referencedObjectTaskId.Value);
        }

        if (parallelTimeElement == null)
        {
            return null;
        }

        return context.WfHandler.ActTicket.Tasks.FirstOrDefault(candidate => candidate.Elements.Any(element =>
            element.Id == parallelTimeElement.Id));
    }

    internal bool CanCreateTimeObjectTask => state.EditingTargetDatesTask != null
        && (canEdit([state.EditingTargetDatesTask], WorkflowEditableFieldKeys.ValidFrom)
            || canEdit([state.EditingTargetDatesTask], WorkflowEditableFieldKeys.ValidTo));

    internal Task OpenCreateTimeObjectTaskEditor()
    {
        if (!CanCreateTimeObjectTask)
        {
            return Task.CompletedTask;
        }

        state.NewTimeObjectTaskModel = new TargetDatesEditorModel
        {
            BeginDate = AccessTaskVisibility.ShowValidFrom ? state.TargetDatesEditorModel.BeginDate : null,
            EndDate = AccessTaskVisibility.ShowValidTo ? state.TargetDatesEditorModel.EndDate : null,
            ShowBeginDate = AccessTaskVisibility.ShowValidFrom,
            ShowEndDate = AccessTaskVisibility.ShowValidTo
        };
        state.DisplayTimeObjectTaskEditor = true;
        return refreshAfterEditorOpen();
    }

    internal async Task SaveTargetDatesCore(TargetDatesEditorModel model)
    {
        if (state.EditingTargetDatesTask == null)
        {
            return;
        }

        foreach (WfReqTask task in GetTasksToUpdate(state.EditingTargetDatesTask))
        {
            context.WfHandler.SetReqTaskEnv(task);
            ApplyTimeObjectModel(model);
            await context.WfHandler.ChangeReqTask(allowLockedApprovalUpdate: true);
        }
        bool offerFormerTimeObjectDeletion = ShouldOfferFormerTimeObjectDeletion(model);
        state.EditingTargetDatesTask = null;
        if (offerFormerTimeObjectDeletion)
        {
            state.PendingFormerTimeObjectTask = state.FormerTimeObjectTask;
            state.DisplayDeleteFormerTimeObjectTask = true;
        }
        state.FormerTimeObjectTask = null;
        state.FormerTimeObjectSelectorId = null;
        state.FormerTimeObjectElementId = null;
        await context.RefreshUi();
    }

    internal async Task CreateTimeObjectTaskCore(TargetDatesEditorModel model)
    {
        if (state.EditingTargetDatesTask == null || !CanCreateTimeObjectTask)
        {
            return;
        }

        WfReqTask sourceTask = state.EditingTargetDatesTask;
        string timeObjectName = TimeObjectNamingHelper.BuildDisplayName(
            model.FlowTimeObjectName?.Trim(),
            TimeObjectOrigin.Approver,
            TimeObjectNamingHelper.ParseSuffixes(context.UserConfig.ReqTimeObjectOriginSuffixes));
        WfReqElement timeElement = new()
        {
            Field = ElemFieldType.time.ToString(),
            RequestAction = RequestAction.create.ToString(),
            Name = timeObjectName,
            StartTime = model.BeginDate,
            EndTime = model.EndDate
        };
        WfReqTask newTask = new()
        {
            TicketId = context.WfHandler.ActTicket.Id,
            TaskNumber = context.WfHandler.ActTicket.HighestTaskNumber() + 1,
            TaskType = WfTaskType.object_create.ToString(),
            RequestAction = RequestAction.create.ToString(),
            StateId = sourceTask.StateId,
            Title = timeObjectName,
            ManagementId = sourceTask.ManagementId,
            TargetBeginDate = model.BeginDate,
            TargetEndDate = model.EndDate,
            Locked = true,
            Elements = [timeElement],
            Approvals =
            [
                new WfApproval
                {
                    StateId = sourceTask.StateId,
                    TenantId = context.WfHandler.ActTicket.TenantId,
                    ApproverGroup = sourceTask.Approvals.FirstOrDefault()?.ApproverGroup ?? "",
                    InitialApproval = true
                }
            ]
        };

        InitializeNewObjectTaskState(newTask);
        context.WfHandler.SelectReqTask(newTask, ObjAction.add);
        await context.WfHandler.AddReqTask(allowLockedApprovalUpdate: true);
        WfReqTask? createdTask = context.WfHandler.ActTicket.Tasks.FirstOrDefault(task => task.TaskNumber == newTask.TaskNumber);
        WfReqElement? createdElement = createdTask == null ? null : TimeObjectElementHelper.Find(createdTask.Elements);
        if (createdTask == null || createdElement == null)
        {
            context.WfHandler.SetReqTaskEnv(sourceTask);
            context.WfHandler.ResetReqTaskActions();
            return;
        }

        TargetDatesEditorModel updatedModel = new(state.TargetDatesEditorModel)
        {
            BeginDate = createdElement.StartTime,
            EndDate = createdElement.EndTime,
            FlowTimeObjectId = TimeObjectReferenceResolver.GetSelectorId(createdElement),
            TicketElementId = createdElement.Id,
            FlowTimeObjectName = createdElement.Name,
            TicketTimeObjects = lookup.GetTicketTimeObjects(),
            TicketTimeObjectElementIds = lookup.GetTicketTimeObjectElementIds()
        };
        state.TargetDatesEditorModel = updatedModel;
        context.WfHandler.SetReqTaskEnv(sourceTask);
        context.WfHandler.ResetReqTaskActions();
        state.DisplayTimeObjectTaskEditor = false;
        state.DisplayTargetDatesEditor = true;
        await context.RefreshUi();
    }

    internal void InitializeNewObjectTaskState(WfReqTask task)
    {
        int initialState = context.WfHandler.ActStateMatrix.LowestInputState;
        task.StateId = initialState;
        foreach (WfApproval approval in task.Approvals)
        {
            approval.StateId = initialState;
        }
    }

    internal bool ShouldOfferFormerTimeObjectDeletion(TargetDatesEditorModel model)
    {
        if (state.FormerTimeObjectTask?.Id is not > 0 || state.FormerTimeObjectSelectorId == model.FlowTimeObjectId
            || model.FlowTimeObjectId == null)
        {
            return false;
        }

        WfReqTask? accessTask = state.EditingTargetDatesTask;
        if (accessTask == null)
        {
            return false;
        }

        return !context.WfHandler.ActTicket.Tasks
            .Where(task => task.TaskType == WfTaskType.access.ToString()
                && task.Id != state.FormerTimeObjectTask.Id && task.Id != accessTask.Id)
            .Any(UsesFormerTimeObject);
    }

    internal bool UsesFormerTimeObject(WfReqTask task)
    {
        WfReqElement? timeElement = TimeObjectElementHelper.Find(task.Elements);
        if (timeElement == null || state.FormerTimeObjectTask == null)
        {
            return false;
        }

        return timeElement.ObjectTaskId == state.FormerTimeObjectTask.Id
            || (state.FormerTimeObjectElementId is > 0 && timeElement.TimeObjId == -state.FormerTimeObjectElementId.Value)
            || TimeObjectReferenceResolver.FindParallelTimeTask(
                context.WfHandler.ActTicket.Tasks, task, null, null)?.Id == state.FormerTimeObjectTask.Id;
    }

    internal async Task DeleteFormerTimeObjectTaskCore()
    {
        if (state.PendingFormerTimeObjectTask == null)
        {
            return;
        }

        context.WfHandler.SetReqTaskEnv(state.PendingFormerTimeObjectTask);
        await context.WfHandler.ConfDeleteReqTask(allowLockedApprovalUpdate: true);
        state.PendingFormerTimeObjectTask = null;
        await context.RefreshUi();
    }

    internal IEnumerable<WfReqTask> GetTasksToUpdate(WfReqTask task)
    {
        return ApprovalTaskBundleHelper.GetTasksToUpdate(context.WfHandler.ActTicket.Tasks, task, context.UserConfig);
    }

    internal void ApplyTimeObjectModel(TargetDatesEditorModel model)
    {
        context.WfHandler.ActReqTask.TargetBeginDate = model.BeginDate;
        context.WfHandler.ActReqTask.TargetEndDate = model.EndDate;
        if (!model.FlowTimeObjectId.HasValue || model.FlowTimeObjectId.Value == 0)
        {
            WfReqElement? existingTimeElement = TimeObjectElementHelper.Find(context.WfHandler.ActReqTask.Elements);
            if (existingTimeElement != null)
            {
                context.WfHandler.ActReqTask.Elements.Remove(existingTimeElement);
                if (existingTimeElement.Id > 0)
                    context.WfHandler.ActReqTask.RemovedElements.Add(existingTimeElement);
            }
            return;
        }

        WfReqElement timeElement = TimeObjectElementHelper.GetOrCreate(context.WfHandler.ActReqTask);
        if (model.FlowTimeObjectId.Value > 0)
        {
            TimeObjectElementHelper.SetReference(timeElement, model.FlowTimeObjectId.Value);
        }
        else
        {
            WfReqTask? ticketTimeObjectTask = model.TicketElementId is > 0
                ? context.WfHandler.ActTicket.Tasks.FirstOrDefault(task => task.Elements.Any(element =>
                    element.Id == model.TicketElementId.Value
                    && element.Field == ElemFieldType.time.ToString()))
                : null;
            ticketTimeObjectTask ??= TimeObjectReferenceResolver.FindParallelTimeTask(
                context.WfHandler.ActTicket.Tasks, context.WfHandler.ActReqTask, model.FlowTimeObjectId, model.TicketElementId);
            if (ticketTimeObjectTask != null)
                TimeObjectElementHelper.SetTicketTaskReference(timeElement, ticketTimeObjectTask.Id);
            else
                TimeObjectElementHelper.ClearReference(timeElement);
        }
        timeElement.Name = model.FlowTimeObjectName;
        timeElement.StartTime = model.BeginDate;
        timeElement.EndTime = model.EndDate;
    }

    internal bool CanEditTimeObject(IEnumerable<WfReqTask> tasks) =>
        (AccessTaskVisibility.ShowValidFrom && canEdit(tasks, WorkflowEditableFieldKeys.ValidFrom))
        || (AccessTaskVisibility.ShowValidTo && canEdit(tasks, WorkflowEditableFieldKeys.ValidTo));

    internal string EditableTimeObjectField(IEnumerable<WfReqTask> tasks) =>
        AccessTaskVisibility.ShowValidFrom && canEdit(tasks, WorkflowEditableFieldKeys.ValidFrom)
            ? WorkflowEditableFieldKeys.ValidFrom
            : WorkflowEditableFieldKeys.ValidTo;
}
