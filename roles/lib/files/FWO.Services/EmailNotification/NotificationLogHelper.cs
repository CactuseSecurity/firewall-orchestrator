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
        public static async Task<int> InsertAsync(ApiConnection apiConnection, FwoNotification notification,
            IEnumerable<string> tos, IEnumerable<string>? ccs, IEnumerable<string>? bccs, string subject,
            DateTimeOffset? deadline = null)
        {
            NotificationLogInsertEntry entry = new()
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

            ReturnIdWrapper result = await apiConnection.SendQueryAsync<ReturnIdWrapper>(NotificationQueries.insertNotificationLog,
                new { entries = new List<NotificationLogInsertEntry> { entry } });
            return result.ReturnIds?.FirstOrDefault()?.Id is long id ? (int)id : 0;
        }

        public static async Task UpdateAsync(ApiConnection apiConnection, int logId, NotificationLogStatus status, string error = "")
        {
            await apiConnection.SendQueryAsync<ReturnId>(NotificationQueries.updateNotificationLog,
                new { id = logId, status = status.ToString(), error });
        }
    }
}
