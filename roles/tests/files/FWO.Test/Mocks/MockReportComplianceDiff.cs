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