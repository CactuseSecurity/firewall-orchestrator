using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using System.Net;

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
        /// Maximum length of a caller-provided placeholder text such as an interface or app name.
        /// </summary>
        public const int kMaxCallerTextLength = 1000;

        /// <summary>
        /// Maximum length of the caller-provided content of a workflow email, e.g. the reason for rejecting an interface.
        /// </summary>
        public const int kMaxCallerContentLength = 10000;

        /// <summary>
        /// Replaces owner and time interval placeholders.
        /// </summary>
        /// <param name="text">Subject or body template.</param>
        /// <param name="owner">Owner whose name and id are inserted.</param>
        /// <param name="timeIntervalText">Text for the time interval placeholder.</param>
        /// <param name="isHtmlBody">True when the text is an HTML body, so that every value is HTML-encoded.</param>
        public static string ReplaceOwnerPlaceholders(string text, FwoOwner? owner, string timeIntervalText = "", bool isHtmlBody = false)
        {
            Func<string?, string> insert = SelectValueEncoding(isHtmlBody);
            return text
                .Replace(Placeholder.APPNAME, insert(owner?.Name))
                .Replace(Placeholder.APPID, insert(owner?.ExtAppId))
                .Replace(Placeholder.TIME_INTERVAL, insert(timeIntervalText));
        }

        /// <summary>
        /// Replaces workflow placeholders.
        /// </summary>
        /// <param name="text">Subject or body template.</param>
        /// <param name="statefulObject">Workflow object the values are derived from.</param>
        /// <param name="owner">Owner of the workflow object, if known.</param>
        /// <param name="placeholderData">Additional values, e.g. of the interface a request is about.</param>
        /// <param name="isHtmlBody">True when the text is an HTML body, so that every value is HTML-encoded and links are rendered as anchors.</param>
        public static string ReplaceWorkflowPlaceholders(string text, WfStatefulObject statefulObject, FwoOwner? owner,
            NotificationPlaceholderData? placeholderData = null, bool isHtmlBody = false)
        {
            NotificationPlaceholderValues values = new()
            {
                Application = owner ?? GetWorkflowOwner(statefulObject) ?? new(),
                RequesterName = GetRequesterName(statefulObject),
                Reason = GetWorkflowReason(statefulObject),
                RequestDate = GetWorkflowRequestDate(statefulObject)
            };
            values = ApplyWorkflowPlaceholderData(values, placeholderData);
            return ReplaceNotificationPlaceholders(text, values, isHtmlBody);
        }

        /// <summary>
        /// Replaces notification placeholders.
        /// </summary>
        /// <param name="text">Subject or body template.</param>
        /// <param name="values">The values to insert.</param>
        /// <param name="isHtmlBody">True when the text is an HTML body, so that every value is HTML-encoded and links are rendered as anchors.</param>
        public static string ReplaceNotificationPlaceholders(string text, NotificationPlaceholderValues values, bool isHtmlBody = false)
        {
            Func<string?, string> insert = SelectValueEncoding(isHtmlBody);
            return text
                .Replace(Placeholder.APPNAME, insert(values.Application.Name))
                .Replace(Placeholder.APPID, insert(values.Application.ExtAppId))
                .Replace(Placeholder.REQUESTING_APPNAME, insert(values.RequestingOwner?.Name))
                .Replace(Placeholder.REQUESTING_APPID, insert(values.RequestingOwner?.ExtAppId))
                .Replace(Placeholder.REQUESTER, insert(FirstNonEmpty(values.RequesterName, values.RequestingOwner?.Name)))
                .Replace(Placeholder.REQUESTDATE, insert(values.RequestDate))
                .Replace(Placeholder.INTERFACE_NAME, insert(values.InterfaceName))
                .Replace(Placeholder.INTERFACE_LINK, RenderLink(values.InterfaceLinkUrl, values.InterfaceLinkText, values.InterfaceLinkName, isHtmlBody))
                .Replace(Placeholder.NEW_INTERFACE_NAME, insert(values.NewInterfaceName))
                .Replace(Placeholder.NEW_INTERFACE_LINK, RenderLink(values.NewInterfaceLinkUrl, values.NewInterfaceLinkText, values.NewInterfaceLinkName, isHtmlBody))
                .Replace(Placeholder.REASON, insert(values.Reason))
                .Replace(Placeholder.USER_NAME, insert(values.UserName));
        }

        /// <summary>
        /// Restricts placeholder values sent by a caller of the workflow action endpoint to what the caller may decide.
        /// The user name is the authenticated caller, link targets have to lead into the FWO UI, and reason and requester
        /// are always taken from the workflow object. All remaining values are free text, which is encoded when inserted.
        /// </summary>
        /// <param name="data">The values sent by the caller; null when none were sent.</param>
        /// <param name="callerName">Name of the authenticated caller.</param>
        /// <param name="uiHostName">Configured base URL of the FWO UI.</param>
        /// <param name="error">Why the values were rejected; null when they were accepted.</param>
        /// <returns>The restricted values; null when none were sent or they were rejected.</returns>
        public static NotificationPlaceholderData? RestrictCallerPlaceholderData(NotificationPlaceholderData? data, string callerName,
            string uiHostName, out string? error)
        {
            error = FindOversizedCallerValue(data);
            if (data == null || error != null)
            {
                return null;
            }

            return new NotificationPlaceholderData
            {
                RequestingAppName = data.RequestingAppName,
                RequestingAppId = data.RequestingAppId,
                InterfaceName = data.InterfaceName,
                InterfaceLinkText = data.InterfaceLinkText,
                InterfaceLinkName = data.InterfaceLinkName,
                InterfaceLinkUrl = NormalizeUiLink(data.InterfaceLinkUrl, uiHostName),
                NewInterfaceName = data.NewInterfaceName,
                NewInterfaceLinkText = data.NewInterfaceLinkText,
                NewInterfaceLinkName = data.NewInterfaceLinkName,
                NewInterfaceLinkUrl = NormalizeUiLink(data.NewInterfaceLinkUrl, uiHostName),
                Content = data.Content,
                UserName = callerName,
                RequestDate = data.RequestDate
            };
        }

        /// <summary>
        /// True for an absolute http(s) URL below the configured FWO UI base URL.
        /// </summary>
        /// <param name="url">The link target to check.</param>
        /// <param name="uiHostName">Configured base URL of the FWO UI.</param>
        public static bool IsUiLink(string? url, string uiHostName)
        {
            return TryParseUiLink(url, uiHostName, out _);
        }

        /// <summary>
        /// Returns the normalised form of a link into the configured FWO UI, in which characters such as quotes and
        /// angle brackets are percent-encoded, or an empty string for any other link.
        /// </summary>
        /// <param name="url">The link target to check.</param>
        /// <param name="uiHostName">Configured base URL of the FWO UI.</param>
        public static string NormalizeUiLink(string? url, string uiHostName)
        {
            return TryParseUiLink(url, uiHostName, out Uri? link) ? link!.AbsoluteUri : "";
        }

        private static bool TryParseUiLink(string? url, string uiHostName, out Uri? link)
        {
            link = null;
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(uiHostName)
                || !Uri.TryCreate(url, UriKind.Absolute, out Uri? parsedLink) || !Uri.TryCreate(uiHostName, UriKind.Absolute, out Uri? uiBase))
            {
                return false;
            }

            bool isHttp = parsedLink.Scheme == Uri.UriSchemeHttps || parsedLink.Scheme == Uri.UriSchemeHttp;
            bool sameOrigin = string.Equals(parsedLink.GetLeftPart(UriPartial.Authority), uiBase.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
            string basePath = uiBase.AbsolutePath.TrimEnd('/') + "/";
            if (!isHttp || !sameOrigin || !parsedLink.AbsolutePath.StartsWith(basePath, StringComparison.Ordinal))
            {
                return false;
            }
            link = parsedLink;
            return true;
        }

        private static string? FindOversizedCallerValue(NotificationPlaceholderData? data)
        {
            if (data == null)
            {
                return null;
            }
            if (data.Content.Length > kMaxCallerContentLength)
            {
                return $"Notification content must not be longer than {kMaxCallerContentLength} characters.";
            }

            List<string> texts =
            [
                data.RequestingAppName, data.RequestingAppId, data.InterfaceName, data.InterfaceLinkText, data.InterfaceLinkName,
                data.InterfaceLinkUrl, data.NewInterfaceName, data.NewInterfaceLinkText, data.NewInterfaceLinkName,
                data.NewInterfaceLinkUrl, data.Reason, data.UserName, data.RequesterName, data.RequestDate
            ];
            return texts.Any(text => (text?.Length ?? 0) > kMaxCallerTextLength)
                ? $"Notification placeholder values must not be longer than {kMaxCallerTextLength} characters."
                : null;
        }

        private static Func<string?, string> SelectValueEncoding(bool isHtmlBody)
        {
            return isHtmlBody ? HtmlOutputEncoder.EncodeText : value => value ?? "";
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

            string encodedUrl = WebUtility.HtmlEncode(url);
            string displayText = string.IsNullOrWhiteSpace(text) ? linkName : text;
            if (string.IsNullOrWhiteSpace(displayText))
            {
                // the url is shown as text of an HTML body, so it is encoded like every other value
                return encodedUrl;
            }

            string encodedDisplayText = WebUtility.HtmlEncode(displayText);
            string encodedLinkName = WebUtility.HtmlEncode(linkName);

            if (string.IsNullOrWhiteSpace(linkName))
            {
                return $"<a target=\"_blank\" href=\"{encodedUrl}\">{encodedDisplayText}</a>";
            }

            return $"<a target=\"_blank\" href=\"{encodedUrl}\">{encodedDisplayText}: {encodedLinkName}</a>";
        }
    }
}
