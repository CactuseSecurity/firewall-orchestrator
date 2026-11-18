using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Shared;

namespace FWO.Ui.Services;

/// <summary>
/// Coordinates the approval-task editors while keeping their state and domain responsibilities separate.
/// </summary>
internal sealed class ApprovalTaskDetailsEditorService
{
    private readonly ApprovalTaskDetailsEditorState state = new();
    private readonly ApprovalTaskDetailsEditorContext context = new();
    private readonly ApprovalTaskDetailsLookupService lookup;
    private readonly ApprovalTaskDetailsMetadataEditor metadataEditor;
    private readonly ApprovalTaskDetailsElementsEditor elementsEditor;
    private readonly ApprovalTaskDetailsTimeObjectEditor timeObjectEditor;

    internal ApprovalTaskDetailsEditorService()
    {
        lookup = new ApprovalTaskDetailsLookupService(context, state);
        metadataEditor = new ApprovalTaskDetailsMetadataEditor(context, state, RefreshAfterEditorOpen, GetTasksToUpdate);
        elementsEditor = new ApprovalTaskDetailsElementsEditor(context, state, RefreshAfterEditorOpen);
        timeObjectEditor = new ApprovalTaskDetailsTimeObjectEditor(
            context,
            state,
            lookup,
            CanEdit,
            RefreshAfterEditorOpen);
    }

    internal WfHandler WfHandler => context.WfHandler;
    internal UserConfig userConfig => context.UserConfig;

    /// <summary>Disables all request-task editors while retaining their shared details presentation.</summary>
    internal bool ReadOnlyDisplay { get; set; }

    internal bool PlannerOwnerChangesAllowed { get; set; }

    internal void Configure(WfHandler wfHandler, UserConfig config, Func<Task> refresh)
    {
        context.Configure(wfHandler, config, refresh);
    }

    internal ApproverAllowedChangesConfig AllowedChanges =>
        ApproverAllowedChangesConfig.Parse(context.UserConfig.ReqAllowedChangesByApprover);

    internal AccessTaskFieldVisibilityConfig AccessTaskVisibility =>
        TicketFieldVisibilityConfig.Parse(context.UserConfig.ReqTicketFieldVisibility).AccessTask;

    internal ApprovalTaskElementDisplayMode accessElementDisplayMode
    {
        get => state.AccessElementDisplayMode;
        set => state.AccessElementDisplayMode = value;
    }
    internal List<NetworkZone> networkZones
    {
        get => state.NetworkZones;
        set => state.NetworkZones = value;
    }
    internal List<IpProtocol> ipProtocols
    {
        get => state.IpProtocols;
        set => state.IpProtocols = value;
    }
    internal List<IpProtocol> selectableIpProtocols
    {
        get => state.SelectableIpProtocols;
        set => state.SelectableIpProtocols = value;
    }
    internal List<RuleAction> ruleActions
    {
        get => state.RuleActions;
        set => state.RuleActions = value;
    }
    internal List<Tracking> trackings
    {
        get => state.Trackings;
        set => state.Trackings = value;
    }

