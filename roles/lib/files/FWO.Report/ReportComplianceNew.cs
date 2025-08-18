using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;

namespace FWO.Report
{
    public class ReportComplianceNew : ReportCompliance
    {
        public ReportComplianceNew(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {

        }

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            int maxDegreeOfParallelism = Environment.ProcessorCount;
            SemaphoreSlim? semaphoreGetRules = new SemaphoreSlim(maxDegreeOfParallelism);
            List<Task> getRulesTasks = new List<Task>();
            List<Dictionary<string, object>> QueryVariablesList = new List<Dictionary<string, object>>();

            // Get amount of rules to fetch

            var result = await apiConnection.SendQueryAsync<AggregateCount>(RuleQueries.countRules);
            int rulesCount = result?.Aggregate?.Count ?? 0;

            // Create query variables for fetching rules

            for (int offset = 0; offset < rulesCount; offset += elementsPerFetch)
            {
                QueryVariablesList.Add(CreateQueryVariables(offset, elementsPerFetch));
            }

            // Start fetching rules in parallel

            foreach (Dictionary<string, object> queryVariables in QueryVariablesList)
            {
                await semaphoreGetRules.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        List<Rule> fetchedRules = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesChunk, queryVariables);
                        Rules.AddRange(fetchedRules);
                    }
                    finally
                    {
                        semaphoreGetRules.Release();
                    }
                }, ct);

                getRulesTasks.Add(task);
            }

            await Task.WhenAll(getRulesTasks);

            // Set compliance data.


            var semaphoreSetComplianceData = new SemaphoreSlim(maxDegreeOfParallelism);

            var setComplianceDataForRuleTasks = new List<Task>();

            foreach (Rule rule in Rules)
            {
                await semaphoreSetComplianceData.WaitAsync(ct);
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await SetComplianceDataForRule(rule, Violations);
                    }
                    finally
                    {
                        semaphoreSetComplianceData.Release();
                    }
                }, ct);
                setComplianceDataForRuleTasks.Add(task);
            }
            await Task.WhenAll(setComplianceDataForRuleTasks);

            ReportData.RulesFlat = Rules;
            ReportData.ElementsCount = Rules.Count;
        }

        public override List<Rule> GetRules()
        {
            return Rules;
        }

        private Dictionary<string, object> CreateQueryVariables(int offset, int limit)
        {
            return new Dictionary<string, object>
            {
                { QueryVar.ImportIdStart, 0 },
                { QueryVar.ImportIdEnd, int.MaxValue },
                { QueryVar.Offset, offset },
                { QueryVar.Limit, limit }
            };
        }
    }
}