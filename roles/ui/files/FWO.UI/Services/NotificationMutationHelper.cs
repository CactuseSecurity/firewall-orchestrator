using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Creates shared GraphQL mutation payloads and persists notifications for UI-side notification editing.
    /// </summary>
    public static class NotificationMutationHelper
    {
        /// <summary>
        /// Persists a new notification and returns the saved copy with the assigned id.
        /// </summary>
        public static async Task<FwoNotification> AddAsync(ApiConnection apiConnection, NotificationClient client, FwoNotification notification)
        {
            object variables = BuildAddVariables(client, notification);
            ReturnIdWrapper response = await apiConnection.SendQueryAsync<ReturnIdWrapper>(NotificationQueries.addNotification, variables);
            return CreatePersistedNotification(client, notification, response.ReturnIds![0].NewId);
        }

        /// <summary>
        /// Persists changes to an existing notification and returns the updated copy.
        /// </summary>
        public static async Task<FwoNotification> UpdateAsync(ApiConnection apiConnection, NotificationClient client, FwoNotification notification)
        {
            object variables = BuildUpdateVariables(notification);
            await apiConnection.SendQueryAsync<ReturnIdWrapper>(NotificationQueries.updateNotification, variables);
            return CreatePersistedNotification(client, notification, notification.Id);
        }

        /// <summary>
        /// Deletes a persisted notification by id.
        /// </summary>
        public static Task DeleteAsync(ApiConnection apiConnection, int notificationId)
        {
            return apiConnection.SendQueryAsync<object>(NotificationQueries.deleteNotification, new { id = notificationId });
        }

        /// <summary>
        /// Builds GraphQL variables for inserting a notification.
        /// </summary>
        private static object BuildAddVariables(NotificationClient client, FwoNotification notification)
        {
            return new
            {
                client = client.ToString(),
                channel = notification.Channel.ToString(),
                name = notification.Name,
                recipientTo = notification.RecipientTo.ToString(),
                emailAddressTo = notification.EmailAddressTo,
                recipientCc = notification.RecipientCc.ToString(),
                emailAddressCc = notification.EmailAddressCc,
                subject = notification.EmailSubject,
                emailBody = notification.EmailBody,
                scheduleId = notification.ScheduleId,
                bundleType = notification.BundleType?.ToString(),
                bundleId = notification.BundleId,
                layout = notification.Layout.ToString(),
                deadline = notification.Deadline.ToString(),
                intervalBeforeDeadline = (int?)notification.IntervalBeforeDeadline,
                offsetBeforeDeadline = notification.OffsetBeforeDeadline,
                intervalAfterDeadline = (int?)notification.RepeatIntervalAfterDeadline,
                initialOffsetAfterDeadline = notification.InitialOffsetAfterDeadline,
                offsetAfterDeadline = notification.RepeatOffsetAfterDeadline,
                repetitionsAfterDeadline = notification.RepetitionsAfterDeadline
            };
        }

        /// <summary>
        /// Builds GraphQL variables for updating a notification.
        /// </summary>
        private static object BuildUpdateVariables(FwoNotification notification)
        {
            return new
            {
                id = notification.Id,
                channel = notification.Channel.ToString(),
                name = notification.Name,
                recipientTo = notification.RecipientTo.ToString(),
                emailAddressTo = notification.EmailAddressTo,
                recipientCc = notification.RecipientCc.ToString(),
                emailAddressCc = notification.EmailAddressCc,
                subject = notification.EmailSubject,
                emailBody = notification.EmailBody,
                scheduleId = notification.ScheduleId,
                bundleType = notification.BundleType?.ToString(),
                bundleId = notification.BundleId,
                layout = notification.Layout.ToString(),
                deadline = notification.Deadline.ToString(),
                intervalBeforeDeadline = (int?)notification.IntervalBeforeDeadline,
                offsetBeforeDeadline = notification.OffsetBeforeDeadline,
                intervalAfterDeadline = (int?)notification.RepeatIntervalAfterDeadline,
                initialOffsetAfterDeadline = notification.InitialOffsetAfterDeadline,
                offsetAfterDeadline = notification.RepeatOffsetAfterDeadline,
                repetitionsAfterDeadline = notification.RepetitionsAfterDeadline
            };
        }

        /// <summary>
        /// Creates a persisted notification copy with the assigned database id.
        /// </summary>
        private static FwoNotification CreatePersistedNotification(NotificationClient client, FwoNotification notification, int id)
        {
            FwoNotification persistedNotification = Clone(notification);
            persistedNotification.Id = id;
            persistedNotification.NotificationClient = client;
            return persistedNotification;
        }

        /// <summary>
        /// Copies all mutable notification values from source to target while keeping the existing instance.
        /// </summary>
        private static FwoNotification Clone(FwoNotification notification)
        {
            return new FwoNotification
            {
                Id = notification.Id,
                NotificationClient = notification.NotificationClient,
                Channel = notification.Channel,
                Name = notification.Name,
                RecipientTo = notification.RecipientTo,
                EmailAddressTo = notification.EmailAddressTo,
                RecipientCc = notification.RecipientCc,
                EmailAddressCc = notification.EmailAddressCc,
                EmailSubject = notification.EmailSubject,
                EmailBody = notification.EmailBody,
                ScheduleId = notification.ScheduleId,
                BundleType = notification.BundleType,
                BundleId = notification.BundleId,
                Layout = notification.Layout,
                Deadline = notification.Deadline,
                IntervalBeforeDeadline = notification.IntervalBeforeDeadline,
                OffsetBeforeDeadline = notification.OffsetBeforeDeadline,
                RepeatIntervalAfterDeadline = notification.RepeatIntervalAfterDeadline,
                InitialOffsetAfterDeadline = notification.InitialOffsetAfterDeadline,
                RepeatOffsetAfterDeadline = notification.RepeatOffsetAfterDeadline,
                RepetitionsAfterDeadline = notification.RepetitionsAfterDeadline,
                LastSent = notification.LastSent
            };
        }
    }
}
