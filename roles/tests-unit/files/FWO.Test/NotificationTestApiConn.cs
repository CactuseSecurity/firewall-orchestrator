using GraphQL;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Services;

namespace FWO.Test
{
    internal class NotificationTestApiConn : SimulatedApiConnection
    {
        readonly FwoNotification Notif1 = new()
        {
            Id = 1,
            RecipientTo = EmailRecipientOption.OtherAddresses,
            EmailAddressTo = "a@b.de",
            EmailSubject = "subject",
            Deadline = NotificationDeadline.RequestDate,
            RepeatIntervalAfterDeadline = SchedulerInterval.Days,
            RepeatOffsetAfterDeadline = 2,
            RepetitionsAfterDeadline = 3
        }; 

        readonly FwoNotification Notif2 = new()
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
                List<FwoNotification>? notifs = [ Vars != null && Vars.Contains($"{NotificationClient.InterfaceRequest}")? Notif1 : Notif2 ];
                GraphQLResponse<dynamic> response = new(){ Data = notifs };
                return response.Data;
            }

            throw new NotImplementedException();
        }
    }
}
