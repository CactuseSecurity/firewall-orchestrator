using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;

namespace FWO.Test.Mocks
{
    public class MockReportComplianceDiff : ReportComplianceDiff
    {
        public bool MockPostProcessDiffReportsRule { get; set; } = true;

        public MockReportComplianceDiff(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {
        }

        public MockReportComplianceDiff(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, ReportParams reportParams) : base(query, userConfig, reportType, reportParams)
        {
        }

        /// <summary>
        /// Creates the query variables for testing the diff report filters.
        /// </summary>
        public Dictionary<string, object> CreateQueryVariablesPublic(int offset, int limit, string query)
        {
            return CreateQueryVariables(offset, limit, query);
        }

        protected override async Task PostProcessDiffReportsRule(Rule rule, ApiConnection apiConnection)
        {
            if (MockPostProcessDiffReportsRule)
            {
                await Task.CompletedTask;
            }
            else
            {
                await base.PostProcessDiffReportsRule(rule, apiConnection);
            }
        }
    }

}
