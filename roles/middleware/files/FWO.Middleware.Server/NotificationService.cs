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
        public async Task<int> SendNotificationsIfDue(FwoOwner owner, DateTime? extDeadline, string content, ReportBase? report = null, string timeIntervalText = "")
        {
            int emailsSent = 0;
            foreach (var notification in Notifications.Where(n => n.OwnerId == null || n.OwnerId == owner.Id))
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
        public async Task<int> SendNotification(FwoNotification notification, FwoOwner owner, string content, ReportBase? report = null, string timeIntervalText = "")
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
        /// Analyse and send single Notification if due
        /// </summary>
        /// <param name="notification">Notification to be handled</param>
        /// <param name="owner">Owner for whom the notification is done</param>
        /// <param name="extDeadline">Deadline date e.g. from ticket, if not defined by owner (only for InterfaceClient)</param>
        /// <param name="content">Text for notification (e.g. email body)</param>
        /// <param name="report">Optional report to be sent as attachment</param>
        /// <param name="timeIntervalText">Optional resolved time interval text for placeholder replacement.</param>
        /// <returns>number of emails sent</returns>
        public async Task<int> SendNotificationIfDue(FwoNotification notification, FwoOwner owner, DateTime? extDeadline, string content, ReportBase? report = null, string timeIntervalText = "")
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
        public static bool IsNotificationDue(FwoOwner owner, DateTime? extDeadline, FwoNotification notification)
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

        private static DateTime GetDeadlineDate(NotificationDeadline deadline, FwoOwner owner, DateTime? extDeadline)
        {
            if (deadline == NotificationDeadline.RecertDate && owner.NextRecertDate != null)
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
            return DateTime.Now;
        }

        private static async Task<List<FwoNotification>> LoadNotifications(NotificationClient notificationClient, ApiConnection apiConnection)
        {
            return await apiConnection.SendQueryAsync<List<FwoNotification>>(NotificationQueries.getNotifications, new { client = notificationClient.ToString() });
        }

        private async Task SendEmail(FwoNotification notification, string content, FwoOwner owner, ReportBase? report = null, string timeIntervalText = "")
        {
            string decryptedSecret = AesEnc.TryDecrypt(GlobalConfig.EmailPassword, false, "NotificationService", "Could not decrypt mailserver password.");
            EmailConnection emailConnection = new(GlobalConfig.EmailServerAddress, GlobalConfig.EmailPort,
                GlobalConfig.EmailTls, GlobalConfig.EmailUser, decryptedSecret, GlobalConfig.EmailSenderAddress);

            MailData? mail = await PrepareEmail(notification, content, owner, report, timeIntervalText);

            await MailKitMailer.SendAsync(mail, emailConnection, notification.Layout == NotificationLayout.HtmlInBody, new());
        }

        private async Task<MailData> PrepareEmail(FwoNotification notification, string content, FwoOwner owner, ReportBase? report = null, string timeIntervalText = "")
        {
            string subject = notification.EmailSubject
                .Replace(Placeholder.APPNAME, owner.Name)
                .Replace(Placeholder.APPID, owner.ExtAppId)
                .Replace(Placeholder.TIME_INTERVAL, timeIntervalText);
            string body = content.Replace(Placeholder.TIME_INTERVAL, timeIntervalText);
            FormFile? attachment = null;
            if (report != null)
            {
                switch (notification.Layout)
                {
                    case NotificationLayout.HtmlInBody:
                        body += report.ExportToHtml();
                        break;
                    case NotificationLayout.PdfAsAttachment:
                        string html = report.ExportToHtml();
                        string? pdfData = await report.ToPdf(html);

                        if (string.IsNullOrWhiteSpace(pdfData))
                            throw new ProcessingFailedException("No Pdf generated.");

                        attachment = EmailHelper.CreateAttachment(pdfData, GlobalConst.kPdf, subject);
                        break;
                    case NotificationLayout.HtmlAsAttachment:
                        attachment = EmailHelper.CreateAttachment(report.ExportToHtml(), GlobalConst.kHtml, subject);
                        break;
                    case NotificationLayout.JsonAsAttachment:
                        attachment = EmailHelper.CreateAttachment(report.ExportToJson(), GlobalConst.kJson, subject);
                        break;
                    case NotificationLayout.CsvAsAttachment:
                        attachment = EmailHelper.CreateAttachment(report.ExportToCsv(), GlobalConst.kCsv, subject);
                        break;
                    default:
                        break;
                }
            }
            MailData mailData = new(await CollectRecipients(notification, owner), subject) { Body = body, Cc = await CollectRecipients(notification, owner, true) };
            if (attachment != null)
            {
                mailData.Attachments = new FormFileCollection() { attachment };
            }
            return mailData;
        }

        private async Task<List<string>> CollectRecipients(FwoNotification notification, FwoOwner owner, bool cc = false)
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
