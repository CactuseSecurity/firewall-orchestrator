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
        /// Carries the values required to resolve notification placeholders.
        /// Callers populate the data they have; the resolver decides how to render it.
        /// </summary>
        public sealed record NotificationPlaceholderValues
        {
            public FwoOwner Application { get; init; } = new();
            public FwoOwner? RequestingOwner { get; init; }
            public UiUser? Requester { get; init; }
            public string InterfaceName { get; init; } = "";
            public string InterfaceLinkText { get; init; } = "";
            public string InterfaceLinkName { get; init; } = "";
            public string InterfaceLinkUrl { get; init; } = "";
            public string NewInterfaceName { get; init; } = "";
            public string NewInterfaceLinkText { get; init; } = "";
            public string NewInterfaceLinkName { get; init; } = "";
            public string NewInterfaceLinkUrl { get; init; } = "";
            public string Reason { get; init; } = "";
            public string UserName { get; init; } = "";
            public string RequesterName { get; init; } = "";
            public string RequestDate { get; init; } = "";
        }

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
        public static string ReplaceWorkflowPlaceholders(string text, WfStatefulObject statefulObject, FwoOwner? owner,
            NotificationPlaceholderData? placeholderData = null, bool renderHtmlLinks = false)
        {
            NotificationPlaceholderValues values = new()
            {
                Application = owner ?? GetWorkflowOwner(statefulObject) ?? new(),
                RequesterName = GetRequesterName(statefulObject),
                Reason = GetWorkflowReason(statefulObject),
                RequestDate = GetWorkflowRequestDate(statefulObject)
            };
            values = ApplyWorkflowPlaceholderData(values, placeholderData);
            return ReplaceNotificationPlaceholders(text, values, renderHtmlLinks);
        }

        /// <summary>
        /// Replaces notification placeholders.
        /// </summary>
        public static string ReplaceNotificationPlaceholders(string text, NotificationPlaceholderValues values, bool renderHtmlLinks = false)
        {
            return text
                .Replace(Placeholder.APPNAME, values.Application.Name ?? "")
                .Replace(Placeholder.APPID, values.Application.ExtAppId ?? "")
                .Replace(Placeholder.REQUESTING_APPNAME, values.RequestingOwner?.Name ?? "")
                .Replace(Placeholder.REQUESTING_APPID, values.RequestingOwner?.ExtAppId ?? "")
                .Replace(Placeholder.REQUESTER, FirstNonEmpty(values.RequesterName, values.RequestingOwner?.Name))
                .Replace(Placeholder.REQUESTDATE, values.RequestDate)
                .Replace(Placeholder.INTERFACE_NAME, values.InterfaceName)
                .Replace(Placeholder.INTERFACE_LINK, RenderLink(values.InterfaceLinkUrl, values.InterfaceLinkText, values.InterfaceLinkName, renderHtmlLinks))
                .Replace(Placeholder.NEW_INTERFACE_NAME, values.NewInterfaceName)
                .Replace(Placeholder.NEW_INTERFACE_LINK, RenderLink(values.NewInterfaceLinkUrl, values.NewInterfaceLinkText, values.NewInterfaceLinkName, renderHtmlLinks))
                .Replace(Placeholder.REASON, values.Reason)
                .Replace(Placeholder.USER_NAME, values.UserName);
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

        private static string GetWorkflowReason(WfStatefulObject statefulObject)
        {
            return statefulObject switch
            {
                WfTicket ticket => ticket.Reason ?? "",
                WfReqTask reqTask => reqTask.Reason ?? "",
                WfImplTask implTask => implTask.Comments.LastOrDefault()?.Comment.CommentText ?? "",
                _ => statefulObject.OptComment() ?? ""
            };
        }

        private static string GetWorkflowRequestDate(WfStatefulObject statefulObject)
        {
            return statefulObject is WfTicket ticket && ticket.CreationDate != default
                ? ticket.CreationDate.ToString("dd.MM.yyyy")
                : "";
        }

        private static NotificationPlaceholderValues ApplyWorkflowPlaceholderData(NotificationPlaceholderValues values, NotificationPlaceholderData? data)
        {
            if (data == null)
            {
                return values;
            }

            return values with
            {
                RequestingOwner = !string.IsNullOrWhiteSpace(data.RequestingAppName) || !string.IsNullOrWhiteSpace(data.RequestingAppId)
                    ? new FwoOwner { Name = data.RequestingAppName, ExtAppId = data.RequestingAppId }
                    : values.RequestingOwner,
                InterfaceName = FirstNonEmpty(data.InterfaceName, values.InterfaceName),
                InterfaceLinkText = FirstNonEmpty(data.InterfaceLinkText, values.InterfaceLinkText),
                InterfaceLinkName = FirstNonEmpty(data.InterfaceLinkName, values.InterfaceLinkName),
                InterfaceLinkUrl = FirstNonEmpty(data.InterfaceLinkUrl, values.InterfaceLinkUrl),
                NewInterfaceName = FirstNonEmpty(data.NewInterfaceName, values.NewInterfaceName),
                NewInterfaceLinkText = FirstNonEmpty(data.NewInterfaceLinkText, values.NewInterfaceLinkText),
                NewInterfaceLinkName = FirstNonEmpty(data.NewInterfaceLinkName, values.NewInterfaceLinkName),
                NewInterfaceLinkUrl = FirstNonEmpty(data.NewInterfaceLinkUrl, values.NewInterfaceLinkUrl),
                Reason = FirstNonEmpty(data.Reason, values.Reason),
                UserName = FirstNonEmpty(data.UserName, values.UserName),
                RequesterName = FirstNonEmpty(data.RequesterName, values.RequesterName),
                RequestDate = FirstNonEmpty(data.RequestDate, values.RequestDate)
            };
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }

        private static string RenderLink(string url, string text, string linkName, bool renderHtmlLinks)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return "";
            }

            if (!renderHtmlLinks)
            {
                return url;
            }

            string displayText = string.IsNullOrWhiteSpace(text) ? linkName : text;
            if (string.IsNullOrWhiteSpace(displayText))
            {
                return url;
            }

            if (string.IsNullOrWhiteSpace(linkName))
            {
                return $"<a target=\"_blank\" href=\"{url}\">{displayText}</a>";
            }

            return $"<a target=\"_blank\" href=\"{url}\">{displayText}: {linkName}</a>";
        }
    }
}
