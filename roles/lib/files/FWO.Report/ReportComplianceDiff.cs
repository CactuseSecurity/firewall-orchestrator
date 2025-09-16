using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
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

        protected override bool ShowRule(Rule rule)
        {
            bool showRule = base.ShowRule(rule);

            if (rule.ViolationDetails.StartsWith("No changes") || rule.Disabled)
            {
                showRule = false;
            }

            return showRule;
        }
    }
}