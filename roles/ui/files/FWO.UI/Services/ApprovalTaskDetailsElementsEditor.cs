using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Ui.Services;

/// <summary>
/// Handles source, destination, service, object-task and group-task changes.
/// </summary>
internal sealed class ApprovalTaskDetailsElementsEditor
{
    private readonly ApprovalTaskDetailsEditorContext context;
    private readonly ApprovalTaskDetailsEditorState state;
    private readonly Func<Task> refreshAfterEditorOpen;

    internal ApprovalTaskDetailsElementsEditor(
        ApprovalTaskDetailsEditorContext context,
        ApprovalTaskDetailsEditorState state,
        Func<Task> refreshAfterEditorOpen)
    {
        this.context = context;
        this.state = state;
        this.refreshAfterEditorOpen = refreshAfterEditorOpen;
    }

    internal Task OpenElementsEditor(WfReqTask task, string fieldKey)
    {
        state.EditingElementsTask = task;
        state.EditingElementsField = fieldKey;
        state.DisplayElementsEditor = true;
        return refreshAfterEditorOpen();
    }

    internal Task OpenGroupMembersEditor(WfReqTask task)
    {
        string fieldKey = task.GetNwObjectElements(ElemFieldType.source).Count > 0
            ? WorkflowEditableFieldKeys.Source
            : WorkflowEditableFieldKeys.Services;
        return OpenElementsEditor(task, fieldKey);
    }

    internal Task OpenExistingObjectTaskEditor(WfReqTask task)
    {
        state.NewObjectTask = task;
        state.ObjectTaskIsExisting = true;
        state.ObjectTaskIsModify = task.TaskType == WfTaskType.object_modify.ToString();
        state.NewObjectTaskKind = WfObjectTaskHelper.GetKind(WfObjectTaskHelper.GetRequestedElement(task)
            ?? WfObjectTaskHelper.GetOriginalElement(task));
        state.DisplayObjectTaskEditor = true;
        return refreshAfterEditorOpen();
    }

    internal Task OpenObjectTaskEditor()
    {
        if (state.EditingElementsTask == null)
        {
            return Task.CompletedTask;
        }

        state.NewObjectTask = new WfReqTask
        {
            TicketId = context.WfHandler.ActTicket.Id,
            TaskNumber = context.WfHandler.ActTicket.HighestTaskNumber() + 1,
            TaskType = WfTaskType.object_create.ToString(),
            RequestAction = RequestAction.create.ToString(),
            StateId = state.EditingElementsTask.StateId,
            ManagementId = state.EditingElementsTask.ManagementId,
            Locked = true,
            Title = context.UserConfig.GetText("task")
        };
        state.NewObjectTaskKind = state.EditingElementsField == WorkflowEditableFieldKeys.Services
            ? ObjectTaskKind.Service
            : ObjectTaskKind.Network;
        state.ObjectTaskIsExisting = false;
        state.ObjectTaskIsModify = false;
        state.DisplayElementsEditor = false;
        state.DisplayObjectTaskEditor = true;
        return refreshAfterEditorOpen();
    }

    internal async Task HandleObjectTaskEditorDisplayChanged(bool display)
    {
        state.DisplayObjectTaskEditor = display;
        if (!display)
        {
            state.ObjectTaskIsExisting = false;
            state.ObjectTaskIsModify = false;
        }
        if (!display && state.EditingElementsTask != null)
        {
            state.DisplayElementsEditor = true;
        }
        await context.RefreshUi();
    }

    internal async Task SaveObjectTaskCore(WfReqTask task)
    {
        if (!state.ObjectTaskIsModify)
        {
            task.Title = task.Elements.FirstOrDefault()?.Name ?? task.Title;
        }
        context.WfHandler.SetReqTaskEnv(task);
        if (state.ObjectTaskIsExisting)
        {
            await context.WfHandler.ChangeReqTask(allowLockedApprovalUpdate: true);
        }
        else
        {
            await context.WfHandler.AddReqTask(allowLockedApprovalUpdate: true);
        }
        await RefreshAccessTasksUsingObject(task);
        state.DisplayObjectTaskEditor = false;
        state.ObjectTaskIsExisting = false;
        state.ObjectTaskIsModify = false;
        state.DisplayElementsEditor = state.EditingElementsTask != null;
        await context.RefreshUi();
    }

