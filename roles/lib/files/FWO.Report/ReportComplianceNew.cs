using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Filter;

namespace FWO.Report
{
    public class ReportComplianceNew : ReportCompliance
    {
        private readonly int _maxDegreeOfParallelism;
        private readonly SemaphoreSlim _semaphore;
        private List<Dictionary<string, object>> _queryVariablesList = new();
        private List<Task> _tasks = new();

        public ReportComplianceNew(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {
            _maxDegreeOfParallelism = Environment.ProcessorCount;
            _semaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
        }

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            _queryVariablesList.Clear();
            _tasks.Clear();
        
            // Get amount of rules to fetch.

            var result = await apiConnection.SendQueryAsync<AggregateCount>(RuleQueries.countRules);
            int rulesCount = result?.Aggregate?.Count ?? 0;

            // Get data parallelized.

            Rules = await GetDataParallelized<Rule>(rulesCount, elementsPerFetch, apiConnection, ct, RuleQueries.getRulesChunk);
            Log.TryWriteLog(LogType.Debug, "Compliance Report Prototype", $"Fetched {Rules.Count} rules for compliance report.", DebugConfig.ExtendedLogReportGeneration);

            Violations = await GetDataParallelized<ComplianceViolation>(rulesCount, elementsPerFetch, apiConnection, ct, ComplianceQueries.getViolationsChunk);
            Log.TryWriteLog(LogType.Debug, "Compliance Report Prototype", $"Fetched {Violations.Count} violations for compliance report.", DebugConfig.ExtendedLogReportGeneration);

            // Set compliance data.

            await SetComplianceData(ct);

            // Set report data.

            ReportData.RulesFlat = Rules;
            ReportData.ElementsCount = Rules.Count;
        }

        public async Task SetComplianceData(CancellationToken ct)
        {
            foreach (Rule rule in Rules)
            {
                await _semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await SetComplianceDataForRule(rule, Violations);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, ct);

                _tasks.Add(task);
            }

            await Task.WhenAll(_tasks);

            _tasks.Clear();
        }

        private async Task<List<T>> GetDataParallelized<T>(int rulesCount, int elementsPerFetch, ApiConnection apiConnection, CancellationToken ct, string query)
        {
            List<T> allData = new();

            // Create query variables for fetching rules

            for (int offset = 0; offset < rulesCount; offset += elementsPerFetch)
            {
                _queryVariablesList.Add(CreateQueryVariables(offset, elementsPerFetch, query));
            }

            // Start fetching fetching tasks

            foreach (Dictionary<string, object> queryVariables in _queryVariablesList)
            {
                await _semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        List<T> fetchedData = await apiConnection.SendQueryAsync<List<T>>(query, queryVariables);
                        allData.AddRange(fetchedData);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, ct);

                _tasks.Add(task);
            }

            // Wait for all tasks to complete and clear task list and query variables list

            await Task.WhenAll(_tasks);
            _tasks.Clear();
            _queryVariablesList.Clear();

            // Return all fetched data

            return allData;
        }

        private Dictionary<string, object> CreateQueryVariables(int offset, int limit, string query)
        {
            Dictionary<string, object> queryVariables = new();

            if (query.Contains(QueryVar.ImportIdStart))
            {
                queryVariables[QueryVar.ImportIdStart] = 0;
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

            return queryVariables;
        }
    }
}