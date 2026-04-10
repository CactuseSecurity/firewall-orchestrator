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
            NotificationMutationVariables variables = NotificationMutationVariables.Create(notification);
            return new
            {
                client = client.ToString(),
                userId = variables.userId,
                channel = variables.channel,
                name = variables.name,
                recipientTo = variables.recipientTo,
                emailAddressTo = variables.emailAddressTo,
                recipientCc = variables.recipientCc,
                emailAddressCc = variables.emailAddressCc,
                subject = variables.subject,
                emailBody = variables.emailBody,
                scheduleId = variables.scheduleId,
                bundleType = variables.bundleType,
                bundleId = variables.bundleId,
                layout = variables.layout,
                deadline = variables.deadline,
                intervalBeforeDeadline = variables.intervalBeforeDeadline,
                offsetBeforeDeadline = variables.offsetBeforeDeadline,
                intervalAfterDeadline = variables.intervalAfterDeadline,
                initialOffsetAfterDeadline = variables.initialOffsetAfterDeadline,
                offsetAfterDeadline = variables.offsetAfterDeadline,
                repetitionsAfterDeadline = variables.repetitionsAfterDeadline
            };
        }

        /// <summary>
        /// Builds GraphQL variables for updating a notification.
        /// </summary>
        private static object BuildUpdateVariables(FwoNotification notification)
        {
            NotificationMutationVariables variables = NotificationMutationVariables.Create(notification);
            return new
            {
                id = notification.Id,
                userId = variables.userId,
                channel = variables.channel,
                name = variables.name,
                recipientTo = variables.recipientTo,
                emailAddressTo = variables.emailAddressTo,
                recipientCc = variables.recipientCc,
                emailAddressCc = variables.emailAddressCc,
                subject = variables.subject,
                emailBody = variables.emailBody,
                scheduleId = variables.scheduleId,
                bundleType = variables.bundleType,
                bundleId = variables.bundleId,
                layout = variables.layout,
                deadline = variables.deadline,
                intervalBeforeDeadline = variables.intervalBeforeDeadline,
                offsetBeforeDeadline = variables.offsetBeforeDeadline,
                intervalAfterDeadline = variables.intervalAfterDeadline,
                initialOffsetAfterDeadline = variables.initialOffsetAfterDeadline,
                offsetAfterDeadline = variables.offsetAfterDeadline,
                repetitionsAfterDeadline = variables.repetitionsAfterDeadline
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
                UserId = notification.UserId,
                OwnerId = notification.OwnerId,
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

        /// <summary>
        /// Captures the GraphQL mutation variables shared by add and update notification requests.
        /// </summary>
        private sealed record NotificationMutationVariables
        {
            public int? userId { get; init; }

            public string channel { get; init; } = "";

            public string? name { get; init; }

            public string? recipientTo { get; init; }

            public string? emailAddressTo { get; init; }

            public string? recipientCc { get; init; }

            public string? emailAddressCc { get; init; }

            public string? subject { get; init; }

            public string? emailBody { get; init; }

            public int? scheduleId { get; init; }

            public string? bundleType { get; init; }

            public string? bundleId { get; init; }

            public string layout { get; init; } = "";

            public string? deadline { get; init; }

            public int? intervalBeforeDeadline { get; init; }

            public int? offsetBeforeDeadline { get; init; }

            public int? intervalAfterDeadline { get; init; }

            public int? initialOffsetAfterDeadline { get; init; }

            public int? offsetAfterDeadline { get; init; }

            public int? repetitionsAfterDeadline { get; init; }

            public static NotificationMutationVariables Create(FwoNotification notification)
            {
                return new NotificationMutationVariables
                {
                    userId = notification.UserId,
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
        }
    }
}
