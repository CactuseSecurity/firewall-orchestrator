using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services
{
    /// <summary>
    /// Resolves common notification placeholders from the context available at send time.
    /// </summary>
    public static class NotificationPlaceholderResolver
    {
        /// <summary>
        /// Replaces owner and time interval placeholders.
        /// </summary>
        public static string ReplaceOwnerPlaceholders(string text, FwoOwner? owner, string timeIntervalText = "")
        {
            return text
                .Replace(Placeholder.APPNAME, owner?.Name ?? "")
                .Replace(Placeholder.APPID, owner?.ExtAppId ?? "")
                .Replace(Placeholder.TIME_INTERVAL, timeIntervalText);
        }

        /// <summary>
        /// Replaces workflow placeholders.
        /// </summary>
        public static string ReplaceWorkflowPlaceholders(string text, WfStatefulObject statefulObject, FwoOwner? owner)
        {
            return ReplaceOwnerPlaceholders(text, owner ?? GetWorkflowOwner(statefulObject))
                .Replace(Placeholder.REQUESTER, GetRequesterName(statefulObject));
        }

        private static FwoOwner? GetWorkflowOwner(WfStatefulObject statefulObject)
        {
            return statefulObject switch
            {
                WfTicket ticket => ticket.Tasks.SelectMany(task => task.Owners).FirstOrDefault()?.Owner,
                WfReqTask reqTask => reqTask.Owners.FirstOrDefault()?.Owner,
                _ => null
            };
        }

        private static string GetRequesterName(WfStatefulObject statefulObject)
        {
            if (statefulObject is WfTicket ticket)
            {
                return FirstNonEmpty(ticket.Requester?.Name, ticket.RequesterDn);
            }
            return "";
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }
    }
}
