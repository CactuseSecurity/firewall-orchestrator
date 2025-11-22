using DnsClient.Internal;
using FWO.Basics;
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

        private static SemaphoreSlim CreateSemaphore(int parallelismLevel)
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(parallelismLevel);
            return semaphore;
        }

        public abstract Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null);

        public virtual async Task<List<T>[]> SendParallelizedQueriesAsync<T>(
            int elementsCount,
            int parallelismLevel,
            int elementsPerFetch,
            string query,
            Func<List<T>, Task<List<T>>>? postProcessAsync = null,
            List<int>? managementIds = null
            )
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(parallelismLevel, parallelismLevel);

            List<Task<List<T>>> tasks = new();

            int chunkNumber = 0;

            for (int offset = 0; offset < elementsCount; offset += elementsPerFetch)
            {
                chunkNumber++;
                var queryVariables = CreateQueryVariables(offset, elementsPerFetch, query, managementIds);

                tasks.Add(FetchChunkAsync(query, queryVariables, semaphore, chunkNumber, postProcessAsync));
            }

            return await Task.WhenAll(tasks);

                async Task<List<T>> FetchChunkAsync(
                string query,
                Dictionary<string, object> queryVariables,
                SemaphoreSlim semaphore,
                int chunkNumber,
                Func<List<T>, Task<List<T>>>? postProcessAsync)
            {
                await semaphore.WaitAsync();
                try
                {
                    List<T> data = await SendQueryAsync<List<T>>(query, queryVariables);

                    if (postProcessAsync != null)
                    {
                        Logger.TryWriteInfo("Api Connection", $"Processing chunk {chunkNumber}.", LocalSettings.ComplianceCheckVerbose);
                        data = await postProcessAsync(data);
                        Logger.TryWriteInfo("Api Connection", $"Processed chunk {chunkNumber}.", LocalSettings.ComplianceCheckVerbose);
                    }

                    return data;
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }


        protected virtual Dictionary<string, object> CreateQueryVariables(int offset, int limit, string query, List<int>? relevanteManagementIDs = null)
        {
            Dictionary<string, object> queryVariables = new();

            if (query.Contains(QueryVar.ImportIdStart))
            {
                queryVariables[QueryVar.ImportIdStart] = int.MaxValue;
            }

            if (query.Contains(QueryVar.ImportIdEnd))
            {
                queryVariables[QueryVar.ImportIdEnd] = int.MaxValue;
            }

            if (query.Contains(QueryVar.Offset))
            {
                queryVariables[QueryVar.Offset] = offset;
            }

            if (query.Contains(QueryVar.Limit))
            {
                queryVariables[QueryVar.Limit] = limit;
            }

            if (query.Contains("mgm_ids"))
            {
                queryVariables["mgm_ids"] = relevanteManagementIDs ?? [];
            }

            return queryVariables;
        }

        public abstract GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, 
            GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null);

        protected abstract void Dispose(bool disposing);
        public abstract void DisposeSubscriptions<T>();

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