    internal bool displayOwnerEditor
    {
        get => state.DisplayOwnerEditor;
        set => state.DisplayOwnerEditor = value;
    }
    internal WfReqTask? editingOwnerTask
    {
        get => state.EditingOwnerTask;
        set => state.EditingOwnerTask = value;
    }
    internal FwoOwner selectedOwner
    {
        get => state.SelectedOwner;
        set => state.SelectedOwner = value;
    }
    internal bool displayRuleActionEditor
    {
        get => state.DisplayRuleActionEditor;
        set => state.DisplayRuleActionEditor = value;
    }
    internal WfReqTask? editingRuleActionTask
    {
        get => state.EditingRuleActionTask;
        set => state.EditingRuleActionTask = value;
    }
    internal RuleAction? selectedRuleAction
    {
        get => state.SelectedRuleAction;
        set => state.SelectedRuleAction = value;
    }
    internal bool displayTrackingEditor
    {
        get => state.DisplayTrackingEditor;
        set => state.DisplayTrackingEditor = value;
    }
    internal WfReqTask? editingTrackingTask
    {
        get => state.EditingTrackingTask;
        set => state.EditingTrackingTask = value;
    }
    internal Tracking? selectedTracking
    {
        get => state.SelectedTracking;
        set => state.SelectedTracking = value;
    }
    internal bool displayManagementEditor
    {
        get => state.DisplayManagementEditor;
        set => state.DisplayManagementEditor = value;
    }
    internal WfReqTask? editingManagementTask
    {
        get => state.EditingManagementTask;
        set => state.EditingManagementTask = value;
    }
    internal Management? selectedManagement
    {
        get => state.SelectedManagement;
        set => state.SelectedManagement = value;
    }
    internal bool displayTargetDatesEditor
    {
        get => state.DisplayTargetDatesEditor;
        set => state.DisplayTargetDatesEditor = value;
    }
    internal WfReqTask? editingTargetDatesTask
    {
        get => state.EditingTargetDatesTask;
        set => state.EditingTargetDatesTask = value;
    }
    internal WfReqTask? formerTimeObjectTask
    {
        get => state.FormerTimeObjectTask;
        set => state.FormerTimeObjectTask = value;
    }
    internal long? formerTimeObjectSelectorId
    {
        get => state.FormerTimeObjectSelectorId;
        set => state.FormerTimeObjectSelectorId = value;
    }
    internal long? formerTimeObjectElementId
    {
        get => state.FormerTimeObjectElementId;
        set => state.FormerTimeObjectElementId = value;
    }
    internal TargetDatesEditorModel targetDatesEditorModel
    {
        get => state.TargetDatesEditorModel;
        set => state.TargetDatesEditorModel = value;
    }
    internal bool displayTimeObjectTaskEditor
    {
        get => state.DisplayTimeObjectTaskEditor;
        set => state.DisplayTimeObjectTaskEditor = value;
    }
    internal TargetDatesEditorModel newTimeObjectTaskModel
    {
        get => state.NewTimeObjectTaskModel;
        set => state.NewTimeObjectTaskModel = value;
    }
    internal bool displayDeleteFormerTimeObjectTask
    {
        get => state.DisplayDeleteFormerTimeObjectTask;
        set => state.DisplayDeleteFormerTimeObjectTask = value;
    }
    internal WfReqTask? pendingFormerTimeObjectTask
    {
        get => state.PendingFormerTimeObjectTask;
        set => state.PendingFormerTimeObjectTask = value;
    }
    internal bool displayTextEditor
    {
        get => state.DisplayTextEditor;
        set => state.DisplayTextEditor = value;
    }
    internal bool displayElementsEditor
    {
        get => state.DisplayElementsEditor;
        set => state.DisplayElementsEditor = value;
    }
    internal WfReqTask? editingElementsTask
    {
        get => state.EditingElementsTask;
        set => state.EditingElementsTask = value;
    }
    internal string editingElementsField
    {
        get => state.EditingElementsField;
        set => state.EditingElementsField = value;
    }
    internal bool displayObjectTaskEditor
    {
        get => state.DisplayObjectTaskEditor;
        set => state.DisplayObjectTaskEditor = value;
    }
    internal bool objectTaskIsExisting
    {
        get => state.ObjectTaskIsExisting;
        set => state.ObjectTaskIsExisting = value;
    }
    internal bool objectTaskIsModify
    {
        get => state.ObjectTaskIsModify;
        set => state.ObjectTaskIsModify = value;
    }
    internal WfReqTask newObjectTask
    {
        get => state.NewObjectTask;
        set => state.NewObjectTask = value;
    }
    internal ObjectTaskKind newObjectTaskKind
    {
        get => state.NewObjectTaskKind;
        set => state.NewObjectTaskKind = value;
    }
    internal bool displayDeleteObjectTask
    {
        get => state.DisplayDeleteObjectTask;
        set => state.DisplayDeleteObjectTask = value;
    }
    internal WfReqTask? pendingObjectTask
    {
        get => state.PendingObjectTask;
        set => state.PendingObjectTask = value;
    }
    internal bool workInProgress
    {
        get => state.WorkInProgress;
        set => state.WorkInProgress = value;
    }
    internal WfReqTask? editingTextTask
    {
        get => state.EditingTextTask;
        set => state.EditingTextTask = value;
    }
    internal string textEditorKey
    {
        get => state.TextEditorKey;
        set => state.TextEditorKey = value;
    }
    internal string textEditorValue
    {
        get => state.TextEditorValue;
        set => state.TextEditorValue = value;
    }
    internal bool textEditorMultiline
    {
        get => state.TextEditorMultiline;
        set => state.TextEditorMultiline = value;
    }

