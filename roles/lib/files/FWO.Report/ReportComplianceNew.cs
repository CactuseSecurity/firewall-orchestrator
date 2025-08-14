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
            var semaphoreGetRules = new SemaphoreSlim(maxDegreeOfParallelism);
            var getRulesTasks = new List<Task>();

            Query.QueryVariables[QueryVar.Limit] = elementsPerFetch;
            Query.QueryVariables[QueryVar.Offset] = 0;

            // Set necessary Management Data

            List<ManagementReport> managementsWithRelevantImportId = await GetRelevantImportIds(apiConnection);

            ReportData.ManagementData = [];
            foreach (var management in managementsWithRelevantImportId)
            {
                SetMgtQueryVars(management);    // this includes mgm_id AND relevant import ID!
                ManagementReport managementReport = (await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables))[0];
                managementReport.Import = management.Import;
                ReportData.ManagementData.Add(managementReport);
            }

            // Count rules

            var result = await apiConnection.SendQueryAsync<AggregateCount>(RuleQueries.countRules);
            int rulesCount = result?.Aggregate?.Count ?? 0;

            while (CheckFetching(rulesCount, int.Parse(Query.QueryVariables[QueryVar.Offset].ToString() ?? "0")))
            {


                // Start task that gets chunk of rules

                await semaphoreGetRules.WaitAsync(ct);
                var task = Task.Run(async () =>
                {
                    try
                    {
                        List<Rule> fetchedRules = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesChunk, Query.QueryVariables);
                        Rules.AddRange(fetchedRules);
                    }
                    finally
                    {
                        semaphoreGetRules.Release();
                    }
                }, ct);
                getRulesTasks.Add(task);

                // Update offset

                Query.QueryVariables[QueryVar.Offset] = int.Parse(Query.QueryVariables[QueryVar.Offset].ToString() ?? "0") + elementsPerFetch;

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
        }

        private bool CheckFetching(int rulesCount, int offset)
        {
            bool keepFetching = false;

            if (rulesCount > 0 && offset < rulesCount)
            {
                keepFetching = true;

            }

            return keepFetching;
        }

        public override List<Rule> GetRules()
        {
            return Rules;
        }

        protected override void SetMgtQueryVars(ManagementReport management)
        {
            Query.QueryVariables[QueryVar.ImportIdStart] = management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1;
            Query.QueryVariables[QueryVar.ImportIdEnd]   = management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1;
        }
    }
}