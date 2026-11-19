using FWO.Basics;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Icon (css classes) displayed next to the name of a workflow state.
    /// </summary>
    public sealed record WorkflowStateIcon(string IconClass, string ColorClass)
    {
        /// <summary>
        /// All css classes of the icon element.
        /// </summary>
        public string CssClass => $"{IconClass} {ColorClass}";
    }

    /// <summary>
    /// Maps the default workflow states (see fworch-request-config.sql) to icons. States added by the customer have no icon.
    /// </summary>
    public static class WorkflowStateIcons
    {
        private const string kWaitingIcon = "bi bi-hourglass-split";
        private const string kInProgressIcon = "bi bi-play-circle-fill";
        private const string kPhaseDoneIcon = "bi bi-check-circle";
        private const string kReworkIcon = Icons.Undo;
        private const string kTroubleIcon = Icons.Warning;
        private const string kNegativeIcon = "bi bi-x-circle-fill";
        private const string kDiscardedIcon = Icons.Delete;
        private const string kTextSecondary = "text-secondary";
        private const string kTextPrimary = "text-primary";
        private const string kTextSuccess = "text-success";
        private const string kTextWarning = "text-warning";
        private const string kTextDanger = "text-danger";

        private static readonly WorkflowStateIcon kDraft = new(Icons.Edit, kTextSecondary);
        private static readonly WorkflowStateIcon kWaiting = new(kWaitingIcon, kTextSecondary);
        private static readonly WorkflowStateIcon kInProgress = new(kInProgressIcon, kTextPrimary);
        private static readonly WorkflowStateIcon kPhaseDone = new(kPhaseDoneIcon, kTextSuccess);
        private static readonly WorkflowStateIcon kRework = new(kReworkIcon, kTextWarning);
        private static readonly WorkflowStateIcon kTrouble = new(kTroubleIcon, kTextWarning);
        private static readonly WorkflowStateIcon kNegative = new(kNegativeIcon, kTextDanger);
        private static readonly WorkflowStateIcon kDone = new(Icons.Check, kTextSuccess);
        private static readonly WorkflowStateIcon kDiscarded = new(kDiscardedIcon, kTextSecondary);

        private static readonly Dictionary<int, WorkflowStateIcon> kIconsByStateId = new()
        {
            [0] = kDraft,               // Draft
            [49] = kWaiting,            // Requested
            [50] = kWaiting,            // To Approve
            [60] = kInProgress,         // In Approval
            [99] = kPhaseDone,          // Approved
            [100] = kWaiting,           // To Plan
            [110] = kInProgress,        // In Planning
            [120] = kWaiting,           // Wait For Approval
            [130] = kTrouble,           // Compliance Violation
            [149] = kPhaseDone,         // Planned
            [150] = kWaiting,           // To Verify Plan
            [160] = kInProgress,        // Plan In Verification
            [199] = kPhaseDone,         // Plan Verified
            [200] = kWaiting,           // To Implement
            [205] = kRework,            // Rework
            [210] = kInProgress,        // In Implementation
            [220] = kTrouble,           // Implementation Trouble
            [249] = kPhaseDone,         // Implemented
            [250] = kWaiting,           // To Review
            [260] = kInProgress,        // In Review
            [270] = kRework,            // Further Work Requested
            [299] = kPhaseDone,         // Verified
            [300] = kWaiting,           // To Recertify
            [310] = kInProgress,        // In Recertification
            [349] = kPhaseDone,         // Recertified
            [400] = kNegative,          // Decertified
            [500] = kInProgress,        // InProgress
            [600] = kDone,              // Done
            [610] = kNegative,          // Rejected
            [620] = kDiscarded          // Discarded
        };

        /// <summary>
        /// Returns the icon of a state, or null if the state has no icon.
        /// </summary>
        public static WorkflowStateIcon? Get(int stateId)
        {
            return kIconsByStateId.TryGetValue(stateId, out WorkflowStateIcon? icon) ? icon : null;
        }
    }
}