    internal List<FwoOwner> OwnerOptions() => metadataEditor.OwnerOptions().ToList();
    internal List<Management> ManagementOptions => metadataEditor.ManagementOptions;

    internal Task InitializeAsync(ApiConnection apiConnection) => lookup.InitializeAsync(apiConnection);

    internal IEnumerable<List<WfReqTask>> GetTaskGroups() => lookup.GetTaskGroups();

    internal static bool IsAccessTask(WfReqTask task) => task.TaskType == WfTaskType.access.ToString();

    internal static bool IsGroupTask(WfReqTask task) => task.TaskType == WfTaskType.group_create.ToString()
        || task.TaskType == WfTaskType.group_modify.ToString()
        || task.TaskType == WfTaskType.group_delete.ToString();

    internal static string? DetailsOpen(WfReqTask task) => IsAccessTask(task) ? "open" : null;

    internal ApprovalTaskViewContext CreateTaskViewContext() => new(
        new ApprovalTaskDisplayLogic(
            context.WfHandler,
            context.UserConfig,
            state.RuleActions,
            state.Trackings,
            state.IpProtocols,
            state.NetworkZones,
            state.AccessElementDisplayMode),
        AccessTaskVisibility,
        state.IpProtocols,
        new ApprovalTaskViewActions
        {
            CanEdit = CanEdit,
            CanEditTimeObject = tasks => !ReadOnlyDisplay && timeObjectEditor.CanEditTimeObject(tasks),
            EditableTimeObjectField = timeObjectEditor.EditableTimeObjectField,
            EditOwner = metadataEditor.OpenOwnerEditor,
            EditRuleAction = metadataEditor.OpenRuleActionEditor,
            EditTracking = metadataEditor.OpenTrackingEditor,
            EditManagement = metadataEditor.OpenManagementEditor,
            EditField = elementsEditor.OpenElementsEditor,
            EditGroupMembers = elementsEditor.OpenGroupMembersEditor,
            EditObjectTask = elementsEditor.OpenExistingObjectTaskEditor,
            EditText = metadataEditor.OpenTextEditor,
            EditTargetDates = timeObjectEditor.OpenTargetDatesEditor
        });

    internal bool CanEdit(IEnumerable<WfReqTask> tasks, string fieldKey)
    {
        bool plannerOwnerEdit = PlannerOwnerChangesAllowed && fieldKey == WorkflowEditableFieldKeys.Owner;
        if (ReadOnlyDisplay && !plannerOwnerEdit)
        {
            return false;
        }

        List<WfReqTask> taskList = tasks.ToList();
        if (taskList.Count == 0
            || taskList.Any(task => !context.WfHandler.CanActOnReqTaskInCurrentPhase(task)))
        {
            return false;
        }

        if (plannerOwnerEdit)
        {
            return context.WfHandler.Phase == WorkflowPhases.planning && !context.WfHandler.ReadOnlyMode;
        }

        WfReqTask firstTask = taskList.First();
        WfTaskType taskType = Enum.TryParse(firstTask.TaskType ?? "", true, out WfTaskType parsedTaskType)
            ? parsedTaskType
            : WfTaskType.access;
        if (WfObjectTaskHelper.IsObjectTask(taskType)
            && WfObjectTaskHelper.HasFlowDbReference(firstTask!)
            && (fieldKey == WorkflowEditableFieldKeys.Name || fieldKey == WorkflowEditableFieldKeys.Content))
        {
            return false;
        }

        return ApproverAllowedChangesAccess.CanEditTaskField(AllowedChanges, new()
        {
            IsApprovalPhase = true,
            ApproveReqTaskMode = true,
            ReadOnlyMode = context.WfHandler.ReadOnlyMode,
            TaskType = taskType,
            HasImplementationTasks = taskList.Any(task => task.ImplementationTasks.Count > 0)
        }, fieldKey);
    }

    internal bool CanCreateTimeObjectTask => timeObjectEditor.CanCreateTimeObjectTask;

