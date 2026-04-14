using FWO.Logging;

namespace FWO.Api.Client
{
    public abstract class ApiConnection : IDisposable
    {
        private bool disposed = false;

        public event EventHandler<string>? OnAuthHeaderChanged;

        public Basics.Interfaces.ILogger Logger = new Logger();

        protected List<ApiSubscription> subscriptions = [];

        protected void InvokeOnAuthHeaderChanged(object? sender, string newAuthHeader)
        {
            OnAuthHeaderChanged?.Invoke(sender, newAuthHeader);
        }

        public abstract void SetAuthHeader(string jwt);

        public abstract void SetRole(string role);

        public abstract void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList);

        public abstract void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList);

        public abstract void SwitchBack();

        public async Task RunWithRole(string role, Func<Task> action)
        {
            SetRole(role);
            try
            {
                await action();
            }
            finally
            {
                SwitchBack();
            }
        }

        public async Task<TResult> RunWithRole<TResult>(string role, Func<Task<TResult>> action)
        {
            SetRole(role);
            try
            {
                return await action();
            }
            finally
            {
                SwitchBack();
            }
        }

        public async Task RunWithProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList, Func<Task> action)
        {
            SetProperRole(user, targetRoleList);
            try
            {
                await action();
            }
            finally
            {
                SwitchBack();
            }
        }

        public async Task<TResult> RunWithProperRole<TResult>(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList, Func<Task<TResult>> action)
        {
            SetProperRole(user, targetRoleList);
            try
            {
                return await action();
            }
            finally
            {
                SwitchBack();
            }
        }

        /// <summary>
        /// Sends an API call and returns the deserialized result or throws on errors.
        /// </summary>
        public abstract Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null);

        /// <summary>
        /// Sends an API call and returns a non-throwing response wrapper containing data or errors.
        /// </summary>
        public abstract Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null);

        public abstract GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler,
            GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null);

        protected abstract void Dispose(bool disposing);
        public abstract void DisposeSubscriptions<T>();

        ~ApiConnection()
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
