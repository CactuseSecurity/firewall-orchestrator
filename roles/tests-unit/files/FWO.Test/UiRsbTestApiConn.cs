using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Services;
using GraphQL;

namespace FWO.Test
{
    internal class UiRsbTestApiConn : SimulatedApiConnection
    {
        private readonly Dictionary<long, string> ruleQueries = [];

        /// <summary>
        /// Variables of all rule detail queries.
        /// </summary>
        public List<Dictionary<string, object>> RuleQueryVariables { get; } = [];

        /// <summary>
        /// Times the import ids of the all tab were requested for, in pairs of start and end.
        /// </summary>
        public List<string> RequestedImportTimes { get; } = [];

        /// <summary>
        /// Import id of all managements at any time.
        /// </summary>
        public long? RelevantImportId { get; set; }

        /// <summary>
        /// Number of object detail queries (by ids) which fail before the following ones succeed.
        /// </summary>
        public int FailObjectDetailQueries { get; set; }

        /// <summary>
        /// Returns the query the details of a rule were fetched with.
        /// </summary>
        public string QueryOfRule(long ruleId)
        {
            return ruleQueries[ruleId];
        }

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
            Type responseType = typeof(QueryResponseType);
            if (query == ReportQueries.getRelevantImportIdsAtTime)
            {
                RequestedImportTimes.Add((string)((Dictionary<string, object>)variables!)[QueryVar.Time]);
            }
            if (responseType == typeof(List<ManagementReport>))
            {
                List<ManagementReport> reports = SimulatedReport.DetailedReport().ReportData.ManagementData;
                reports.ForEach(report => report.Import.ImportAggregate.ImportAggregateMax.RelevantImportId = RelevantImportId);
                GraphQLResponse<dynamic> response = new() { Data = reports };
                return response.Data;
            }
            if (responseType == typeof(List<Rule>))
            {
                RuleQueryVariables.Add((Dictionary<string, object>)variables!);
                ruleQueries[(long)RuleQueryVariables[^1][QueryVar.RuleId]] = query;
                List<Rule> rules = SimulatedReport.DetailedReport().ReportData.ManagementData
                    .SelectMany(management => management.Rulebases)
                    .SelectMany(rulebase => rulebase.Rules)
                    .ToList();
                return (QueryResponseType)(object)rules;
            }
            if (query == ObjectQueries.getNetworkObjectDetailsByIds)
            {
                return (QueryResponseType)(object)GetNetworkObjects((Dictionary<string, object>)variables!);
            }
            throw new NotImplementedException();
        }

        private List<NetworkObject> GetNetworkObjects(Dictionary<string, object> variables)
        {
            if (FailObjectDetailQueries > 0)
            {
                FailObjectDetailQueries--;
                throw new InvalidOperationException("simulated api error");
            }
            long[] ids = (long[])variables[QueryVar.NwObjIds];
            return [.. SimulatedReport.DetailedReport().ReportData.ManagementData[0].ReportObjects.Where(networkObject => ids.Contains(networkObject.Id))];
        }
    }
}
