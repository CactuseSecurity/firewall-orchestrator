using FWO.Api.Client;
using GraphQL;
using GraphQL.Client.Http;

namespace FWO.Test
{
    internal class SimulatedApiConnection : ApiConnection
    {
        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            throw new NotImplementedException();
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            throw new NotImplementedException();
        }

        public override void SetAuthHeader(string jwt)
        {}

        public override void SetRole(string role)
        {}

        public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {}

        public override void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {}

        public override void SwitchBack()
        {}

        protected override void Dispose(bool disposing)
        {}

        public override void DisposeSubscriptions<T>()
        {}
    }

    internal class SimulatedApiSubscription<SubscriptionResponseType> : GraphQlApiSubscription<SubscriptionResponseType>
    {
        public SimulatedApiSubscription(ApiConnection apiConnection, GraphQLHttpClient graphQlClient, GraphQLRequest request, Action<Exception> exceptionHandler, SubscriptionUpdate OnUpdate)
         : base(apiConnection, graphQlClient, request, exceptionHandler, OnUpdate)
        { }
        
        protected override void CreateSubscription()
        { }
    }
}
