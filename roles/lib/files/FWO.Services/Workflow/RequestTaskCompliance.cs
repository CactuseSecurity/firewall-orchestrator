using System.Text;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Helpers for the compliance status of access tasks: which tasks are checked, the stored check result
    /// and the task content the result depends on.
    /// </summary>
    public static class RequestTaskCompliance
    {
        /// <summary>
        /// Whether the task gets a compliance status: access tasks with source, destination and service that are not deleted,
        /// i.e. the tasks the policy checker maps to a rule.
        /// </summary>
        /// <param name="task">The request task.</param>
        public static bool IsCheckedTask(WfReqTask task)
        {
            return !string.Equals(task.RequestAction, nameof(RequestAction.delete), StringComparison.OrdinalIgnoreCase)
                && task.GetNwObjectElements(ElemFieldType.source).Count > 0
                && task.GetNwObjectElements(ElemFieldType.destination).Count > 0
                && task.GetServiceElements().Count > 0;
        }

        /// <summary>Returns the stored check result of the task, or null if the task was not checked yet.</summary>
        /// <param name="task">The request task.</param>
        public static bool? StoredResult(WfReqTask task)
        {
            return task.GetAddInfoBoolValue(AdditionalInfoKeys.ComplianceOk);
        }

        /// <summary>
        /// Describes the content of the ticket's tasks the check result depends on; a change of this content requires a new check.
        /// The additional info is not part of it, so storing the check result does not change the fingerprint.
        /// </summary>
        /// <param name="ticket">The ticket.</param>
        public static string Fingerprint(WfTicket ticket)
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
