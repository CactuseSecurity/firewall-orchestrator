using FWO.Api.Client;
using FWO.Api.Client.Queries;
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
                await apiConnection.SendQueryAsync<object>(NotificationQueries.deleteNotification, new { id = notification.Id });
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
            string notificationName = $"{reportSchedule.Name} {notificationSettings.FormatName.ToUpperInvariant()}";
            NotificationLayout layout = ToNotificationLayout(notificationSettings.FormatName);
            var variables = new
            {
                client = NotificationClient.Report.ToString(),
                channel = NotificationChannel.Email.ToString(),
                name = notificationName,
                recipientTo = EmailRecipientOption.OtherAddresses.ToString(),
                emailAddressTo = notificationSettings.EmailRecipients,
                recipientCc = EmailRecipientOption.None.ToString(),
                emailAddressCc = "",
                subject = notificationSettings.EmailSubject,
                emailBody = notificationSettings.EmailBody,
                scheduleId = reportSchedule.Id,
                bundleType = notificationSettings.BundleType?.ToString(),
                bundleId = notificationSettings.BundleId,
                layout = layout.ToString(),
                deadline = NotificationDeadline.None.ToString(),
                intervalBeforeDeadline = (int?)null,
                offsetBeforeDeadline = (int?)null,
                intervalAfterDeadline = (int?)null,
                initialOffsetAfterDeadline = (int?)null,
                offsetAfterDeadline = (int?)null,
                repetitionsAfterDeadline = (int?)null
            };

            ReturnIdWrapper response = await apiConnection.SendQueryAsync<ReturnIdWrapper>(NotificationQueries.addNotification, variables);
            return new FwoNotification
            {
                Id = response.ReturnIds![0].NewId,
                NotificationClient = NotificationClient.Report,
                Channel = NotificationChannel.Email,
                Name = notificationName,
                RecipientTo = EmailRecipientOption.OtherAddresses,
                EmailAddressTo = notificationSettings.EmailRecipients,
                RecipientCc = EmailRecipientOption.None,
                EmailAddressCc = "",
                EmailSubject = notificationSettings.EmailSubject,
                EmailBody = notificationSettings.EmailBody,
                ScheduleId = reportSchedule.Id,
                BundleType = notificationSettings.BundleType,
                BundleId = notificationSettings.BundleId,
                Layout = layout,
                Deadline = NotificationDeadline.None
            };
        }

        private static async Task<FwoNotification> UpdateNotification(ApiConnection apiConnection, ReportSchedule reportSchedule,
            FwoNotification notification, NotificationSettings notificationSettings)
        {
            string notificationName = $"{reportSchedule.Name} {notificationSettings.FormatName.ToUpperInvariant()}";
            NotificationLayout layout = ToNotificationLayout(notificationSettings.FormatName);
            var variables = new
            {
                id = notification.Id,
                channel = NotificationChannel.Email.ToString(),
                name = notificationName,
                recipientTo = EmailRecipientOption.OtherAddresses.ToString(),
                emailAddressTo = notificationSettings.EmailRecipients,
                recipientCc = EmailRecipientOption.None.ToString(),
                emailAddressCc = "",
                subject = notificationSettings.EmailSubject,
                emailBody = notificationSettings.EmailBody,
                scheduleId = reportSchedule.Id,
                bundleType = notificationSettings.BundleType?.ToString(),
                bundleId = notificationSettings.BundleId,
                layout = layout.ToString(),
                deadline = NotificationDeadline.None.ToString(),
                intervalBeforeDeadline = (int?)null,
                offsetBeforeDeadline = (int?)null,
                intervalAfterDeadline = (int?)null,
                initialOffsetAfterDeadline = (int?)null,
                offsetAfterDeadline = (int?)null,
                repetitionsAfterDeadline = (int?)null
            };

            await apiConnection.SendQueryAsync<ReturnIdWrapper>(NotificationQueries.updateNotification, variables);
            notification.Channel = NotificationChannel.Email;
            notification.Name = notificationName;
            notification.RecipientTo = EmailRecipientOption.OtherAddresses;
            notification.EmailAddressTo = notificationSettings.EmailRecipients;
            notification.RecipientCc = EmailRecipientOption.None;
            notification.EmailAddressCc = "";
            notification.EmailSubject = notificationSettings.EmailSubject;
            notification.EmailBody = notificationSettings.EmailBody;
            notification.ScheduleId = reportSchedule.Id;
            notification.BundleType = notificationSettings.BundleType;
            notification.BundleId = notificationSettings.BundleId;
            notification.Layout = layout;
            notification.Deadline = NotificationDeadline.None;
            return notification;
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
