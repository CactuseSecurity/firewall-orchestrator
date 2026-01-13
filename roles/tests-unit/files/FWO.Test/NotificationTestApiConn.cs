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
            Deadline = NotificationDeadline.RecertDate,
            IntervalBeforeDeadline = SchedulerInterval.Weeks,
            OffsetBeforeDeadline = 3,
            RepeatIntervalAfterDeadline = SchedulerInterval.Weeks,
            RepeatOffsetAfterDeadline = 1,
            RepetitionsAfterDeadline = 1,
            Layout = NotificationLayout.HtmlAsAttachment
        }; 

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
            Type responseType = typeof(QueryResponseType);
            if(responseType == typeof(List<FwoNotification>))
            {
                string? Vars = variables?.ToString();
                List<FwoNotification>? notifs = Vars != null && Vars.Contains($"{NotificationClient.InterfaceRequest}") ? [ NotifReq1, NotifReq2 ]: [ NotifRec ];
                GraphQLResponse<dynamic> response = new(){ Data = notifs };
                return response.Data;
            }
            if(responseType == typeof(ReturnId))
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
                GraphQLResponse<dynamic> response = new(){ Data = new ReturnId() { AffectedRows = notifCount } };
                return response.Data;
            }

            throw new NotImplementedException();
        }
    }
}
