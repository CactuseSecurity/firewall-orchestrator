using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;

namespace FWO.Test.Mocks
{
    public class MockReportComplianceDiff : ReportComplianceDiff
    {
        public MockReportComplianceDiff(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {
        }

        public MockReportComplianceDiff(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, ReportParams reportParams) : base(query, userConfig, reportType, reportParams)
        {
        }

    }

}
