using GraphQL;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Services;

namespace FWO.Test
{
    internal class NotificationTestApiConn : SimulatedApiConnection
    {
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
                LastSent = notification.LastSent
            };
        }
    }
}
