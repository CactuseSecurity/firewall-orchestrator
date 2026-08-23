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
            Notifications = notifications;
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
            List<Ldap>? connectedLdaps = await LoadLdapConnections(apiConnection);
            List<UserGroup> ownerGroups = await LoadOwnerGroups(connectedLdaps);
            IWorkflowRecipientResolver? workflowRecipientResolver = LoadWorkflowRecipientResolver(apiConnection, connectedLdaps);
            return new NotificationService(await LoadNotifications(notificationClient, apiConnection), globalConfig, apiConnection, ownerGroups, workflowRecipientResolver);
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
        /// <returns>number of emails sent</returns>
        public async Task<int> SendNotification(FwoNotification notification, FwoOwner? owner, string? content = null, ReportBase? report = null, string timeIntervalText = "")
        {
            // Later: Handle other channels here when implemented
            bool sent = await SendEmail(notification, content, owner, report, timeIntervalText);
            if (!sent)
            {
                return 0;
            }
            if (!CheckedNotificationIds.Contains(notification.Id))
            {
                CheckedNotificationIds.Add(notification.Id);
            }
            return 1;
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
            foreach (IGrouping<string, FwoNotification> notificationGroup in notifications.GroupBy(GetBundleGroupKey))
            {
                List<FwoNotification> groupedNotifications = [.. notificationGroup];
                if (groupedNotifications.Count == 1 || groupedNotifications[0].BundleType == null)
                {
                    emailsSent += await SendNotification(groupedNotifications[0], owner, content, report, timeIntervalText);
                    continue;
                }

                bool sent = await SendBundledEmail(groupedNotifications, content, owner, report, timeIntervalText);
                if (!sent)
                {
                    continue;
                }
                foreach (FwoNotification notification in groupedNotifications)
                {
                    if (!CheckedNotificationIds.Contains(notification.Id))
                    {
                        CheckedNotificationIds.Add(notification.Id);
                    }
                }
                emailsSent++;
            }
            return emailsSent;
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
                return await SendNotification(notification, owner, content, report, timeIntervalText);
            }
            return 0;
        }

        /// <summary>
        /// Set the last sent date for all notifications used so far
        /// </summary>
        /// <returns></returns>
        public async Task<int> UpdateNotificationsLastSent()
        {
            int updatedNotifications = (await ApiConnection.SendQueryAsync<ReturnId>(NotificationQueries.updateNotificationsLastSent, new { ids = CheckedNotificationIds, lastSent = DateTime.Now })).AffectedRows;
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
            if (notification.Deadline == NotificationDeadline.None)
            {
                return true;
            }

            DateTime deadline = GetDeadlineDate(notification.Deadline, owner, extDeadline);

            return deadline >= DateTime.Now
                ? IsNotificationDueBeforeDeadline(deadline, notification)
                : IsNotificationDueAfterDeadline(deadline, notification);
        }

        private static bool IsTimeToSend(DateTime? lastSent, DateTime notifDate)
        {
            return (lastSent == null || ((DateTime)lastSent).Date < notifDate.Date) && notifDate.Date <= DateTime.Now.Date;
        }

        private static bool TryGetConfiguredInterval(SchedulerInterval? interval, string propertyName, out SchedulerInterval configuredInterval)
        {
            if (interval != null)
            {
                configuredInterval = (SchedulerInterval)interval;
                return true;
            }

            Log.WriteWarning("Notifications", $"Notification interval '{propertyName}' is not configured. Skipping due evaluation.");
            configuredInterval = default;
            return false;
        }

        private static bool IsNotificationDueBeforeDeadline(DateTime deadline, FwoNotification notification)
        {
            if (!TryGetConfiguredInterval(notification.IntervalBeforeDeadline, nameof(notification.IntervalBeforeDeadline), out SchedulerInterval intervalBeforeDeadline))
            {
                return false;
            }

            DateTime notifDate = ApplyIntervalOffset(deadline, intervalBeforeDeadline, -notification.OffsetBeforeDeadline ?? 0);
            return IsTimeToSend(notification.LastSent, notifDate);
        }

        private static bool IsNotificationDueAfterDeadline(DateTime deadline, FwoNotification notification)
        {
            if (!TryGetConfiguredInterval(notification.RepeatIntervalAfterDeadline, nameof(notification.RepeatIntervalAfterDeadline), out SchedulerInterval repeatIntervalAfterDeadline))
            {
                return false;
            }

            DateTime nextNotifDate = ApplyIntervalOffset(deadline.Date, repeatIntervalAfterDeadline, notification.InitialOffsetAfterDeadline ?? 0);
            DateTime currentNotifDate = nextNotifDate;
            int counter = -1;
            while (nextNotifDate <= DateTime.Now.Date && counter++ <= notification.RepetitionsAfterDeadline)
            {
                currentNotifDate = nextNotifDate;
                nextNotifDate = ApplyIntervalOffset(nextNotifDate, repeatIntervalAfterDeadline, notification.RepeatOffsetAfterDeadline ?? 0);
            }

            return counter <= notification.RepetitionsAfterDeadline && IsTimeToSend(notification.LastSent, currentNotifDate);
        }

        private static DateTime ApplyIntervalOffset(DateTime value, SchedulerInterval interval, int offset)
        {
            return interval switch
            {
                SchedulerInterval.Days => value.AddDays(offset),
                SchedulerInterval.Weeks => value.AddDays(offset * GlobalConst.kDaysPerWeek),
                SchedulerInterval.Months => value.AddMonths(offset),
                _ => throw new NotSupportedException("Time interval is not supported.")
            };
        }

        private static DateTime GetDeadlineDate(NotificationDeadline deadline, FwoOwner? owner, DateTime? extDeadline)
        {
            if (deadline == NotificationDeadline.RecertDate && owner?.NextRecertDate != null)
            {
                return (DateTime)owner.NextRecertDate;
            }
            else if (deadline == NotificationDeadline.RequestDate && extDeadline != null)
            {
                return (DateTime)extDeadline;
            }
            else if (deadline == NotificationDeadline.RuleExpiry && extDeadline != null)
            {
                return (DateTime)extDeadline;
            }
            else if (deadline == NotificationDeadline.DecommissionDate && owner?.DecommDate != null)
            {
                return (DateTime)owner.DecommDate;
            }
            return DateTime.Now;
        }

        private static async Task<List<FwoNotification>> LoadNotifications(NotificationClient notificationClient, ApiConnection apiConnection)
        {
            return await apiConnection.SendQueryAsync<List<FwoNotification>>(NotificationQueries.getNotifications, new { client = notificationClient.ToString() });
        }

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

        private async Task<bool> SendEmail(FwoNotification notification, string? content, FwoOwner? owner, ReportBase? report = null, string timeIntervalText = "")
        {
            MailData? mail = await PrepareEmail(notification, content, owner, report, timeIntervalText);
            if (mail.To.Count == 0 && mail.Cc.Count == 0 && mail.Bcc.Count == 0)
            {
                Log.WriteWarning("Notifications",
                    $"No recipients resolved for notification client {notification.NotificationClient} while preparing notification {notification.Id}. Skipping send.");
                return false;
            }

            string decryptedSecret = AesEnc.TryDecrypt(GlobalConfig.EmailPassword, false, "NotificationService", "Could not decrypt mailserver password.");
            EmailConnection emailConnection = new(GlobalConfig.EmailServerAddress, GlobalConfig.EmailPort,
                GlobalConfig.EmailTls, GlobalConfig.EmailUser, decryptedSecret, GlobalConfig.EmailSenderAddress);

            await MailKitMailer.SendAsync(mail, emailConnection, notification.Layout == NotificationLayout.HtmlInBody, new());
            return true;
        }

        private async Task<bool> SendBundledEmail(List<FwoNotification> notifications, string? content, FwoOwner? owner, ReportBase? report = null, string timeIntervalText = "")
        {
            MailData mail = await PrepareBundledEmail(notifications, content, owner, report, timeIntervalText);
            if (mail.To.Count == 0 && mail.Cc.Count == 0 && mail.Bcc.Count == 0)
            {
                FwoNotification baseNotification = notifications.First();
                Log.WriteWarning("Notifications",
                    $"No recipients resolved for notification client {baseNotification.NotificationClient} while preparing bundled notification {baseNotification.Id}. Skipping send.");
                return false;
            }

            string decryptedSecret = AesEnc.TryDecrypt(GlobalConfig.EmailPassword, false, "NotificationService", "Could not decrypt mailserver password.");
            EmailConnection emailConnection = new(GlobalConfig.EmailServerAddress, GlobalConfig.EmailPort,
                GlobalConfig.EmailTls, GlobalConfig.EmailUser, decryptedSecret, GlobalConfig.EmailSenderAddress);

            await MailKitMailer.SendAsync(mail, emailConnection, false, new());
            return true;
        }

        private async Task<MailData> PrepareEmail(FwoNotification notification, string? content, FwoOwner? owner, ReportBase? report = null, string timeIntervalText = "")
        {
            string subject = NotificationPlaceholderResolver.ReplaceOwnerPlaceholders(notification.EmailSubject ?? "", owner, timeIntervalText);
            string body = NotificationPlaceholderResolver.ReplaceOwnerPlaceholders(NotificationEmailLayoutHelper.BuildBody(notification, content), owner, timeIntervalText);
            FormFile? attachment = report != null ? await BuildAttachment(notification, report, subject) : null;
            EmailHelper? emailHelper = GlobalConfig.UseDummyEmailAddress ? null : await CreateEmailHelper();
            if (report != null && notification.Layout == NotificationLayout.HtmlInBody)
            {
                body += report.ExportToHtmlBody();
            }
            List<string> tos = emailHelper == null ? await CollectRecipients(notification, owner) : await CollectRecipients(notification, owner, emailHelper);
            List<string> bccs = emailHelper == null ? await CollectRecipients(notification, owner, false, true) : await CollectRecipients(notification, owner, emailHelper, false, true);
            List<string> ccs = emailHelper == null ? await CollectRecipients(notification, owner, true) : await CollectRecipients(notification, owner, emailHelper, true);
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

        private async Task<EmailHelper> CreateEmailHelper()
        {
            EmailHelper emailHelper = new(ApiConnection, null, new(), DefaultInit.DoNothing, OwnerGroups, recipientResolver: WorkflowRecipientResolver);
            await emailHelper.Init();
            return emailHelper;
        }

        private async Task<List<string>> CollectRecipients(FwoNotification notification, FwoOwner? owner, bool cc = false, bool bcc = false)
        {
            if (GlobalConfig.UseDummyEmailAddress)
            {
                return [GlobalConfig.DummyEmailAddress];
            }
            EmailHelper emailHelper = await CreateEmailHelper();
            return await CollectRecipients(notification, owner, emailHelper, cc, bcc);
        }

        private static async Task<List<string>> CollectRecipients(FwoNotification notification, FwoOwner? owner, EmailHelper emailHelper, bool cc = false, bool bcc = false)
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
                List<string> recipients = await emailHelper.GetRecipients(addressList ?? "", owner, null);
                if (recipients.Count == 0)
                {
                    Log.WriteWarning("Notifications", $"No recipients resolved for configured responsibles while preparing notification client {notification.NotificationClient}.");
                }
                return recipients;
            }
            if (recipientOption == EmailRecipientOption.OtherAddresses && LooksLikeRecipientSelectionJson(addressList))
            {
                List<string> recipients = await emailHelper.GetRecipients(addressList ?? "", null, null);
                if (recipients.Count == 0)
                {
                    Log.WriteWarning("Notifications", $"No recipients resolved for other addresses while preparing notification client {notification.NotificationClient}.");
                }
                return recipients;
            }
            List<string> resolvedRecipients = await emailHelper.GetRecipients(recipientOption, null, owner, null, addresses);
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
