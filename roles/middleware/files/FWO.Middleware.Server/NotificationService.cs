using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Config.Api;
using FWO.Mail;
using FWO.Encryption;
using FWO.Logging;
using FWO.Report;
using FWO.Services;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class to administrate and send Notifications
    /// </summary>
    public class NotificationService
    {
        /// <summary>
        /// Notifications for current NotificationClient
        /// </summary>
        public readonly List<FwoNotification> Notifications;
        private List<int> CheckedNotificationIds = [];
        private readonly ApiConnection ApiConnection;
        private readonly GlobalConfig GlobalConfig;
        private readonly List<UserGroup> OwnerGroups;
        private readonly IWorkflowRecipientResolver? WorkflowRecipientResolver;


        NotificationService(List<FwoNotification> notifications, GlobalConfig globalConfig, ApiConnection apiConnection, List<UserGroup> ownerGroups,
            IWorkflowRecipientResolver? workflowRecipientResolver = null)
        {
            ApiConnection = apiConnection;
            GlobalConfig = globalConfig;
            Notifications = notifications.Where(notification => notification.Active).ToList();
            OwnerGroups = ownerGroups;
            WorkflowRecipientResolver = workflowRecipientResolver;
        }

        /// <summary>
        /// async Constructor
        /// </summary>
        /// <param name="notificationClient"></param>
        /// <param name="globalConfig"></param>
        /// <param name="apiConnection"></param>
        /// <returns></returns>
        public static async Task<NotificationService> CreateAsync(NotificationClient notificationClient, GlobalConfig globalConfig, ApiConnection apiConnection)
        {
            List<FwoNotification> notifications = await LoadNotifications(notificationClient, apiConnection);
            if (notifications.Count == 0)
            {
                return new NotificationService(notifications, globalConfig, apiConnection, new List<UserGroup>());
            }

            List<Ldap>? connectedLdaps = await LoadLdapConnections(apiConnection);
            List<UserGroup> ownerGroups = await LoadOwnerGroups(connectedLdaps);
            IWorkflowRecipientResolver? workflowRecipientResolver = LoadWorkflowRecipientResolver(apiConnection, connectedLdaps);
            return new NotificationService(notifications, globalConfig, apiConnection, ownerGroups, workflowRecipientResolver);
        }

        /// <summary>
        /// Creates a notification service with explicitly supplied owner groups and optional recipient resolver.
        /// </summary>
        /// <param name="notificationClient"></param>
        /// <param name="globalConfig"></param>
        /// <param name="apiConnection"></param>
        /// <param name="ownerGroups"></param>
        /// <param name="workflowRecipientResolver">Optional workflow recipient resolver for LDAP-backed recipient lookup.</param>
        /// <returns></returns>
        public static async Task<NotificationService> CreateAsync(NotificationClient notificationClient, GlobalConfig globalConfig, ApiConnection apiConnection,
            List<UserGroup> ownerGroups, IWorkflowRecipientResolver? workflowRecipientResolver = null)
        {
            return new NotificationService(await LoadNotifications(notificationClient, apiConnection), globalConfig, apiConnection, ownerGroups, workflowRecipientResolver);
        }

        /// <summary>
        /// Creates a notification service with preloaded LDAP connections and owner groups.
        /// </summary>
        /// <param name="notificationClient">Notification client to load.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="connectedLdaps">Preloaded LDAP connections used for recipient resolution.</param>
        /// <param name="ownerGroups">Preloaded owner groups used for recipient resolution.</param>
        /// <returns></returns>
        public static async Task<NotificationService> CreateAsync(NotificationClient notificationClient, GlobalConfig globalConfig, ApiConnection apiConnection,
            List<Ldap> connectedLdaps, List<UserGroup> ownerGroups)
        {
            IWorkflowRecipientResolver? workflowRecipientResolver = LoadWorkflowRecipientResolver(apiConnection, connectedLdaps);
            return new NotificationService(await LoadNotifications(notificationClient, apiConnection), globalConfig, apiConnection, ownerGroups, workflowRecipientResolver);
        }

        /// <summary>
        /// Analyse and send all Notifications if due, restricted to owner if given
        /// </summary>
        /// <param name="owner">Owner for whom the notification is done</param>
        /// <param name="extDeadline">Deadline date e.g. from ticket, if not defined by owner (only for InterfaceClient)</param>
        /// <param name="content">Text for notification (e.g. email body)</param>
        /// <param name="report">Optional report to be sent as attachment</param>
        /// <param name="timeIntervalText">Optional resolved time interval text for placeholder replacement.</param>
        /// <returns>number of emails sent</returns>
        public async Task<int> SendNotificationsIfDue(FwoOwner? owner, DateTime? extDeadline, string? content, ReportBase? report = null, string timeIntervalText = "")
        {
            int emailsSent = 0;
            foreach (var notification in Notifications.Where(n => n.OwnerId == null || n.OwnerId == owner?.Id))
            {
                emailsSent += await SendNotificationIfDue(notification, owner, extDeadline, content, report, timeIntervalText);
            }
            return emailsSent;
        }

        /// <summary>
        /// Sends a single notification without checking if it is currently due.
        /// </summary>
        /// <param name="notification">Notification to send.</param>
        /// <param name="owner">Owner for whom the notification is sent.</param>
        /// <param name="content">Text for notification (e.g. email body).</param>
        /// <param name="report">Optional report to be sent as attachment.</param>
        /// <param name="timeIntervalText">Optional resolved time interval text for placeholder replacement.</param>
        /// <param name="resolvedDeadline">Resolved deadline timestamp used for notification logging.</param>
        /// <param name="placeholderValues">Optional caller-provided values for notification placeholder replacement.</param>
        /// <returns>number of emails sent</returns>
        public async Task<int> SendNotification(FwoNotification notification, FwoOwner? owner, string? content = null, ReportBase? report = null,
            string timeIntervalText = "", DateTime? resolvedDeadline = null,
            NotificationPlaceholderResolver.NotificationPlaceholderValues? placeholderValues = null)
        {
            return await SendNotificationWithResult(notification, owner, content, report, timeIntervalText,
                resolvedDeadline, placeholderValues) == NotificationDeliveryResult.Delivered ? 1 : 0;
        }

        /// <summary>
        /// Sends one active notification and preserves the complete delivery outcome.
        /// </summary>
        /// <param name="notification">Notification to send.</param>
        /// <param name="owner">Owner for whom the notification is sent.</param>
        /// <param name="content">Optional caller content for the notification body.</param>
        /// <param name="report">Optional report attachment.</param>
        /// <param name="timeIntervalText">Optional interval text for placeholders.</param>
        /// <param name="resolvedDeadline">Resolved deadline used for logging.</param>
        /// <param name="placeholderValues">Optional caller context for placeholders.</param>
        /// <returns>The explicit processing result.</returns>
        public async Task<NotificationDeliveryResult> SendNotificationWithResult(FwoNotification notification, FwoOwner? owner,
            string? content = null, ReportBase? report = null, string timeIntervalText = "", DateTime? resolvedDeadline = null,
            NotificationPlaceholderResolver.NotificationPlaceholderValues? placeholderValues = null)
        {
            if (!notification.Active)
            {
                return NotificationDeliveryResult.Suppressed;
            }

            // Later: Handle other channels here when implemented.
            NotificationDeliveryResult deliveryResult = await SendEmail(notification, content, owner, report, timeIntervalText,
                resolvedDeadline, placeholderValues);
            if (deliveryResult is NotificationDeliveryResult.Delivered
                or NotificationDeliveryResult.Suppressed
                or NotificationDeliveryResult.NoRecipients)
            {
                AddCheckedNotificationId(notification.Id);
            }
            return deliveryResult;
        }

        /// <summary>
        /// Sends notifications grouped by bundle information. Notifications without bundle settings are sent individually.
        /// </summary>
        /// <param name="notifications">Notifications to send.</param>
        /// <param name="owner">Owner for whom the notifications are sent.</param>
        /// <param name="content">Text for notification (e.g. email body).</param>
        /// <param name="report">Optional report to be sent as attachment.</param>
        /// <param name="timeIntervalText">Optional resolved time interval text for placeholder replacement.</param>
        /// <returns>number of emails sent</returns>
        public async Task<int> SendBundledNotifications(List<FwoNotification> notifications, FwoOwner? owner, string? content = null, ReportBase? report = null, string timeIntervalText = "")
        {
            int emailsSent = 0;
            foreach (IGrouping<string, FwoNotification> notificationGroup in notifications.Where(notification => notification.Active).GroupBy(GetBundleGroupKey))
            {
                List<FwoNotification> groupedNotifications = [.. notificationGroup];
                emailsSent += await SendNotificationGroup(groupedNotifications, owner, content, report, timeIntervalText);
            }
            return emailsSent;
        }

        private async Task<int> SendNotificationGroup(List<FwoNotification> notifications, FwoOwner? owner, string? content,
            ReportBase? report, string timeIntervalText)
        {
            if (notifications.Count == 1 || notifications[0].BundleType == null)
            {
                return await SendNotification(notifications[0], owner, content, report, timeIntervalText);
            }

            NotificationDeliveryResult result = await SendBundledEmail(notifications, content, owner, report, timeIntervalText);
            TrackBundledNotificationResult(notifications, result);
            return result == NotificationDeliveryResult.Delivered ? 1 : 0;
        }

        private void TrackBundledNotificationResult(List<FwoNotification> notifications, NotificationDeliveryResult result)
        {
            if (result == NotificationDeliveryResult.NoRecipients)
            {
                foreach (FwoNotification notification in notifications)
                {
                    AddCheckedNotificationId(notification.Id);
                }
                return;
            }

            if (result == NotificationDeliveryResult.Delivered)
            {
                foreach (FwoNotification notification in notifications.Where(notification => NotificationLoggingMode.ShouldSend(notification.Logging)))
                {
                    AddCheckedNotificationId(notification.Id);
                }
            }
        }

        /// <summary>
        /// Analyse and send single Notification if due
        /// </summary>
        /// <param name="notification">Notification to be handled</param>
        /// <param name="owner">Owner for whom the notification is done</param>
        /// <param name="extDeadline">Deadline date e.g. from ticket, if not defined by owner (only for InterfaceClient)</param>
        /// <param name="content">Text for notification (e.g. email body)</param>
        /// <param name="report">Optional report to be sent as attachment</param>
        /// <param name="timeIntervalText">Optional resolved time interval text for placeholder replacement.</param>
        /// <returns>number of emails sent</returns>
        public async Task<int> SendNotificationIfDue(FwoNotification notification, FwoOwner? owner, DateTime? extDeadline, string? content = null, ReportBase? report = null, string timeIntervalText = "")
        {
            if (IsNotificationDue(owner, extDeadline, notification))
            {
                return await SendNotification(notification, owner, content, report, timeIntervalText, extDeadline);
            }
            return 0;
        }

        /// <summary>
        /// Set the last sent date for all notifications used so far
        /// </summary>
        /// <returns></returns>
        public async Task<int> UpdateNotificationsLastSent()
        {
            int updatedNotifications = await NotificationLastSentHelper.UpdateAsync(ApiConnection, CheckedNotificationIds);
            CheckedNotificationIds = [];
            return updatedNotifications;
        }

        /// <summary>
        /// Checks whether a notification is currently due for sending.
        /// </summary>
        /// <param name="owner">Owner context used for owner-based deadlines.</param>
        /// <param name="extDeadline">External deadline (e.g. request date, rule expiry date).</param>
        /// <param name="notification">Notification configuration to evaluate.</param>
        /// <returns>True if the notification should be sent now; otherwise false.</returns>
        public static bool IsNotificationDue(FwoOwner? owner, DateTime? extDeadline, FwoNotification notification)
        {
            return NotificationScheduleHelper.IsNotificationDue(owner, extDeadline, notification);
        }

        /// <summary>
        /// Loads notification definitions for the selected notification client.
        /// </summary>
        /// <param name="notificationClient">Notification client to load.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <returns>Notification definitions for the client.</returns>
        private static async Task<List<FwoNotification>> LoadNotifications(NotificationClient notificationClient, ApiConnection apiConnection)
        {
            return await apiConnection.SendQueryAsync<List<FwoNotification>>(NotificationQueries.getNotifications, new { client = notificationClient.ToString() });
        }

        /// <summary>
        /// Loads LDAP connections for recipient resolution.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <returns>Loaded LDAP connections or null when loading fails.</returns>
        private static async Task<List<Ldap>?> LoadLdapConnections(ApiConnection apiConnection)
        {
            try
            {
                return await apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections);
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Notifications", $"Could not load LDAP connections for recipient resolution. Continuing without owner-group fallback or workflow resolver: {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads owner groups from already available LDAP connections.
        /// </summary>
        /// <param name="connectedLdaps">Previously loaded LDAP connections.</param>
        /// <returns>Owner groups or an empty list when loading fails.</returns>
        private static async Task<List<UserGroup>> LoadOwnerGroups(List<Ldap>? connectedLdaps)
        {
            try
            {
                if (connectedLdaps == null)
                {
                    throw new InvalidOperationException("LDAP connections unavailable.");
                }

                return await MiddlewareServerServices.GetInternalGroups(connectedLdaps);
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Notifications", $"Could not load internal owner groups for recipient resolution. Continuing without owner-group fallback: {exception.Message}");
                return [];
            }
        }

        /// <summary>
        /// Creates the workflow recipient resolver from already available LDAP connections.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="connectedLdaps">Previously loaded LDAP connections.</param>
        /// <returns>A workflow recipient resolver or null when loading fails.</returns>
        private static WorkflowRecipientResolver? LoadWorkflowRecipientResolver(ApiConnection apiConnection, List<Ldap>? connectedLdaps)
        {
            try
            {
                if (connectedLdaps == null)
                {
                    throw new InvalidOperationException("LDAP connections unavailable.");
                }

                return new WorkflowRecipientResolver(apiConnection, connectedLdaps);
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Notifications", $"Could not load LDAP connections for workflow recipient resolution. Continuing without LDAP-backed recipient lookup: {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// Prepares and sends a single notification email when recipients are available.
        /// </summary>
        /// <param name="notification">Notification to send.</param>
        /// <param name="content">Email body content.</param>
        /// <param name="owner">Owner context used for placeholder replacement.</param>
        /// <param name="report">Optional report attachment.</param>
        /// <param name="timeIntervalText">Optional resolved time interval text.</param>
        /// <param name="resolvedDeadline">Resolved deadline timestamp used for notification logging.</param>
        /// <param name="placeholderValues">Optional caller-provided values for notification placeholder replacement.</param>
        /// <returns>The delivery outcome of the notification.</returns>
        private async Task<NotificationDeliveryResult> SendEmail(FwoNotification notification, string? content, FwoOwner? owner, ReportBase? report = null,
            string timeIntervalText = "", DateTime? resolvedDeadline = null,
            NotificationPlaceholderResolver.NotificationPlaceholderValues? placeholderValues = null)
        {
            MailData? mail = await PrepareEmail(notification, content, owner, report, timeIntervalText, placeholderValues);
            if (mail.To.Count == 0 && mail.Cc.Count == 0 && mail.Bcc.Count == 0)
            {
                Log.WriteWarning("Notifications",
                    $"No recipients resolved for notification client {notification.NotificationClient} while preparing notification {notification.Id}. Skipping send.");
                await LogNoRecipientFailureIfConfigured(notification, mail.Subject, resolvedDeadline);
                return NotificationDeliveryResult.NoRecipients;
            }

            int logId = 0;
            if (NotificationLoggingMode.ShouldLog(notification.Logging))
            {
                DateTimeOffset? deadline = resolvedDeadline.HasValue ? new DateTimeOffset(resolvedDeadline.Value) : null;
                logId = await NotificationLogHelper.InsertAsync(ApiConnection, notification, mail.To, mail.Cc, mail.Bcc, mail.Subject, deadline);
            }

            if (!NotificationLoggingMode.ShouldSend(notification.Logging))
            {
                await CompleteNotificationLog(logId, NotificationLogStatus.Suppressed);
                return NotificationDeliveryResult.Suppressed;
            }

            try
            {
                string decryptedSecret = AesEnc.TryDecrypt(GlobalConfig.EmailPassword, false, "NotificationService", "Could not decrypt mailserver password.");
                EmailConnection emailConnection = new(GlobalConfig.EmailServerAddress, GlobalConfig.EmailPort,
                    GlobalConfig.EmailTls, GlobalConfig.EmailUser, decryptedSecret, GlobalConfig.EmailSenderAddress);

                bool sent = await MailKitMailer.SendAsync(mail, emailConnection, notification.Layout == NotificationLayout.HtmlInBody, new());
                await CompleteNotificationLog(logId, sent ? NotificationLogStatus.Sent : NotificationLogStatus.Failed,
                    sent ? "" : "SMTP delivery failed.");
                return sent ? NotificationDeliveryResult.Delivered : NotificationDeliveryResult.Failed;
            }
            catch (Exception exception)
            {
                await CompleteNotificationLog(logId, NotificationLogStatus.Failed, exception.Message);
                throw;
            }
        }

        private async Task CompleteNotificationLog(int logId, NotificationLogStatus status, string error = "")
        {
            if (logId > 0)
            {
                await NotificationLogHelper.UpdateAsync(ApiConnection, logId, status, error);
            }
        }

        private async Task LogNoRecipientFailureIfConfigured(FwoNotification notification, string subject, DateTime? deadline)
        {
            if (!NotificationLoggingMode.ShouldLog(notification.Logging)
                || await HasMatchingNoRecipientFailure(notification, subject, deadline))
            {
                return;
            }

            DateTimeOffset? resolvedDeadline = deadline.HasValue ? new DateTimeOffset(deadline.Value) : null;
            int logId = await NotificationLogHelper.InsertAsync(ApiConnection, notification, [], [], [], subject, resolvedDeadline);
            await CompleteNotificationLog(logId, NotificationLogStatus.Failed, "No recipients resolved.");
        }

        private async Task<bool> HasMatchingNoRecipientFailure(FwoNotification notification, string subject, DateTime? deadline)
        {
            List<NotificationLogEntry> entries = await ApiConnection.SendQueryAsync<List<NotificationLogEntry>>(
                NotificationQueries.getNoRecipientNotificationLogs, new
                {
                    notificationId = notification.Id,
                    status = NotificationLogStatus.Failed.ToString(),
                    error = "No recipients resolved.",
                    subject
                });
            DateTimeOffset? resolvedDeadline = deadline.HasValue ? new DateTimeOffset(deadline.Value) : null;
            return entries.Any(entry => entry.Subject == subject
                && entry.DeadlineType == notification.Deadline
                && entry.Deadline == resolvedDeadline);
        }

        /// <summary>
        /// Sends a bundled notification email for a group of notifications.
        /// </summary>
        /// <param name="notifications">Grouped notifications to send together.</param>
        /// <param name="content">Text for notification email body.</param>
        /// <param name="owner">Owner context used for placeholder replacement.</param>
        /// <param name="report">Optional report attachment.</param>
        /// <param name="timeIntervalText">Optional resolved time interval text.</param>
        /// <returns>The delivery outcome of the bundled notification.</returns>
        private async Task<NotificationDeliveryResult> SendBundledEmail(List<FwoNotification> notifications, string? content, FwoOwner? owner, ReportBase? report = null, string timeIntervalText = "")
        {
            List<FwoNotification> sendableNotifications = [.. notifications.Where(notification => NotificationLoggingMode.ShouldSend(notification.Logging))];
            List<FwoNotification> suppressedNotifications = [.. notifications.Where(notification => !NotificationLoggingMode.ShouldSend(notification.Logging))];
            PreparedBundleMails preparedMails = await PrepareBundleMails(
                sendableNotifications, suppressedNotifications, content, owner, report, timeIntervalText);
            List<int> suppressedLogIds = await LogSuppressedBundle(suppressedNotifications, preparedMails.SuppressedMail,
                preparedMails.HasSuppressedRecipients);
            await CompleteNotificationLogs(suppressedLogIds, NotificationLogStatus.Suppressed);

            if (await HandleNoRecipientBundle(notifications, sendableNotifications, suppressedNotifications,
                preparedMails))
            {
                return NotificationDeliveryResult.NoRecipients;
            }

            if (sendableNotifications.Count == 0)
            {
                return NotificationDeliveryResult.Suppressed;
            }

            return await SendPreparedBundle(sendableNotifications, preparedMails.SendableMail!);
        }

        private async Task<PreparedBundleMails> PrepareBundleMails(
            List<FwoNotification> sendableNotifications, List<FwoNotification> suppressedNotifications,
            string? content, FwoOwner? owner, ReportBase? report, string timeIntervalText)
        {
            MailData? suppressedMail = suppressedNotifications.Count > 0
                ? await PrepareBundledEmail(suppressedNotifications, content, owner, report, timeIntervalText)
                : null;
            MailData? sendableMail = sendableNotifications.Count > 0
                ? await PrepareBundledEmail(sendableNotifications, content, owner, report, timeIntervalText)
                : null;
            return new PreparedBundleMails(suppressedMail, sendableMail);
        }

        private async Task<List<int>> LogSuppressedBundle(List<FwoNotification> notifications, MailData? mail, bool hasRecipients)
        {
            if (!hasRecipients)
            {
                return [];
            }

            foreach (FwoNotification notification in notifications)
            {
                AddCheckedNotificationId(notification.Id);
            }
            return await LogBundledNotifications(notifications, mail!);
        }

        private async Task<bool> HandleNoRecipientBundle(List<FwoNotification> allNotifications,
            List<FwoNotification> sendableNotifications, List<FwoNotification> suppressedNotifications,
            PreparedBundleMails preparedMails)
        {
            bool noSendableRecipients = sendableNotifications.Count > 0 && !preparedMails.HasSendableRecipients;
            bool noSuppressedRecipients = sendableNotifications.Count == 0 && !preparedMails.HasSuppressedRecipients;
            if (!noSendableRecipients && !noSuppressedRecipients)
            {
                return false;
            }

            if (noSendableRecipients)
            {
                await LogNoRecipientFailures(sendableNotifications, preparedMails.SendableMail!.Subject);
            }
            if (noSuppressedRecipients)
            {
                await LogNoRecipientFailures(suppressedNotifications, preparedMails.SuppressedMail!.Subject);
            }

            FwoNotification baseNotification = allNotifications.First();
            Log.WriteWarning("Notifications",
                $"No recipients resolved for notification client {baseNotification.NotificationClient} while preparing bundled notification {baseNotification.Id}. Skipping send.");
            return true;
        }

        private async Task LogNoRecipientFailures(List<FwoNotification> notifications, string subject)
        {
            foreach (FwoNotification notification in notifications)
            {
                await LogNoRecipientFailureIfConfigured(notification, subject, null);
            }
        }

        private async Task<NotificationDeliveryResult> SendPreparedBundle(List<FwoNotification> notifications, MailData mail)
        {
            List<int> sendableLogIds = await LogBundledNotifications(notifications, mail);

            try
            {
                string decryptedSecret = AesEnc.TryDecrypt(GlobalConfig.EmailPassword, false, "NotificationService", "Could not decrypt mailserver password.");
                EmailConnection emailConnection = new(GlobalConfig.EmailServerAddress, GlobalConfig.EmailPort,
                    GlobalConfig.EmailTls, GlobalConfig.EmailUser, decryptedSecret, GlobalConfig.EmailSenderAddress);

                bool sent = await MailKitMailer.SendAsync(mail, emailConnection, false, new());
                await CompleteNotificationLogs(sendableLogIds, sent ? NotificationLogStatus.Sent : NotificationLogStatus.Failed,
                    sent ? "" : "SMTP delivery failed.");
                return sent ? NotificationDeliveryResult.Delivered : NotificationDeliveryResult.Failed;
            }
            catch (Exception exception)
            {
                await CompleteNotificationLogs(sendableLogIds, NotificationLogStatus.Failed, exception.Message);
                throw;
            }
        }

        private sealed class PreparedBundleMails
        {
            public PreparedBundleMails(MailData? suppressedMail, MailData? sendableMail)
            {
                SuppressedMail = suppressedMail;
                SendableMail = sendableMail;
            }

            public MailData? SuppressedMail { get; }
            public MailData? SendableMail { get; }
            public bool HasSuppressedRecipients => SuppressedMail is { } mail && HasRecipients(mail);
            public bool HasSendableRecipients => SendableMail is { } mail && HasRecipients(mail);
        }

        private static bool HasRecipients(MailData mail)
        {
            return mail.To.Count > 0 || mail.Cc.Count > 0 || mail.Bcc.Count > 0;
        }

        private void AddCheckedNotificationId(int notificationId)
        {
            if (!CheckedNotificationIds.Contains(notificationId))
            {
                CheckedNotificationIds.Add(notificationId);
            }
        }

        private async Task<List<int>> LogBundledNotifications(List<FwoNotification> notifications, MailData mail)
        {
            List<int> logIds = [];
            foreach (FwoNotification notification in notifications.Where(notification => NotificationLoggingMode.ShouldLog(notification.Logging)))
            {
                int logId = await NotificationLogHelper.InsertAsync(ApiConnection, notification, mail.To, mail.Cc, mail.Bcc, mail.Subject);
                if (logId > 0)
                {
                    logIds.Add(logId);
                }
            }
            return logIds;
        }

        private async Task CompleteNotificationLogs(List<int> logIds, NotificationLogStatus status, string error = "")
        {
            foreach (int logId in logIds)
            {
                await NotificationLogHelper.UpdateAsync(ApiConnection, logId, status, error);
            }
        }

        private async Task<MailData> PrepareEmail(FwoNotification notification, string? content, FwoOwner? owner, ReportBase? report = null,
            string timeIntervalText = "", NotificationPlaceholderResolver.NotificationPlaceholderValues? placeholderValues = null)
        {
            string subject = NotificationPlaceholderResolver.ReplaceOwnerPlaceholders(notification.EmailSubject ?? "", owner, timeIntervalText);
            string body = NotificationPlaceholderResolver.ReplaceOwnerPlaceholders(NotificationEmailLayoutHelper.BuildBody(notification, content), owner, timeIntervalText);
            if (placeholderValues != null)
            {
                subject = NotificationPlaceholderResolver.ReplaceNotificationPlaceholders(subject, placeholderValues);
                body = NotificationPlaceholderResolver.ReplaceNotificationPlaceholders(body, placeholderValues,
                    renderHtmlLinks: notification.Layout == NotificationLayout.HtmlInBody);
            }
            FormFile? attachment = report != null ? await BuildAttachment(notification, report, subject) : null;
            EmailHelper? emailHelper = GlobalConfig.UseDummyEmailAddress ? null : await CreateEmailHelper();
            if (report != null && notification.Layout == NotificationLayout.HtmlInBody)
            {
                body += report.ExportToHtmlBody();
            }
            UiUser? requester = placeholderValues?.Requester;
            List<string> tos = emailHelper == null ? await CollectRecipients(notification, owner, requester) : await CollectRecipients(notification, owner, emailHelper, requester);
            List<string> bccs = emailHelper == null ? await CollectRecipients(notification, owner, requester, false, true) : await CollectRecipients(notification, owner, emailHelper, requester, false, true);
            List<string> ccs = emailHelper == null ? await CollectRecipients(notification, owner, requester, true) : await CollectRecipients(notification, owner, emailHelper, requester, true);
            MailData mailData = new(tos, subject)
            {
                Body = body,
                Bcc = bccs,
                Cc = ccs
            };
            if (attachment != null)
            {
                mailData.Attachments = new FormFileCollection() { attachment };
            }
            return mailData;
        }

        private async Task<MailData> PrepareBundledEmail(List<FwoNotification> notifications, string? content, FwoOwner? owner, ReportBase? report = null, string timeIntervalText = "")
        {
            FwoNotification baseNotification = notifications.First();
            MailData mailData = await PrepareEmail(baseNotification, content, owner, null, timeIntervalText);
            if (report == null || baseNotification.BundleType == null)
            {
                return mailData;
            }

            switch (baseNotification.BundleType)
            {
                case BundleType.Attachments:
                    FormFileCollection attachments = [];
                    foreach (FwoNotification notification in notifications)
                    {
                        FormFile? attachment = await BuildAttachment(notification, report, mailData.Subject);
                        if (attachment != null)
                        {
                            attachments.Add(attachment);
                        }
                    }

                    if (attachments.Count > 0)
                    {
                        mailData.Attachments = attachments;
                    }
                    break;
                default:
                    throw new NotSupportedException($"Bundle type {baseNotification.BundleType} is not supported.");
            }

            return mailData;
        }

        private static string GetBundleGroupKey(FwoNotification notification)
        {
            return notification.BundleType == null || string.IsNullOrWhiteSpace(notification.BundleId)
                ? $"single:{notification.Id}"
                : $"{notification.BundleType}:{notification.BundleId}";
        }

        private static async Task<FormFile?> BuildAttachment(FwoNotification notification, ReportBase report, string subject)
        {
            return await NotificationEmailLayoutHelper.BuildAttachment(notification.Layout, subject, report.ExportToHtml, report.ExportToJson, report.ExportToCsv,
                async html =>
                {
                    string? pdfData = await report.ToPdf(html);
                    if (string.IsNullOrWhiteSpace(pdfData))
                    {
                        throw new ProcessingFailedException("No Pdf generated.");
                    }
                    return pdfData;
                });
        }

        /// <summary>
        /// Creates and initializes the email helper used for recipient resolution.
        /// </summary>
        /// <returns>Initialized email helper.</returns>
        private async Task<EmailHelper> CreateEmailHelper()
        {
            EmailHelper emailHelper = new(ApiConnection, null, new(), DefaultInit.DoNothing, OwnerGroups, recipientResolver: WorkflowRecipientResolver);
            await emailHelper.Init();
            return emailHelper;
        }

        /// <summary>
        /// Collects recipients for the notification using the configured resolution path.
        /// </summary>
        /// <param name="notification">Notification to inspect.</param>
        /// <param name="owner">Owner context used for recipient resolution.</param>
        /// <param name="requester">Optional persisted requester used for requester recipient resolution.</param>
        /// <param name="cc">Whether to resolve the Cc recipient set.</param>
        /// <param name="bcc">Whether to resolve the Bcc recipient set.</param>
        /// <returns>Resolved email addresses.</returns>
        private async Task<List<string>> CollectRecipients(FwoNotification notification, FwoOwner? owner, UiUser? requester,
            bool cc = false, bool bcc = false)
        {
            if (GlobalConfig.UseDummyEmailAddress)
            {
                return [GlobalConfig.DummyEmailAddress];
            }
            EmailHelper emailHelper = await CreateEmailHelper();
            return await CollectRecipients(notification, owner, emailHelper, requester, cc, bcc);
        }

        /// <summary>
        /// Collects recipients for the notification using a preinitialized email helper.
        /// </summary>
        /// <param name="notification">Notification to inspect.</param>
        /// <param name="owner">Owner context used for recipient resolution.</param>
        /// <param name="emailHelper">Preinitialized email helper.</param>
        /// <param name="requester">Optional persisted requester used for requester recipient resolution.</param>
        /// <param name="cc">Whether to resolve the Cc recipient set.</param>
        /// <param name="bcc">Whether to resolve the Bcc recipient set.</param>
        /// <returns>Resolved email addresses.</returns>
        private static async Task<List<string>> CollectRecipients(FwoNotification notification, FwoOwner? owner, EmailHelper emailHelper,
            UiUser? requester, bool cc = false, bool bcc = false)
        {
            EmailRecipientOption recipientOption = notification.RecipientTo;
            string? addressList = notification.EmailAddressTo;
            if (bcc)
            {
                recipientOption = notification.RecipientBcc;
                addressList = notification.EmailAddressBcc;
            }
            else if (cc)
            {
                recipientOption = notification.RecipientCc;
                addressList = notification.EmailAddressCc;
            }

            List<string> addresses = EmailHelper.SplitAddresses(addressList);
            if (recipientOption == EmailRecipientOption.ConfiguredResponsibles)
            {
                List<string> recipients = await emailHelper.GetRecipients(addressList ?? "", owner, null, requester);
                if (recipients.Count == 0)
                {
                    Log.WriteWarning("Notifications", $"No recipients resolved for configured responsibles while preparing notification client {notification.NotificationClient}.");
                }
                return recipients;
            }
            if (recipientOption == EmailRecipientOption.OtherAddresses && LooksLikeRecipientSelectionJson(addressList))
            {
                List<string> recipients = await emailHelper.GetRecipients(addressList ?? "", null, null, requester);
                if (recipients.Count == 0)
                {
                    Log.WriteWarning("Notifications", $"No recipients resolved for other addresses while preparing notification client {notification.NotificationClient}.");
                }
                return recipients;
            }
            List<string> resolvedRecipients = await emailHelper.GetRecipients(recipientOption, null, owner,
                requester?.Dn, addresses, requester?.Email);
            if (resolvedRecipients.Count == 0 && recipientOption != EmailRecipientOption.None)
            {
                Log.WriteWarning("Notifications", $"No recipients resolved for notification client {notification.NotificationClient} using option {recipientOption}.");
            }
            return resolvedRecipients;
        }

        private static bool LooksLikeRecipientSelectionJson(string? recipientValue)
        {
            return recipientValue?.TrimStart().StartsWith('{') == true;
        }
    }
}
