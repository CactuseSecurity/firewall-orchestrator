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

        public ReportComplianceNew(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {
            _maxDegreeOfParallelism = Environment.ProcessorCount;
            _semaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
        }

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            // Get amount of rules to fetch.

            var result = await apiConnection.SendQueryAsync<AggregateCount>(RuleQueries.countRules);
            int rulesCount = result?.Aggregate?.Count ?? 0;

            // Get data parallelized.

            Rules = await GetDataParallelized<Rule>(rulesCount, rulesPerFetch, apiConnection, ct, RuleQueries.getRulesWithViolationsByChunk);
            Log.TryWriteLog(LogType.Debug, "Compliance Report Prototype", $"Fetched {Rules.Count} rules for compliance report.", DebugConfig.ExtendedLogReportGeneration);

            // Set compliance data.

            await SetComplianceData(ct);

            // Set report data.

            ReportData.RulesFlat = Rules;
            ReportData.ElementsCount = Rules.Count;
        }

        public async Task SetComplianceData(CancellationToken ct)
        {
            List<Task> tasks = new();

            foreach (Rule rule in Rules)
            {
                await _semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await SetComplianceDataForRule(rule);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, ct);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        private async Task SetComplianceDataForRule(Rule rule)
        {
            try
            {
                rule.ViolationDetails = "";
                rule.Compliance = ComplianceViolationType.None;

                if (await CheckEvaluability(rule))
                {
                    foreach (var violation in rule.Violations)
                    {
                        if (IsDiffReport && ViolationDiffs.TryGetValue(violation, out char changeSign))
                        {
                            violation.Details = $"({changeSign}) {violation.Details}";
                        }

                        if (rule.ViolationDetails != "")
                        {
                            rule.ViolationDetails += "\n";
                        }

                        rule.ViolationDetails += violation.Details;

                        // No need to differentiate between different types of violations here at the moment.

                        rule.Compliance = ComplianceViolationType.MultipleViolations;
                    }                
                }
                else
                {
                    rule.Compliance = ComplianceViolationType.NotEvaluable;
                }
            }
            catch (System.Exception e)
            {
                Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while setting compliance data for rule {rule.Id}: {e.Message}", DebugConfig.ExtendedLogReportGeneration);
                return;
            }

        }

        private async Task<List<T>> GetDataParallelized<T>(int rulesCount, int elementsPerFetch, ApiConnection apiConnection, CancellationToken ct, string query)
        {
            List<T> allData = new();
            List<Task<List<T>>> tasks = new();
            List<Dictionary<string, object>> queryVariablesList = new();

            // Create query variables for fetching rules

            for (int offset = 0; offset < rulesCount; offset += elementsPerFetch)
            {
                queryVariablesList.Add(CreateQueryVariables(offset, elementsPerFetch, query));
            }

            // Start fetching fetching tasks

            foreach (Dictionary<string, object> queryVariables in queryVariablesList)
            {
                await _semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        return await apiConnection.SendQueryAsync<List<T>>(query, queryVariables);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, ct);

                tasks.Add(task);
            }

            // Wait for all tasks to complete and return the data

            List<T>[]? chunks = await Task.WhenAll(tasks);

            if (chunks != null)
            {
                foreach (List<T> chunk in chunks)
                {
                    if (chunk != null)
                    {
                        allData.AddRange(chunk);
                    }
                }
            }

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