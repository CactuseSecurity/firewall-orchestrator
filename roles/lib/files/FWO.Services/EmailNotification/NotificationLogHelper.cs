using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;

namespace FWO.Services
{
    /// <summary>
    /// Persists notification delivery data for the email log.
    /// </summary>
    public static class NotificationLogHelper
    {
        /// <summary>
        /// Inserts one notification delivery log entry.
        /// </summary>
        /// <param name="apiConnection">API connection used to persist the entry.</param>
        /// <param name="notification">Notification configuration being delivered.</param>
        /// <param name="tos">Resolved To recipients.</param>
        /// <param name="ccs">Resolved Cc recipients.</param>
        /// <param name="bccs">Resolved Bcc recipients.</param>
        /// <param name="subject">Rendered subject.</param>
        /// <param name="deadline">Resolved deadline timestamp.</param>
        public static async Task InsertAsync(ApiConnection apiConnection, FwoNotification notification,
            IEnumerable<string> tos, IEnumerable<string>? ccs, IEnumerable<string>? bccs, string subject,
            DateTimeOffset? deadline = null)
        {
            NotificationLogEntry entry = new()
            {
                Timestamp = DateTimeOffset.UtcNow,
                NotificationId = notification.Id,
                NotificationType = notification.NotificationClient.ToString(),
                To = string.Join(", ", tos),
                Cc = string.Join(", ", ccs ?? []),
                Bcc = string.Join(", ", bccs ?? []),
                Subject = subject,
                DeadlineType = notification.Deadline,
                Deadline = deadline
            };

            await apiConnection.SendQueryAsync<object>(NotificationQueries.insertNotificationLog,
                new { entries = new List<NotificationLogEntry> { entry } });
        }
    }
}