    internal Task OpenOwnerEditor(WfReqTask task) => metadataEditor.OpenOwnerEditor(task);
    internal Task OpenRuleActionEditor(WfReqTask task) => metadataEditor.OpenRuleActionEditor(task);
    internal Task OpenTrackingEditor(WfReqTask task) => metadataEditor.OpenTrackingEditor(task);
    internal Task OpenManagementEditor(WfReqTask task) => metadataEditor.OpenManagementEditor(task);
    internal Task OpenTargetDatesEditor(WfReqTask task, string fieldKey) => timeObjectEditor.OpenTargetDatesEditor(task, fieldKey);
    internal Task OpenCreateTimeObjectTaskEditor() => timeObjectEditor.OpenCreateTimeObjectTaskEditor();
    internal Task OpenTextEditor(WfReqTask task, string fieldKey, string value) => metadataEditor.OpenTextEditor(task, fieldKey, value);
    internal Task OpenElementsEditor(WfReqTask task, string fieldKey) => elementsEditor.OpenElementsEditor(task, fieldKey);
    internal Task OpenGroupMembersEditor(WfReqTask task) => elementsEditor.OpenGroupMembersEditor(task);
    internal Task OpenExistingObjectTaskEditor(WfReqTask task) => elementsEditor.OpenExistingObjectTaskEditor(task);
    internal Task OpenObjectTaskEditor() => elementsEditor.OpenObjectTaskEditor();

    internal Task SaveObjectTask(WfReqTask task) => RunWithProgress(() => elementsEditor.SaveObjectTaskCore(task));
    internal Task HandleObjectTaskEditorDisplayChanged(bool display) => elementsEditor.HandleObjectTaskEditorDisplayChanged(display);
    internal Task SaveObjectTaskCore(WfReqTask task) => elementsEditor.SaveObjectTaskCore(task);
    internal Task RefreshAccessTasksUsingObject(WfReqTask task) => elementsEditor.RefreshAccessTasksUsingObject(task);
    internal static void CopyObjectSnapshot(WfReqElement target, WfReqElement source) => ApprovalTaskDetailsElementsEditor.CopyObjectSnapshot(target, source);
    internal static bool SamePositiveId(long? left, long? right) => ApprovalTaskDetailsElementsEditor.SamePositiveId(left, right);
    internal Task DeleteObjectTask() => RunWithProgress(elementsEditor.DeleteObjectTaskCore);
    internal Task DeleteObjectTaskCore() => elementsEditor.DeleteObjectTaskCore();
    internal Task SaveElements(string fieldKey, IReadOnlyList<NwObjectElement> sources,
        IReadOnlyList<NwObjectElement> destinations, IReadOnlyList<NwServiceElement> services) =>
        RunWithProgress(() => elementsEditor.SaveElementsCore(fieldKey, sources, destinations, services));
    internal Task SaveElementsCore(string fieldKey, IReadOnlyList<NwObjectElement> sources,
        IReadOnlyList<NwObjectElement> destinations, IReadOnlyList<NwServiceElement> services) =>
        elementsEditor.SaveElementsCore(fieldKey, sources, destinations, services);
    internal Task RefreshAccessTasksUsingGroup(WfReqTask task) => elementsEditor.RefreshAccessTasksUsingGroup(task);
    internal static WfReqElement? FindGroupMemberByReference(WfReqElement accessElement,
        IEnumerable<WfReqElement> currentElements) => ApprovalTaskDetailsElementsEditor.FindGroupMemberByReference(accessElement, currentElements);
    internal bool IsObjectTaskReferenced(WfReqTask task) => elementsEditor.IsObjectTaskReferenced(task);

    internal Task RefreshAfterEditorOpen()
    {
        try
        {
            return context.RefreshUi();
        }
        catch (InvalidOperationException)
        {
            // Private callbacks can be invoked on an unrendered component in unit tests.
            return Task.CompletedTask;
        }
    }

    internal async Task RunWithProgress(Func<Task> action)
    {
        if (state.WorkInProgress)
        {
            return;
        }

        state.WorkInProgress = true;
        await RefreshAfterOperation();
        try
        {
            await action();
        }
        finally
        {
            state.WorkInProgress = false;
            await RefreshAfterOperation();
        }
    }

