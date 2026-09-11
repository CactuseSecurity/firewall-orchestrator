using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Logging;

namespace FWO.Services
{
    /// <summary>
    /// Updates the last-sent timestamp for notifications delivered by different send paths.
    /// </summary>
    public static class NotificationLastSentHelper
    {
        private const string LogMessageTitle = "Notifications";

        /// <summary>
        /// Updates last-sent timestamps for the supplied notification IDs.
        /// </summary>
        /// <param name="apiConnection">API connection used to persist the timestamps.</param>
        /// <param name="notificationIds">Notification IDs that were successfully processed.</param>
        /// <returns>The number of affected notification rows.</returns>
        public static async Task<int> UpdateAsync(ApiConnection apiConnection, IEnumerable<int> notificationIds)
        {
            List<int> distinctNotificationIds = [.. notificationIds.Where(id => id > 0).Distinct()];
            if (distinctNotificationIds.Count == 0)
            {
                return 0;
            }

            try
            {
                int affectedRows = (await apiConnection.SendQueryAsync<ReturnId>(NotificationQueries.updateNotificationsLastSent,
                    new { ids = distinctNotificationIds, lastSent = DateTime.Now })).AffectedRows;
                if (affectedRows != distinctNotificationIds.Count)
                {
                    Log.WriteWarning(LogMessageTitle,
                        $"Updated last_sent for {affectedRows} of {distinctNotificationIds.Count} notification(s).");
                }
                return affectedRows;
            }
            catch (Exception exception)
            {
                Log.WriteWarning(LogMessageTitle, $"Could not update last_sent for notification(s): {exception.Message}");
                return 0;
            }
        }
    }
}
