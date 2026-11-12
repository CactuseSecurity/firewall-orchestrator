using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Ui.Shared;

namespace FWO.Ui.Services;

/// <summary>
/// Holds the transient state of the approval-task editors for one view instance.
/// </summary>
internal sealed class ApprovalTaskDetailsEditorState
{
    internal ApprovalTaskElementDisplayMode AccessElementDisplayMode = ApprovalTaskElementDisplayMode.Resolved;
    internal List<NetworkZone> NetworkZones = [];
    internal List<IpProtocol> IpProtocols = [];
    internal List<IpProtocol> SelectableIpProtocols = [];
    internal List<RuleAction> RuleActions = [];
    internal List<Tracking> Trackings = [];

    internal bool DisplayOwnerEditor;
    internal WfReqTask? EditingOwnerTask;
    internal FwoOwner SelectedOwner = new();
    internal bool DisplayRuleActionEditor;
    internal WfReqTask? EditingRuleActionTask;
    internal RuleAction? SelectedRuleAction;
    internal bool DisplayTrackingEditor;
    internal WfReqTask? EditingTrackingTask;
    internal Tracking? SelectedTracking;
    internal bool DisplayManagementEditor;
    internal WfReqTask? EditingManagementTask;
    internal Management? SelectedManagement;
    internal bool DisplayTargetDatesEditor;
    internal WfReqTask? EditingTargetDatesTask;
    internal WfReqTask? FormerTimeObjectTask;
    internal long? FormerTimeObjectSelectorId;
    internal long? FormerTimeObjectElementId;
    internal TargetDatesEditorModel TargetDatesEditorModel = new();
    internal bool DisplayTimeObjectTaskEditor;
    internal TargetDatesEditorModel NewTimeObjectTaskModel = new();
    internal bool DisplayDeleteFormerTimeObjectTask;
    internal WfReqTask? PendingFormerTimeObjectTask;
    internal bool DisplayTextEditor;
    internal bool DisplayElementsEditor;
    internal WfReqTask? EditingElementsTask;
    internal string EditingElementsField = WorkflowEditableFieldKeys.Source;
    internal bool DisplayObjectTaskEditor;
    internal bool ObjectTaskIsExisting;
    internal bool ObjectTaskIsModify;
    internal WfReqTask NewObjectTask = new();
    internal ObjectTaskKind NewObjectTaskKind = ObjectTaskKind.Network;
    internal bool DisplayDeleteObjectTask;
    internal WfReqTask? PendingObjectTask;
    internal bool WorkInProgress;
    internal WfReqTask? EditingTextTask;
    internal string TextEditorKey = "title";
    internal string TextEditorValue = "";
    internal bool TextEditorMultiline;
}