    private async Task RefreshAfterOperation()
    {
        try
        {
            await context.RefreshUi();
        }
        catch (InvalidOperationException)
        {
            // Unit tests can invoke callbacks before the component has a render handle.
        }
    }

    internal Task SaveRuleAction(RuleAction? ruleAction) => RunWithProgress(() => metadataEditor.SaveRuleActionCore(ruleAction));
    internal Task SaveRuleActionCore(RuleAction? ruleAction) => metadataEditor.SaveRuleActionCore(ruleAction);
    internal Task SaveOwner(FwoOwner owner) => RunWithProgress(() => metadataEditor.SaveOwnerCore(owner));
    internal Task SaveOwnerCore(FwoOwner owner) => metadataEditor.SaveOwnerCore(owner);
    internal Task SaveTracking(Tracking? tracking) => RunWithProgress(() => metadataEditor.SaveTrackingCore(tracking));
    internal Task SaveTrackingCore(Tracking? tracking) => metadataEditor.SaveTrackingCore(tracking);
    internal Task SaveManagement(Management? management) => RunWithProgress(() => metadataEditor.SaveManagementCore(management));
    internal Task SaveManagementCore(Management? management) => metadataEditor.SaveManagementCore(management);
    internal Task SaveTargetDates(TargetDatesEditorModel model) => RunWithProgress(() => timeObjectEditor.SaveTargetDatesCore(model));
    internal Task SaveTargetDatesCore(TargetDatesEditorModel model) => timeObjectEditor.SaveTargetDatesCore(model);
    internal Task CreateTimeObjectTask(TargetDatesEditorModel model) => RunWithProgress(() => timeObjectEditor.CreateTimeObjectTaskCore(model));
    internal void InitializeNewObjectTaskState(WfReqTask task) => timeObjectEditor.InitializeNewObjectTaskState(task);
    internal bool ShouldOfferFormerTimeObjectDeletion(TargetDatesEditorModel model) => timeObjectEditor.ShouldOfferFormerTimeObjectDeletion(model);
    internal bool UsesFormerTimeObject(WfReqTask task) => timeObjectEditor.UsesFormerTimeObject(task);
    internal Task DeleteFormerTimeObjectTask() => RunWithProgress(timeObjectEditor.DeleteFormerTimeObjectTaskCore);
    internal Task DeleteFormerTimeObjectTaskCore() => timeObjectEditor.DeleteFormerTimeObjectTaskCore();
    internal IEnumerable<WfReqTask> GetTasksToUpdate(WfReqTask task) => timeObjectEditor.GetTasksToUpdate(task);
    internal void ApplyTimeObjectModel(TargetDatesEditorModel model) => timeObjectEditor.ApplyTimeObjectModel(model);
    internal List<FlowTimeObject> GetTicketTimeObjects() => lookup.GetTicketTimeObjects();
    internal Dictionary<long, long> GetTicketTimeObjectElementIds() => lookup.GetTicketTimeObjectElementIds();
    internal Task SaveText(string value) => RunWithProgress(() => metadataEditor.SaveTextCore(value));
    internal Task SaveTextCore(string value) => metadataEditor.SaveTextCore(value);
    internal static string TaskIds(IEnumerable<WfReqTask> tasks) => string.Join(", ", tasks.Select(task => task.Id));
    internal bool CanEditTimeObject(IEnumerable<WfReqTask> tasks) => timeObjectEditor.CanEditTimeObject(tasks);
    internal string EditableTimeObjectField(IEnumerable<WfReqTask> tasks) => timeObjectEditor.EditableTimeObjectField(tasks);

    internal void ToggleAccessElementDisplay()
    {
        state.AccessElementDisplayMode = state.AccessElementDisplayMode switch
        {
            ApprovalTaskElementDisplayMode.Resolved => ApprovalTaskElementDisplayMode.Technical,
            ApprovalTaskElementDisplayMode.Technical => ApprovalTaskElementDisplayMode.UnresolvedGroups,
            _ => ApprovalTaskElementDisplayMode.Resolved
        };
    }

    internal string NextDisplayModeTextKey() => state.AccessElementDisplayMode switch
    {
        ApprovalTaskElementDisplayMode.Resolved => "technical_view",
        ApprovalTaskElementDisplayMode.Technical => "unresolved_groups_view",
        _ => "resolved_view"
    };
}
