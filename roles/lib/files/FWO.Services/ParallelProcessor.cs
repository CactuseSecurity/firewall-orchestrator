using FWO.Api.Client;
using FWO.Basics;
using FWO.Basics.Interfaces;

namespace FWO.Services
{
    public class ParallelProcessor(ApiConnection apiConnection, ILogger logger)
    {
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
    
        }

        public virtual async Task<List<T>> FetchChunkAsync<T>(
                string query,
                Dictionary<string, object> queryVariables,
                SemaphoreSlim semaphore,
                int chunkNumber,
                Func<List<T>, Task<List<T>>>? postProcessAsync)
            {
                await semaphore.WaitAsync();
                try
                {
                    List<T> data = await apiConnection.SendQueryAsync<List<T>>(query, queryVariables);

                    if (postProcessAsync != null)
                    {
                        logger.TryWriteInfo("Parallel Processing", $"Processing chunk {chunkNumber}.", LocalSettings.ComplianceCheckVerbose);
                        data = await postProcessAsync(data);
                        logger.TryWriteInfo("Parallel Processing", $"Processed chunk {chunkNumber}.", LocalSettings.ComplianceCheckVerbose);
                    }

                    return data;
                }
                finally
                {
                    semaphore.Release();
                }
            }

        protected virtual Dictionary<string, object> CreateQueryVariables(int offset, int limit, string query, List<int>? relevantManagementIDs = null)
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
                queryVariables["mgm_ids"] = relevantManagementIDs ?? [];
            }

            return queryVariables;
        }
    }
}