    /// <summary>
    /// Refreshes access-task snapshots after an object task was changed.
    /// </summary>
    /// <param name="objectTask">The saved object task.</param>
    internal async Task RefreshAccessTasksUsingObject(WfReqTask objectTask)
    {
        WfReqElement? requested = WfObjectTaskHelper.GetRequestedElement(objectTask);
        if (requested == null || !WfObjectTaskHelper.IsObjectTask(objectTask.TaskType))
        {
            return;
        }

        List<WfReqTask> affectedTasks = context.WfHandler.ActTicket.Tasks
            .Where(task => task.TaskType == WfTaskType.access.ToString())
            .Where(task => task.Elements.Any(element => element.ObjectTaskId == objectTask.Id))
            .ToList();

        foreach (WfReqTask accessTask in affectedTasks)
        {
            bool changed = false;
            foreach (WfReqElement element in accessTask.Elements.Where(element => element.ObjectTaskId == objectTask.Id))
            {
                CopyObjectSnapshot(element, requested);
                changed = true;
            }

            if (changed)
            {
                context.WfHandler.SetReqTaskEnv(accessTask);
                await context.WfHandler.ChangeReqTask(allowLockedApprovalUpdate: true);
            }
        }
    }

    /// <summary>
    /// Copies changed object data while preserving access-element identity and action.
    /// </summary>
    internal static void CopyObjectSnapshot(WfReqElement target, WfReqElement source)
    {
        string field = target.Field;
        long elementId = target.Id;
        long taskId = target.TaskId;
        string requestAction = target.RequestAction;
        long? objectTaskId = target.ObjectTaskId;
        target.IpString = source.IpString ?? source.Cidr?.CidrString;
        target.IpEnd = source.IpEnd ?? source.CidrEnd?.CidrString;
        target.Cidr = source.Cidr;
        target.CidrEnd = source.CidrEnd;
        target.Port = source.Port;
        target.PortEnd = source.PortEnd;
        target.ProtoId = source.ProtoId;
        target.NetworkId = source.NetworkId ?? target.NetworkId;
        target.ServiceId = source.ServiceId ?? target.ServiceId;
        target.FlowNetworkObjectId = source.FlowNetworkObjectId ?? target.FlowNetworkObjectId;
        target.FlowNetworkGroupId = source.FlowNetworkGroupId ?? target.FlowNetworkGroupId;
        target.FlowServiceObjectId = source.FlowServiceObjectId ?? target.FlowServiceObjectId;
        target.FlowServiceGroupId = source.FlowServiceGroupId ?? target.FlowServiceGroupId;
        target.TimeObjId = source.TimeObjId ?? target.TimeObjId;
        target.FlowTimeObjectId = source.FlowTimeObjectId ?? target.FlowTimeObjectId;
        target.GroupName = source.GroupName;
        target.Name = source.Name;
        target.Field = field;
        target.Id = elementId;
        target.TaskId = taskId;
        target.RequestAction = requestAction;
        target.ObjectTaskId = objectTaskId;
    }

    internal static bool SamePositiveId(long? left, long? right) => left is > 0 && left == right;

    internal static WfReqElement CreateDeletedGroupMember(WfReqElement element)
    {
        return new WfReqElement(element)
        {
            RequestAction = RequestAction.delete.ToString()
        };
    }

    internal async Task DeleteObjectTaskCore()
    {
        if (state.PendingObjectTask == null)
        {
            return;
        }

        context.WfHandler.SetReqTaskEnv(state.PendingObjectTask);
        await context.WfHandler.ConfDeleteReqTask(allowLockedApprovalUpdate: true);
        state.PendingObjectTask = null;
        state.DisplayDeleteObjectTask = false;
        await context.RefreshUi();
    }

