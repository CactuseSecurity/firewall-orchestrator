using GraphQL;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Services;
using System.Reflection;

namespace FWO.Test
{
    internal class NotificationTestApiConn : SimulatedApiConnection
    {
        public List<(int Id, NotificationLogStatus Status, string Error)> NotificationLogUpdates { get; } = [];
        public List<NotificationLogEntry> NotificationLogEntries { get; } = [];
        public List<int> UpdatedNotificationIds { get; } = [];

        readonly FwoNotification NotifReq1 = new()
        {
            Id = 1,
            RecipientTo = EmailRecipientOption.OtherAddresses,
            EmailAddressTo = "a@b.de",
            EmailSubject = "subject1",
            EmailBody = "body1",
            Deadline = NotificationDeadline.RequestDate,
            RepeatIntervalAfterDeadline = SchedulerInterval.Weeks,
            RepeatOffsetAfterDeadline = 1,
            RepetitionsAfterDeadline = 3
        };

        readonly FwoNotification NotifReq2 = new()
        {
            Id = 2,
            RecipientTo = EmailRecipientOption.OtherAddresses,
            EmailAddressTo = "a@b.de",
            EmailSubject = "subject2",
            EmailBody = "body2",
            Deadline = NotificationDeadline.RequestDate,
            RepeatIntervalAfterDeadline = SchedulerInterval.Days,
            RepeatOffsetAfterDeadline = 7,
            RepetitionsAfterDeadline = 1
        };

        readonly FwoNotification NotifRec = new()
        {
            Id = 1,
            RecipientTo = EmailRecipientOption.OtherAddresses,
            EmailAddressTo = "a@b.de",
            EmailSubject = "subject",
            EmailBody = "body",
            Deadline = NotificationDeadline.RecertDate,
            IntervalBeforeDeadline = SchedulerInterval.Weeks,
            OffsetBeforeDeadline = 3,
            RepeatIntervalAfterDeadline = SchedulerInterval.Weeks,
            RepeatOffsetAfterDeadline = 1,
            RepetitionsAfterDeadline = 1,
            Layout = NotificationLayout.HtmlAsAttachment
        };

        readonly FwoNotification NotifRuleTimer = new()
        {
            Id = 3,
            RecipientTo = EmailRecipientOption.OtherAddresses,
            EmailAddressTo = "a@b.de",
            EmailSubject = "subject3",
            EmailBody = "body3",
            Deadline = NotificationDeadline.RuleExpiry,
            RepeatIntervalAfterDeadline = SchedulerInterval.Days,
            RepeatOffsetAfterDeadline = 7,
            RepetitionsAfterDeadline = 2
        };

