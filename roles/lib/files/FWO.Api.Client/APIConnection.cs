using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Api.Client
{
    public abstract class ApiConnection : IDisposable
    {
        public event EventHandler<string>? OnAuthHeaderChanged;

        protected void InvokeOnAuthHeaderChanged(object? sender, string newAuthHeader)
        {
            OnAuthHeaderChanged?.Invoke(sender, newAuthHeader);
        }

        public abstract void SetAuthHeader(string jwt);

        public abstract void SetRole(string role);

        public abstract void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList);

        public abstract Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null);

        public abstract ApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, ApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null);

        public virtual void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
