using System.Text;
using FWO.Config.Api;
using FWO.Data.Workflow;
using FWO.Services.Workflow;

namespace FWO.Ui.Services
{
    /// <summary>Compliance status of a task or bundle: a bundle is only compliant if all its checked tasks are.</summary>
    /// <param name="IsCompliant">Whether all checked tasks comply with the policies.</param>
    /// <param name="Reasons">Distinct violation reasons of all tasks.</param>
    public sealed record ApprovalTaskCompliance(bool IsCompliant, IReadOnlyList<string> Reasons)
    {
        /// <summary>Merges the results of the given tasks; null if none of them was checked.</summary>
        /// <param name="tasks">The tasks, e.g. a single task or a bundle displayed as one.</param>
        /// <param name="results">The compliance results per task id.</param>
        public static ApprovalTaskCompliance? Of(IEnumerable<WfReqTask> tasks, IReadOnlyDictionary<long, RequestTaskComplianceResult>? results)
        {
            if (results == null)
            {
                return null;
            }
            List<RequestTaskComplianceResult> taskResults = [.. tasks
                .Select(task => results.TryGetValue(task.Id, out RequestTaskComplianceResult? result) ? result : null)
                .OfType<RequestTaskComplianceResult>()];
            if (taskResults.Count == 0)
            {
                return null;
            }
            return new(taskResults.All(result => result.IsCompliant), [.. taskResults.SelectMany(result => result.Reasons).Distinct()]);
        }

        /// <summary>Returns the localized status text ("Ok" or "not allowed").</summary>
        /// <param name="userConfig">The current user configuration.</param>
        public string StatusText(UserConfig userConfig)
        {
            return userConfig.GetText(IsCompliant ? "approval_compliance_ok" : "approval_compliance_not_allowed");
        }

        /// <summary>Returns the reasons as tooltip text, one reason per line.</summary>
        public string Tooltip => string.Join("\n", Reasons);
    }

    /// <summary>
    /// Provides the compliance status of the access tasks of the displayed ticket for all approval views.
    /// The status comes from the policy check of the conditional auto-promote actions, the same check that decides
    /// the conditional state transition; it is only available if such a check is configured.
    /// The check is repeated whenever the checked content of the ticket's tasks changes.
    /// </summary>
    public sealed class ApprovalTaskComplianceService
    {
        private string? checkedFingerprint;
        private int run;

        /// <summary>Whether a conditional policy check is configured, i.e. whether the compliance status is shown at all.</summary>
        public bool IsConfigured { get; private set; }

        /// <summary>Whether the check is running.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>Compliance results per task id; null while the check is running or if it could not be performed.</summary>
        public IReadOnlyDictionary<long, RequestTaskComplianceResult>? Results { get; private set; }

        /// <summary>Returns the merged compliance status of the given tasks; null if none of them was checked.</summary>
        /// <param name="tasks">A single task or a bundle displayed as one.</param>
        public ApprovalTaskCompliance? ComplianceOf(IEnumerable<WfReqTask> tasks) => ApprovalTaskCompliance.Of(tasks, Results);

        /// <summary>
        /// Checks the tasks of the handler's ticket unless the same content has already been checked.
        /// The loading state is set before the first await, so callers can render it while the check runs.
        /// </summary>
        /// <param name="wfHandler">The workflow handler with the displayed ticket and the workflow actions.</param>
        /// <returns>True if the status changed, i.e. a check was performed or the configuration changed.</returns>
        public async Task<bool> UpdateAsync(WfHandler wfHandler)
        {
            string fingerprint = Fingerprint(wfHandler.ActTicket);
            if (fingerprint == checkedFingerprint)
            {
                return false;
            }
            checkedFingerprint = fingerprint;
            ActionHandler? actionHandler = wfHandler.ActionHandler;
            IsConfigured = actionHandler != null && actionHandler.GetConditionalPolicyCheckIds().Count > 0;
            Results = null;
            int currentRun = ++run;
            if (!IsConfigured)
            {
                IsLoading = false;
                return true;
            }

            IsLoading = true;
            IReadOnlyDictionary<long, RequestTaskComplianceResult>? results = await actionHandler!.CheckRequestTaskCompliance(wfHandler.ActTicket);
            if (currentRun == run)
            {
                Results = results;
                IsLoading = false;
            }
            return true;
        }

        /// <summary>Forgets the checked state, so the next update checks again (e.g. when the view is reopened).</summary>
        public void Reset()
        {
            checkedFingerprint = null;
            run++;
            IsLoading = false;
            Results = null;
        }

        /// <summary>Describes the ticket content relevant for the check; a change triggers a new check.</summary>
        private static string Fingerprint(WfTicket ticket)
        {
            StringBuilder builder = new();
            builder.Append(ticket.Id);
            foreach (WfReqTask task in ticket.Tasks)
            {
                builder.Append('|').Append(task.Id).Append(';').Append(task.TaskType).Append(';').Append(task.RequestAction)
                    .Append(';').Append(task.ManagementId);
                foreach (WfReqElement element in task.Elements)
                {
                    builder.Append('#').Append(element.Field).Append(';').Append(element.Name).Append(';').Append(element.IpString)
                        .Append(';').Append(element.IpEnd).Append(';').Append(element.Port).Append(';').Append(element.PortEnd)
                        .Append(';').Append(element.ProtoId).Append(';').Append(element.GroupName).Append(';').Append(element.RequestAction);
                }
            }
            return builder.ToString();
        }
    }
}