        readonly FwoNotification NotifImportChange = new()
        {
            Id = 4,
            NotificationClient = NotificationClient.ImportChange,
            RecipientTo = EmailRecipientOption.OtherAddresses,
            EmailAddressTo = "import@b.de",
            EmailSubject = "import-subject",
            EmailBody = "configured import body@@CONTENT@@",
            Deadline = NotificationDeadline.None,
            Layout = NotificationLayout.HtmlInBody
        };

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
            Type responseType = typeof(QueryResponseType);
            if (responseType == typeof(List<FwoNotification>))
            {
                string? Vars = variables?.ToString();
                List<FwoNotification>? notifs = Vars != null && Vars.Contains($"{NotificationClient.InterfaceRequest}")
                    ? [CloneNotification(NotifReq1), CloneNotification(NotifReq2)]
                    : Vars != null && Vars.Contains($"{NotificationClient.ImportChange}")
                        ? [CloneNotification(NotifImportChange)]
                    : Vars != null && Vars.Contains($"{NotificationClient.RuleTimer}")
                        ? [CloneNotification(NotifRuleTimer)]
                        : [CloneNotification(NotifRec)];
                GraphQLResponse<dynamic> response = new() { Data = notifs };
                return response.Data;
            }
            if (responseType == typeof(ReturnId))
            {
                if (query == NotificationQueries.updateNotificationsLastSent)
                {
                    PropertyInfo? idsProperty = variables?.GetType().GetProperty("ids");
                    if (idsProperty?.GetValue(variables) is IEnumerable<int> ids)
                    {
                        UpdatedNotificationIds.AddRange(ids);
                    }
                }

                if (query == NotificationQueries.updateNotificationLog)
                {
                    int id = GetVariable<int>(variables, "id");
                    NotificationLogStatus status = Enum.Parse<NotificationLogStatus>(GetVariable<string>(variables, "status"));
                    string error = GetVariable<string>(variables, "error");
                    NotificationLogUpdates.Add((id, status, error));
                    NotificationLogEntry? entry = NotificationLogEntries.FirstOrDefault(logEntry => logEntry.Id == id);
                    if (entry != null)
                    {
                        entry.Status = status;
                        entry.Error = error;
                    }
                    GraphQLResponse<dynamic> updateResponse = new() { Data = new ReturnId() { AffectedRows = 1 } };
                    return updateResponse.Data;
                }

                int notifCount = 0;
                var idsProp = variables?.GetType().GetProperty("ids");
                if (idsProp != null)
                {
                    var idsValue = idsProp.GetValue(variables);
                    if (idsValue is System.Collections.ICollection collection)
                    {
                        notifCount = collection.Count;
                    }
                }
                GraphQLResponse<dynamic> response = new() { Data = new ReturnId() { AffectedRows = notifCount } };
                return response.Data;
            }
            if (responseType == typeof(ReturnIdWrapper) && query == NotificationQueries.insertNotificationLog)
            {
                if (variables?.GetType().GetProperty("entries")?.GetValue(variables)
                    is IEnumerable<NotificationLogInsertEntry> entries)
                {
                    foreach (NotificationLogInsertEntry entry in entries)
                    {
                        NotificationLogEntries.Add(new NotificationLogEntry
                        {
                            Id = NotificationLogEntries.Count + 1,
                            NotificationId = entry.NotificationId,
                            Subject = entry.Subject,
                            DeadlineType = entry.DeadlineType,
                            Deadline = entry.Deadline,
                            Status = NotificationLogStatus.Pending
                        });
                    }
                }
                GraphQLResponse<dynamic> response = new()
                {
                    Data = new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { Id = NotificationLogEntries.LastOrDefault()?.Id ?? 1 }]
                    }
                };
                return response.Data;
            }
            if (responseType == typeof(List<NotificationLogEntry>) && query == NotificationQueries.getNoRecipientNotificationLogs)
            {
                int notificationId = GetVariable<int>(variables, "notificationId");
                return (QueryResponseType)(object)NotificationLogEntries
                    .Where(entry => entry.NotificationId == notificationId
                        && entry.Status == NotificationLogStatus.Failed
                        && entry.Error == "No recipients resolved.")
                    .ToList();
            }
            if (responseType == typeof(List<UiUser>) && query == AuthQueries.getUserEmails)
            {
                GraphQLResponse<dynamic> response = new() { Data = new List<UiUser>() };
                return response.Data;
            }
            if (responseType == typeof(List<OwnerResponsibleType>) && query == OwnerQueries.getOwnerResponsibleTypes)
            {
                GraphQLResponse<dynamic> response = new() { Data = new List<OwnerResponsibleType>() };
                return response.Data;
            }

            throw new NotImplementedException();
        }

        private static T GetVariable<T>(object? variables, string name)
        {
            return (T)(variables?.GetType().GetProperty(name)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing notification-log variable '{name}'."));
        }

        private static FwoNotification CloneNotification(FwoNotification notification)
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
                RecipientBcc = notification.RecipientBcc,
                EmailAddressBcc = notification.EmailAddressBcc,
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
                LastSent = notification.LastSent,
                Logging = NotificationLoggingMode.LogOnly
            };
        }
    }
}