    internal async Task SaveElementsCore(string fieldKey, IReadOnlyList<NwObjectElement> sources,
        IReadOnlyList<NwObjectElement> destinations, IReadOnlyList<NwServiceElement> services)
    {
        if (state.EditingElementsTask == null)
        {
            return;
        }

        context.WfHandler.SetReqTaskEnv(state.EditingElementsTask);
        WfReqTask activeTask = context.WfHandler.ActReqTask;
        ElemFieldType field = fieldKey switch
        {
            WorkflowEditableFieldKeys.Source => ElemFieldType.source,
            WorkflowEditableFieldKeys.Destination => ElemFieldType.destination,
            _ => ElemFieldType.service
        };
        List<WfReqTask> formerObjectTasks = activeTask.Elements
            .Where(element => element.Field == field.ToString() && element.TaskId > 0)
            .Where(element => element.ObjectTaskId is > 0)
            .Select(element => context.WfHandler.ActTicket.Tasks.FirstOrDefault(task => task.Id == element.ObjectTaskId))
            .Where(task => task != null && WfObjectTaskHelper.IsManagementObjectTask(task.TaskType))
            .Cast<WfReqTask>()
            .DistinctBy(task => task.Id)
            .ToList();
        List<WfReqElement> replacement = field switch
        {
            ElemFieldType.source => sources.Select(element => element.ToReqElement(field)).ToList(),
            ElemFieldType.destination => destinations.Select(element => element.ToReqElement(field)).ToList(),
            _ => services.Select(element => element.ToReqElement()).ToList()
        };
        List<WfReqElement> existingElements = activeTask.Elements
            .Where(element => element.Field == field.ToString())
            .ToList();
        HashSet<long> retainedIds = replacement.Where(element => element.Id > 0).Select(element => element.Id).ToHashSet();
        if (activeTask.TaskType == WfTaskType.group_modify.ToString())
        {
            replacement.AddRange(existingElements
                .Where(element => element.Id > 0 && !retainedIds.Contains(element.Id))
                .Select(CreateDeletedGroupMember));
        }
        else
        {
            activeTask.RemovedElements.AddRange(existingElements
                .Where(element => element.Id > 0 && !retainedIds.Contains(element.Id))
                .Where(element => activeTask.RemovedElements.All(removed => removed.Id != element.Id)));
        }
        activeTask.Elements = activeTask.Elements
            .Where(element => element.Field != field.ToString())
            .Concat(replacement)
            .ToList();
        await context.WfHandler.ChangeReqTask(allowLockedApprovalUpdate: true);
        if (IsGroupTask(activeTask))
        {
            await RefreshAccessTasksUsingGroup(activeTask);
        }
        state.EditingElementsTask = null;
        WfReqTask? formerObjectTask = formerObjectTasks.FirstOrDefault(task => !IsObjectTaskReferenced(task));
        if (formerObjectTask != null)
        {
            state.PendingObjectTask = formerObjectTask;
            state.DisplayDeleteObjectTask = true;
        }
        await context.RefreshUi();
    }

    /// <summary>
    /// Refreshes access snapshots selected from a group task.
    /// </summary>
    internal async Task RefreshAccessTasksUsingGroup(WfReqTask groupTask)
    {
        List<WfReqElement> currentElements = groupTask.Elements
            .Where(element => element.Field == ElemFieldType.source.ToString()
                || element.Field == ElemFieldType.service.ToString())
            .Where(element => element.RequestAction != RequestAction.delete.ToString())
            .ToList();

        List<WfReqTask> affectedTasks = context.WfHandler.ActTicket.Tasks
            .Where(task => task.TaskType == WfTaskType.access.ToString())
            .ToList();
        foreach (WfReqTask accessTask in affectedTasks)
        {
            bool changed = false;
            foreach (WfReqElement accessElement in accessTask.Elements.Where(element =>
                element.ObjectTaskId == groupTask.Id))
            {
                WfReqElement? currentElement = FindGroupMemberByReference(accessElement, currentElements);
                if (currentElement != null)
                {
                    CopyObjectSnapshot(accessElement, currentElement);
                    changed = true;
                }
            }

            if (changed)
            {
                context.WfHandler.SetReqTaskEnv(accessTask);
                await context.WfHandler.ChangeReqTask(allowLockedApprovalUpdate: true);
            }
        }
    }

    internal static WfReqElement? FindGroupMemberByReference(WfReqElement accessElement,
        IEnumerable<WfReqElement> currentElements)
    {
        IEnumerable<WfReqElement> candidates = currentElements.Where(element => element.Field == accessElement.Field);
        if (accessElement.Field == ElemFieldType.service.ToString())
        {
            return candidates.FirstOrDefault(element =>
                SamePositiveId(element.ServiceId, accessElement.ServiceId)
                || SamePositiveId(element.FlowServiceObjectId, accessElement.FlowServiceObjectId)
                || SamePositiveId(element.FlowServiceGroupId, accessElement.FlowServiceGroupId));
        }

        return candidates.FirstOrDefault(element =>
            SamePositiveId(element.NetworkId, accessElement.NetworkId)
            || SamePositiveId(element.FlowNetworkObjectId, accessElement.FlowNetworkObjectId)
            || SamePositiveId(element.FlowNetworkGroupId, accessElement.FlowNetworkGroupId));
    }

    internal bool IsObjectTaskReferenced(WfReqTask objectTask)
    {
        return context.WfHandler.ActTicket.Tasks
            .Where(task => task.Id != objectTask.Id)
            .SelectMany(task => task.Elements)
            .Any(element => element.ObjectTaskId == objectTask.Id);
    }

    private static bool IsGroupTask(WfReqTask task) => task.TaskType == WfTaskType.group_create.ToString()
        || task.TaskType == WfTaskType.group_modify.ToString()
        || task.TaskType == WfTaskType.group_delete.ToString();
}
