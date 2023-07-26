using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Api.Client
{
    public abstract class ApiConnection : IDisposable
    {
        private bool disposed = false;

        public event EventHandler<string>? OnAuthHeaderChanged;

        protected List<ApiSubscription> subscriptions = new List<ApiSubscription>();

        protected void InvokeOnAuthHeaderChanged(object? sender, string newAuthHeader)
        {
            OnAuthHeaderChanged?.Invoke(sender, newAuthHeader);
        }

        public abstract void SetAuthHeader(string jwt);

        public abstract void SetRole(string role);

        public abstract void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList);

        public abstract void SwitchBack();

        public abstract Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null);

        public abstract GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null);

        protected virtual void AddSubscription(ApiSubscription subscription)
        {
            subscriptions.Add(subscription);
        }

        protected abstract void Dispose(bool disposing);

        ~ ApiConnection()
        {
            if (disposed) return;
            Dispose(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            Dispose(true);
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
