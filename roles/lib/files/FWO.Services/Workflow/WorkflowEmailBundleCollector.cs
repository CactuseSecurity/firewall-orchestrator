using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Collects the emails of one workflow email bundle until it is flushed. Emails captured here are
    /// suppressed at their state action, so anything left in this collector is an email that was not sent.
    /// </summary>
    public sealed class WorkflowEmailBundleCollector
    {
        /// <summary>
        /// True while the bundle is being flushed, so the emails it replays are not captured again.
        /// </summary>
        public bool IsFlushing { get; set; }

        /// <summary>
        /// Last activity timestamp, used by the store to expire abandoned bundles.
        /// </summary>
        public DateTime LastTouchedAt { get; private set; } = DateTime.UtcNow;

        /// <summary>
        /// DN of the user this bundle was created for. Empty for collectors that are not store managed.
        /// </summary>
        public string CallerDn { get; }

        /// <summary>
        /// Emails captured so far, grouped by their bundle key at flush time.
        /// </summary>
        public List<WorkflowEmailBundleItem> PendingItems { get; } = [];

        /// <summary>
        /// Creates a collector that is not managed by a store, for a bundle that lives inside one operation.
        /// </summary>
        public WorkflowEmailBundleCollector() : this("")
        {
        }

        /// <summary>
        /// Creates a collector bound to the user that started the bundle.
        /// </summary>
        /// <param name="callerDn">DN of the user the bundle belongs to</param>
        public WorkflowEmailBundleCollector(string callerDn)
        {
            CallerDn = callerDn ?? "";
        }

        /// <summary>
        /// Checks whether the bundle belongs to the given caller. A bundle spans several requests, so its
        /// ownership is what keeps one user from flushing another user's captured emails.
        /// </summary>
        /// <param name="callerDn">DN of the calling user</param>
        /// <returns>true if the caller may use this bundle</returns>
        public bool BelongsTo(string callerDn)
        {
            return CallerDn.Length == 0 || CallerDn == callerDn;
        }

        /// <summary>
        /// Captures an email into the bundle, unless the bundle has reached its capacity.
        /// </summary>
        /// <param name="action">State action that triggered the email</param>
        /// <param name="requestTask">Request task the email belongs to</param>
        /// <param name="owner">Owner the recipients were resolved for, if any</param>
        /// <param name="userGrpDn">User group DN the email was triggered for, if any</param>
        /// <returns>true if the email was captured; false if the caller has to send it immediately</returns>
        public bool TryAdd(WfStateAction action, WfReqTask requestTask, FwoOwner? owner, string? userGrpDn)
        {
            if (PendingItems.Count >= WorkflowEmailBundleStore.kMaxPendingItemsPerBundle)
            {
                return false;
            }

            Touch();
            PendingItems.Add(new WorkflowEmailBundleItem(action, requestTask, owner, userGrpDn));
            return true;
        }

        /// <summary>
        /// Updates the collector activity timestamp for expiry cleanup.
        /// </summary>
        public void Touch()
        {
            LastTouchedAt = DateTime.UtcNow;
        }
    }

    public sealed class WorkflowEmailBundleItem
    {
        public WfStateAction Action { get; }
        public WfReqTask RequestTask { get; }
        public FwoOwner? Owner { get; }
        public string? UserGrpDn { get; }
        public string BundleKey { get; }

        public WorkflowEmailBundleItem(WfStateAction action, WfReqTask requestTask, FwoOwner? owner, string? userGrpDn)
        {
            Action = new WfStateAction(action);
            RequestTask = new WfReqTask(requestTask);
            Owner = owner;
            UserGrpDn = userGrpDn;
            BundleKey = BuildBundleKey(Action, RequestTask, Owner, UserGrpDn);
        }

        /// <summary>
        /// Builds the request-task part of the bundle key. Two request tasks may only share an email
        /// when every component matches, ownership included: tasks of one ticket can belong to
        /// different owners, and merging them would disclose another owner's requested connections.
        /// </summary>
        /// <param name="requestTask">Request task to derive the key from</param>
        /// <returns>Key covering the task properties that must agree within a bundle</returns>
        public static string BuildTaskBundleKey(WfReqTask requestTask)
        {
            return string.Join("|",
                requestTask.TicketId,
                requestTask.TaskType,
                requestTask.StateId,
                requestTask.GetAddInfoValue(AdditionalInfoKeys.FwConfigChangeTarget),
                requestTask.AssignedGroup,
                requestTask.CurrentHandler?.Dn,
                requestTask.RecentHandler?.Dn,
                BuildOwnerKey(requestTask));
        }

        /// <summary>
        /// Builds the full bundle key: the request-task part plus the action and recipient context
        /// the email was captured for.
        /// </summary>
        /// <param name="action">State action that triggered the email</param>
        /// <param name="requestTask">Request task the email was captured for</param>
        /// <param name="owner">Owner the recipients were resolved for, if any</param>
        /// <param name="userGrpDn">User group DN the email was captured for, if any</param>
        /// <returns>Key identifying one bundle group</returns>
        public static string BuildBundleKey(WfStateAction action, WfReqTask requestTask, FwoOwner? owner, string? userGrpDn)
        {
            return string.Join("|",
                BuildTaskBundleKey(requestTask),
                action.Id,
                action.ExternalParams,
                owner?.Id,
                userGrpDn);
        }

        private static string BuildOwnerKey(WfReqTask requestTask)
        {
            return string.Join(",", requestTask.Owners
                .Select(ownerData => ownerData.Owner.Id)
                .Distinct()
                .OrderBy(ownerId => ownerId));
        }
    }
}
