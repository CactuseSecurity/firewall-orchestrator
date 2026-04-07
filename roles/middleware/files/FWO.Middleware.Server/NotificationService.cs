using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Config.Api;
using FWO.Mail;
using FWO.Encryption;
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


        NotificationService(List<FwoNotification> notifications, GlobalConfig globalConfig, ApiConnection apiConnection, List<UserGroup> ownerGroups)
        {
            ApiConnection = apiConnection;
            GlobalConfig = globalConfig;
            Notifications = notifications;
            OwnerGroups = ownerGroups;
        }

        /// <summary>
        /// async Constructor
        /// </summary>
        /// <param name="notificationClient"></param>
        /// <param name="globalConfig"></param>
        /// <param name="apiConnection"></param>
        /// <param name="ownerGroups"></param>
        /// <returns></returns>
        public static async Task<NotificationService> CreateAsync(NotificationClient notificationClient, GlobalConfig globalConfig, ApiConnection apiConnection, List<UserGroup> ownerGroups)
        {
            List<FwoNotification> notifications = await LoadNotifications(notificationClient, apiConnection);
            return new NotificationService(notifications, globalConfig, apiConnection, ownerGroups);
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
            await SendEmail(notification, content, owner, report, timeIntervalText);
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

                await SendBundledEmail(groupedNotifications, content, owner, report, timeIntervalText);
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
            if (deadline >= DateTime.Now)
            {
                var notifDate = notification.IntervalBeforeDeadline switch
                {
                    SchedulerInterval.Days => deadline.AddDays(-notification.OffsetBeforeDeadline ?? 0),
                    SchedulerInterval.Weeks => deadline.AddDays(-notification.OffsetBeforeDeadline * GlobalConst.kDaysPerWeek ?? 0),
                    SchedulerInterval.Months => deadline.AddMonths(-notification.OffsetBeforeDeadline ?? 0),
                    _ => throw new NotSupportedException("Time interval is not supported.")
                };
                return IsTimeToSend(notification.LastSent, notifDate);
            }
            else
            {
                var nextNotifDate = notification.RepeatIntervalAfterDeadline switch
                {
                    SchedulerInterval.Days => deadline.Date.AddDays(notification.InitialOffsetAfterDeadline ?? 0),
                    SchedulerInterval.Weeks => deadline.Date.AddDays(notification.InitialOffsetAfterDeadline * GlobalConst.kDaysPerWeek ?? 0),
                    SchedulerInterval.Months => deadline.Date.AddMonths(notification.InitialOffsetAfterDeadline ?? 0),
                    _ => throw new NotSupportedException("Time interval is not supported."),
                };
                var currentNotifDate = nextNotifDate;
                int counter = -1;
                while (nextNotifDate <= DateTime.Now.Date && counter++ <= notification.RepetitionsAfterDeadline)
                {
                    currentNotifDate = nextNotifDate;
                    nextNotifDate = notification.RepeatIntervalAfterDeadline switch
                    {
                        SchedulerInterval.Days => nextNotifDate.AddDays(notification.RepeatOffsetAfterDeadline ?? 0),
                        SchedulerInterval.Weeks => nextNotifDate.AddDays(notification.RepeatOffsetAfterDeadline * GlobalConst.kDaysPerWeek ?? 0),
                        SchedulerInterval.Months => nextNotifDate.AddMonths(notification.RepeatOffsetAfterDeadline ?? 0),
                        _ => throw new NotSupportedException("Time interval is not supported."),
                    };
                }
                return counter <= notification.RepetitionsAfterDeadline && IsTimeToSend(notification.LastSent, currentNotifDate);
            }
        }

        private static bool IsTimeToSend(DateTime? lastSent, DateTime notifDate)
        {
            return (lastSent == null || ((DateTime)lastSent).Date < notifDate.Date) && notifDate.Date <= DateTime.Now.Date;
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

        private async Task SendEmail(FwoNotification notification, string? content, FwoOwner? owner, ReportBase? report = null, string timeIntervalText = "")
        {
            string decryptedSecret = AesEnc.TryDecrypt(GlobalConfig.EmailPassword, false, "NotificationService", "Could not decrypt mailserver password.");
            EmailConnection emailConnection = new(GlobalConfig.EmailServerAddress, GlobalConfig.EmailPort,
                GlobalConfig.EmailTls, GlobalConfig.EmailUser, decryptedSecret, GlobalConfig.EmailSenderAddress);

            MailData? mail = await PrepareEmail(notification, content, owner, report, timeIntervalText);

            await MailKitMailer.SendAsync(mail, emailConnection, notification.Layout == NotificationLayout.HtmlInBody, new());
        }

        private async Task SendBundledEmail(List<FwoNotification> notifications, string? content, FwoOwner? owner, ReportBase? report = null, string timeIntervalText = "")
        {
            string decryptedSecret = AesEnc.TryDecrypt(GlobalConfig.EmailPassword, false, "NotificationService", "Could not decrypt mailserver password.");
            EmailConnection emailConnection = new(GlobalConfig.EmailServerAddress, GlobalConfig.EmailPort,
                GlobalConfig.EmailTls, GlobalConfig.EmailUser, decryptedSecret, GlobalConfig.EmailSenderAddress);

            MailData mail = await PrepareBundledEmail(notifications, content, owner, report, timeIntervalText);
            await MailKitMailer.SendAsync(mail, emailConnection, false, new());
        }

        private async Task<MailData> PrepareEmail(FwoNotification notification, string? content, FwoOwner? owner, ReportBase? report = null, string timeIntervalText = "")
        {
            string subject = notification.EmailSubject
                .Replace(Placeholder.APPNAME, owner?.Name ?? "")
                .Replace(Placeholder.APPID, owner?.ExtAppId ?? "")
                .Replace(Placeholder.TIME_INTERVAL, timeIntervalText);
            string body = string.IsNullOrEmpty(content) ? notification.EmailBody : content;
            body = body.Replace(Placeholder.TIME_INTERVAL, timeIntervalText);
            FormFile? attachment = report != null ? await BuildAttachment(notification, report, subject) : null;
            if (report != null && notification.Layout == NotificationLayout.HtmlInBody)
            {
                body += report.ExportToHtml();
            }
            MailData mailData = new(await CollectRecipients(notification, owner), subject) { Body = body, Cc = await CollectRecipients(notification, owner, true) };
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
            switch (notification.Layout)
            {
                case NotificationLayout.PdfAsAttachment:
                    string html = report.ExportToHtml();
                    string? pdfData = await report.ToPdf(html);
                    if (string.IsNullOrWhiteSpace(pdfData))
                    {
                        throw new ProcessingFailedException("No Pdf generated.");
                    }
                    return EmailHelper.CreateAttachment(pdfData, GlobalConst.kPdf, subject);
                case NotificationLayout.HtmlAsAttachment:
                    return EmailHelper.CreateAttachment(report.ExportToHtml(), GlobalConst.kHtml, subject);
                case NotificationLayout.JsonAsAttachment:
                    return EmailHelper.CreateAttachment(report.ExportToJson(), GlobalConst.kJson, subject);
                case NotificationLayout.CsvAsAttachment:
                    return EmailHelper.CreateAttachment(report.ExportToCsv(), GlobalConst.kCsv, subject);
                default:
                    return null;
            }
        }

        private async Task<List<string>> CollectRecipients(FwoNotification notification, FwoOwner? owner, bool cc = false)
        {
            if (GlobalConfig.UseDummyEmailAddress)
            {
                return [GlobalConfig.DummyEmailAddress];
            }
            EmailHelper emailHelper = new(ApiConnection, null, new(), DefaultInit.DoNothing, OwnerGroups);
            await emailHelper.Init();
            return await emailHelper.GetRecipients(cc ? notification.RecipientCc : notification.RecipientTo, null, owner, null,
                EmailHelper.SplitAddresses(cc ? notification.EmailAddressCc : notification.EmailAddressTo));
        }
    }
}
