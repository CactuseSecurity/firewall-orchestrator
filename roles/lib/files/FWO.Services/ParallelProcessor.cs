using FWO.Api.Client;
using FWO.Basics;
using FWO.Basics.Interfaces;

namespace FWO.Services
{
    public class ParallelProcessor(ApiConnection apiConnection, ILogger logger)
    {
        private int _elementsCount = 0;
        private int _elementsPerFetch = 0;
        private int _parallelismLevel = 0;

        public void SetUp(int elementsCount, int parallelismLevel, int elementsPerFetch)
        {
            _elementsCount = elementsCount;
            _parallelismLevel = parallelismLevel;
            _elementsPerFetch = elementsPerFetch;
        }

        /// <summary>
        /// Executes a GraphQL query in multiple chunks while respecting a configurable parallelism level.
        /// The method throttles concurrency via a semaphore and optionally post-processes each chunk.
        /// </summary>
        /// <typeparam name="T">Element type of each chunk in the result set.</typeparam>
        /// <param name="query">GraphQL query to execute.</param>
        /// <param name="postProcessAsync">Optional callback invoked for each chunk before returning.</param>
        /// <param name="managementIds">Management identifiers passed to the GraphQL query when required.</param>
        /// <param name="importId">Import identifier that scopes the requested data.</param>
        /// <param name="cancellationToken">Token used to cancel the work, if necessary.</param>
        /// <returns>All chunk results ordered by their chunk number.</returns>
        public virtual async Task<List<T>[]> SendParallelizedQueriesAsync<T>(
            string query,
            Func<List<T>, Task<List<T>>>? postProcessAsync = null,
            List<int>? managementIds = null,
            long? importId = 0,
            CancellationToken cancellationToken = default)
        {
            if (_elementsCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(_elementsCount), "Number of elements cannot be negative.");
            }

            if (_parallelismLevel <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(_parallelismLevel), "Parallelism level must be at least 1.");
            }

            if (_elementsPerFetch <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(_elementsPerFetch), "Chunk size must be at least 1.");
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("GraphQL query must be provided.", nameof(query));
            }

            if (_elementsCount == 0)
            {
                return Array.Empty<List<T>>();
            }

            using SemaphoreSlim semaphore = new(_parallelismLevel, _parallelismLevel);
            int chunkCount = (int)Math.Ceiling(_elementsCount / (double)_elementsPerFetch);
            List<Task<List<T>>> tasks = new(chunkCount);
            int chunkNumber = 0;

            for (int offset = 0; offset < _elementsCount; offset += _elementsPerFetch)
            {
                chunkNumber++;

                // Prepare the GraphQL variables for the batch and queue its execution.

                var queryVariables = CreateQueryVariables(offset, _elementsPerFetch, query, managementIds, importId);
                tasks.Add(FetchChunkAsync(query, queryVariables, semaphore, chunkNumber, postProcessAsync, cancellationToken));
            }

            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Fetches a chunk from the API, optionally post-processes it and enforces the semaphore limit.
        /// </summary>
        public virtual async Task<List<T>> FetchChunkAsync<T>(
            string query,
            Dictionary<string, object> queryVariables,
            SemaphoreSlim semaphore,
            int chunkNumber,
            Func<List<T>, Task<List<T>>>? postProcessAsync,
            CancellationToken cancellationToken = default)
        {
            // Limit concurrent requests to the configured parallelism.

            await semaphore.WaitAsync(cancellationToken);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Get chunk data from API.

                List<T>? data = await apiConnection.SendQueryAsync<List<T>>(query, queryVariables);
                data ??= [];

                // Optionally post-process chunk data.

                if (postProcessAsync != null)
                {
                    logger.TryWriteInfo("Parallel Processing", $"Processing chunk {chunkNumber}.", LocalSettings.ComplianceCheckVerbose);
                    data = await postProcessAsync(data) ?? data;
                    logger.TryWriteInfo("Parallel Processing", $"Processed chunk {chunkNumber}.", LocalSettings.ComplianceCheckVerbose);
                }

                return data;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Creates GraphQL query variables for parallelized queries.
        /// </summary>
        /// <param name="offset">Offset used for the chunk.</param>
        /// <param name="limit">Chunk size.</param>
        /// <param name="query">GraphQL query under construction.</param>
        /// <param name="relevantManagementIDs">Management identifiers relevant for the chunk.</param>
        /// <param name="importId">Import identifier that scopes the data.</param>
        protected virtual Dictionary<string, object> CreateQueryVariables(int offset, int limit, string query, List<int>? relevantManagementIDs = null, long? importId = null)
        {
            Dictionary<string, object> queryVariables = new();

            if (query.Contains(QueryVar.ImportIdStart))
            {
                queryVariables[QueryVar.ImportIdStart] = importId ?? 0;
            }

            if (query.Contains(QueryVar.ImportIdEnd))
            {
                queryVariables[QueryVar.ImportIdEnd] = importId ?? int.MaxValue;
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
