using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Mail;
using FWO.Encryption;
using FWO.Report;
using FWO.Services;
using System.Text.RegularExpressions;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class to administrate and send Notifications
    /// </summary>
    public class NotificationService
    {
        private readonly ApiConnection ApiConnection;
        private readonly GlobalConfig GlobalConfig;
        private readonly List<FwoNotification> Notifications;
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
        /// Analyse and Send Notifications if due
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="content"></param>
        /// <param name="report"></param>
        /// <returns></returns>
        public async Task<int> SendNotifications(FwoOwner owner, string content, ReportBase? report = null)
        {
            int emailsSent = 0;
            foreach (var notification in Notifications.Where(n => (n.OwnerId == null || n.OwnerId == owner.Id) && IsTimeToSend(owner, n)))
            {
                // Later: Handle other channels here when implemented
                await SendEmail(notification, content, owner, report);
                await UpdateNotificationLastSent(notification, ApiConnection);
                emailsSent++;
            }
            return emailsSent;
        }

        private static bool IsTimeToSend(FwoOwner owner, FwoNotification notification)
        {
            if (notification.Deadline == NotificationDeadline.None)
            {
                return true;
            }
            DateTime deadline = GetDeadlineDate(notification.Deadline, owner);
            if (deadline >= DateTime.Now)
            {
                var notifDate = notification.IntervalBeforeDeadline switch
                {
                    SchedulerInterval.Days => deadline.AddDays(-notification.OffsetBeforeDeadline ?? 0),
                    SchedulerInterval.Weeks => deadline.AddDays(-notification.OffsetBeforeDeadline * GlobalConst.kDaysPerWeek ?? 0),
                    SchedulerInterval.Months => deadline.AddMonths(-notification.OffsetBeforeDeadline ?? 0),
                    _ => throw new NotSupportedException("Time interval is not supported.")
                };
                return (notification.LastSent == null || ((DateTime)notification.LastSent).Date < notifDate.Date) &&
                    notifDate.Date <= DateTime.Now.Date;
            }
            else
            {
                var nextNotifDate = deadline.Date;
                int counter = 0;
                while (nextNotifDate < DateTime.Now.Date && counter++ <= notification.RepetitionsAfterDeadline)
                {
                    nextNotifDate = notification.RepeatIntervalAfterDeadline switch
                    {
                        SchedulerInterval.Days => nextNotifDate.AddDays(notification.RepeatOffsetAfterDeadline ?? 0),
                        SchedulerInterval.Weeks => nextNotifDate.AddDays(notification.RepeatOffsetAfterDeadline * GlobalConst.kDaysPerWeek ?? 0),
                        SchedulerInterval.Months => nextNotifDate.AddMonths(notification.RepeatOffsetAfterDeadline ?? 0),
                        _ => throw new NotSupportedException("Time interval is not supported."),
                    };
                }
                return counter < notification.RepetitionsAfterDeadline &&
                    (notification.LastSent == null || ((DateTime)notification.LastSent).Date < nextNotifDate.Date) &&
                    nextNotifDate.Date <= DateTime.Now.Date;
            }
        }

        private static DateTime GetDeadlineDate(NotificationDeadline deadline, FwoOwner owner)
        {
            if (deadline == NotificationDeadline.RecertDate && owner.NextRecertDate != null)
            {
                return (DateTime)owner.NextRecertDate;
            }
            return DateTime.Now;
        }

        private static async Task<List<FwoNotification>> LoadNotifications(NotificationClient notificationClient, ApiConnection apiConnection)
        {
            return await apiConnection.SendQueryAsync<List<FwoNotification>>(NotificationQueries.getNotifications, new { client = notificationClient.ToString() });
        }

        private static async Task UpdateNotificationLastSent(FwoNotification notification, ApiConnection apiConnection)
        {
            await apiConnection.SendQueryAsync<ReturnId>(NotificationQueries.updateNotificationLastSent, new { id = notification.Id, lastSent = DateTime.Now });
        }

        private async Task SendEmail(FwoNotification notification, string content, FwoOwner owner, ReportBase? report = null)
        {
            string decryptedSecret = AesEnc.TryDecrypt(GlobalConfig.EmailPassword, false, "NotificationService", "Could not decrypt mailserver password.");
            EmailConnection emailConnection = new(GlobalConfig.EmailServerAddress, GlobalConfig.EmailPort,
                GlobalConfig.EmailTls, GlobalConfig.EmailUser, decryptedSecret, GlobalConfig.EmailSenderAddress);

            MailData? mail = await PrepareEmail(notification, content, owner, report);

            await MailKitMailer.SendAsync(mail, emailConnection, notification.Layout == NotificationLayout.HtmlInBody, new());
        }

        private async Task<MailData> PrepareEmail(FwoNotification notification, string content, FwoOwner owner, ReportBase? report = null)
        {
            string subject = notification.EmailSubject;
            string body = content;
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

                        attachment = CreateAttachment(pdfData, GlobalConst.kPdf, subject);
                        break;
                    case NotificationLayout.HtmlAsAttachment:
                        attachment = CreateAttachment(report.ExportToHtml(), GlobalConst.kHtml, subject);
                        break;
                    case NotificationLayout.JsonAsAttachment:
                        attachment = CreateAttachment(report.ExportToJson(), GlobalConst.kJson, subject);
                        break;
                    case NotificationLayout.CsvAsAttachment:
                        attachment = CreateAttachment(report.ExportToCsv(), GlobalConst.kCsv, subject);
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

        private static FormFile? CreateAttachment(string? content, string fileFormat, string subject)
        {
            if (content != null)
            {
                string fileName = ConstructFileName(subject, fileFormat);

                MemoryStream memoryStream;
                string contentType;

                if (fileFormat == GlobalConst.kPdf)
                {
                    memoryStream = new(Convert.FromBase64String(content));
                    contentType = "application/octet-stream";
                }
                else
                {
                    memoryStream = new(System.Text.Encoding.UTF8.GetBytes(content));
                    contentType = $"application/{fileFormat}";
                }

                return new(memoryStream, 0, memoryStream.Length, "FWO-Report-Attachment", fileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = contentType
                };
            }
            return null;
        }

        private static string ConstructFileName(string input, string fileFormat)
        {
            try
            {
                Regex regex = new(@"\s", RegexOptions.None, TimeSpan.FromMilliseconds(500));
                return $"{regex.Replace(input, "")}_{DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssK")}.{fileFormat}";
            }
            catch (RegexMatchTimeoutException)
            {
                Log.WriteWarning("Construct File Name", "Timeout when constructing file name. Taking input.");
                return input;
            }
        }

        private async Task<List<string>> CollectRecipients(FwoNotification notification, FwoOwner owner, bool cc = false)
        {
            if (GlobalConfig.UseDummyEmailAddress)
            {
                return [GlobalConfig.DummyEmailAddress];
            }
            EmailHelper emailHelper = new(ApiConnection, null, new(), DefaultInit.DoNothing, OwnerGroups);
            await emailHelper.Init();
            return emailHelper.GetRecipients(cc ? notification.RecipientCc : notification.RecipientTo, null, owner, null,
                EmailHelper.SplitAddresses(cc ? notification.EmailAddressCc : notification.EmailAddressTo));
        }
    }
}
