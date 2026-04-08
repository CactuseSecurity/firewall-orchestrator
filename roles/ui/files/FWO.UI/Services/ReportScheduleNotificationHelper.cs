using FWO.Api.Client;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Ui.Services
{
    public static class ReportScheduleNotificationHelper
    {
        private sealed class NotificationSettings
        {
            internal string FormatName { get; init; } = "";
            internal string EmailRecipients { get; init; } = "";
            internal string EmailSubject { get; init; } = "";
            internal string EmailBody { get; init; } = "";
            internal BundleType? BundleType { get; init; }
            internal string? BundleId { get; init; }
        }

        public static async Task<List<FwoNotification>> SyncNotifications(ApiConnection apiConnection, ReportSchedule reportSchedule, bool toEmail,
            string emailRecipients, string emailSubject, string emailBody)
        {
            if (!toEmail)
            {
                await DeleteNotifications(apiConnection, reportSchedule.Notifications);
                return [];
            }

            if (reportSchedule.Notifications.Count == 0)
            {
                return await CreateNotifications(apiConnection, reportSchedule, emailRecipients, emailSubject, emailBody);
            }

            return await UpdateNotifications(apiConnection, reportSchedule, emailRecipients, emailSubject, emailBody);
        }

        public static async Task<List<FwoNotification>> CreateNotifications(ApiConnection apiConnection, ReportSchedule reportSchedule,
            string emailRecipients, string emailSubject, string emailBody)
        {
            (BundleType? bundleType, string? bundleId) = GetBundleSettings(reportSchedule);
            List<FwoNotification> createdNotifications = [];
            foreach (FileFormat format in reportSchedule.OutputFormat)
            {
                NotificationSettings notificationSettings = NewNotificationSettings(format.Name, emailRecipients, emailSubject, emailBody, bundleType, bundleId);
                createdNotifications.Add(await AddNotification(apiConnection, reportSchedule, notificationSettings));
            }

            return createdNotifications;
        }

        public static async Task DeleteNotifications(ApiConnection apiConnection, List<FwoNotification>? notifications)
        {
            if (notifications == null)
            {
                return;
            }

            foreach (FwoNotification notification in notifications.Where(notification => notification.Id > 0))
            {
                await NotificationMutationHelper.DeleteAsync(apiConnection, notification.Id);
            }
        }

        private static async Task<List<FwoNotification>> UpdateNotifications(ApiConnection apiConnection, ReportSchedule reportSchedule,
            string emailRecipients, string emailSubject, string emailBody)
        {
            (BundleType? bundleType, string? bundleId) = GetBundleSettings(reportSchedule);
            List<FwoNotification> remainingNotifications = [.. reportSchedule.Notifications];
            List<FwoNotification> updatedNotifications = [];

            foreach (FileFormat format in reportSchedule.OutputFormat)
            {
                NotificationSettings notificationSettings = NewNotificationSettings(format.Name, emailRecipients, emailSubject, emailBody, bundleType, bundleId);
                NotificationLayout layout = ToNotificationLayout(notificationSettings.FormatName);
                FwoNotification? existingNotification = remainingNotifications.FirstOrDefault(notification => notification.Layout == layout);
                if (existingNotification != null)
                {
                    updatedNotifications.Add(await UpdateNotification(apiConnection, reportSchedule, existingNotification, notificationSettings));
                    remainingNotifications.Remove(existingNotification);
                }
                else
                {
                    updatedNotifications.Add(await AddNotification(apiConnection, reportSchedule, notificationSettings));
                }
            }

            await DeleteNotifications(apiConnection, remainingNotifications);
            return updatedNotifications;
        }

        private static async Task<FwoNotification> AddNotification(ApiConnection apiConnection, ReportSchedule reportSchedule,
            NotificationSettings notificationSettings)
        {
            FwoNotification notification = BuildNotification(reportSchedule, notificationSettings);
            return await NotificationMutationHelper.AddAsync(apiConnection, NotificationClient.Report, notification);
        }

        private static async Task<FwoNotification> UpdateNotification(ApiConnection apiConnection, ReportSchedule reportSchedule,
            FwoNotification notification, NotificationSettings notificationSettings)
        {
            FwoNotification updatedNotification = BuildNotification(reportSchedule, notificationSettings);
            updatedNotification.Id = notification.Id;
            return await NotificationMutationHelper.UpdateAsync(apiConnection, NotificationClient.Report, updatedNotification);
        }

        private static FwoNotification BuildNotification(ReportSchedule reportSchedule, NotificationSettings notificationSettings)
        {
            return new FwoNotification
            {
                NotificationClient = NotificationClient.Report,
                Channel = NotificationChannel.Email,
                Name = $"{reportSchedule.Name} {notificationSettings.FormatName.ToUpperInvariant()}",
                RecipientTo = EmailRecipientOption.OtherAddresses,
                EmailAddressTo = notificationSettings.EmailRecipients,
                RecipientCc = EmailRecipientOption.None,
                EmailAddressCc = "",
                EmailSubject = notificationSettings.EmailSubject,
                EmailBody = notificationSettings.EmailBody,
                ScheduleId = reportSchedule.Id,
                BundleType = notificationSettings.BundleType,
                BundleId = notificationSettings.BundleId,
                Layout = ToNotificationLayout(notificationSettings.FormatName),
                Deadline = NotificationDeadline.None
            };
        }

        private static NotificationSettings NewNotificationSettings(string formatName, string emailRecipients, string emailSubject,
            string emailBody, BundleType? bundleType, string? bundleId)
        {
            return new NotificationSettings
            {
                FormatName = formatName,
                EmailRecipients = emailRecipients,
                EmailSubject = emailSubject,
                EmailBody = emailBody,
                BundleType = bundleType,
                BundleId = bundleId
            };
        }

        private static (BundleType? BundleType, string? BundleId) GetBundleSettings(ReportSchedule reportSchedule)
        {
            if (reportSchedule.OutputFormat.Count <= 1)
            {
                return (null, null);
            }

            string? existingBundleId = reportSchedule.Notifications
                .Select(notification => notification.BundleId)
                .FirstOrDefault(bundleId => !string.IsNullOrWhiteSpace(bundleId));

            return (BundleType.Attachments, existingBundleId ?? Guid.NewGuid().ToString());
        }

        private static NotificationLayout ToNotificationLayout(string formatName)
        {
            return formatName switch
            {
                GlobalConst.kHtml => NotificationLayout.HtmlAsAttachment,
                GlobalConst.kPdf => NotificationLayout.PdfAsAttachment,
                GlobalConst.kJson => NotificationLayout.JsonAsAttachment,
                GlobalConst.kCsv => NotificationLayout.CsvAsAttachment,
                _ => throw new NotSupportedException("Output format is not supported.")
            };
        }
    }
}
