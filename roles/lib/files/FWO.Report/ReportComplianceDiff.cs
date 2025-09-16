using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Report.Filter;

namespace FWO.Report
{
    public class ReportComplianceDiff : ReportCompliance
    {
        protected override string InternalQuery => RuleQueries.getRulesWithViolationsInTimespanByChunk;
        
        public ReportComplianceDiff(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {

        }

        public ReportComplianceDiff(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, ReportParams reportParams) : base(query, userConfig, reportType, reportParams)
        {
            
        }
    }
}