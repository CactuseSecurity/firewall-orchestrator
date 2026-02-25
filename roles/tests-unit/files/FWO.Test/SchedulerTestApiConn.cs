using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Services;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;


namespace FWO.Test
{
    internal class SchedulerTestApiConn : SimulatedApiConnection
    {
        public List<string> LogEntries = [];
        public List<string> Alerts = [];
        public List<long> AcknowledgedAlerts = [];

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
            Type responseType = typeof(QueryResponseType);
            if (responseType == typeof(ReturnIdWrapper))
            {
                if (query == MonitorQueries.addLogEntry && variables != null)
                {
                    string? entry = variables.ToString();
                    if (entry != null)
                    {
                        LogEntries.Add(entry);
                    }
                }
                else if (query == MonitorQueries.addAlert && variables != null)
                {
                    string? alert = variables.ToString();
                    if (alert != null)
                    {
                        Alerts.Add(alert);
                    }
                }
                ReturnIdWrapper ReturnIdWrap = new() { ReturnIds = [new()] };
                GraphQLResponse<dynamic> response = new() { Data = ReturnIdWrap };
                return response.Data;
            }
            else if (responseType == typeof(ReturnId))
            {
                if (query == MonitorQueries.acknowledgeAlert && variables != null)
                {
                    object? rawId = variables.GetType().GetProperty("id")?.GetValue(variables);
                    if (rawId != null)
                    {
                        AcknowledgedAlerts.Add(Convert.ToInt64(rawId));
                    }
                }
                GraphQLResponse<dynamic> response = new() { Data = new ReturnId() };
                return response.Data;
            }
            else if (responseType == typeof(List<Alert>))
            {
                GraphQLResponse<dynamic> response = new() { Data = new List<Alert>() { new() { Id = 7, AlertCode = AlertCode.UiError } } };
                return response.Data;
            }

            throw new NotImplementedException();
        }

        public override SimulatedApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, SimulatedApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            return new(this, new(new GraphQLHttpClientOptions(), new SystemTextJsonSerializer(), new()), new(subscription, variables, operationName), exceptionHandler, subscriptionUpdateHandler);
        }
    }
}
