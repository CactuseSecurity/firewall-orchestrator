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
    public class ReportComplianceDiff : ReportCompliance
    {
        public int DiffReferenceInDays { get; set; } = 0;

        protected override string InternalQuery => RuleQueries.getRulesWithViolationsInTimespanByChunk;

        public ReportComplianceDiff(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {

        }

        public ReportComplianceDiff(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, ReportParams reportParams) : base(query, userConfig, reportType, reportParams)
        {
            DiffReferenceInDays = reportParams.ComplianceFilter.DiffReferenceInDays;
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

        protected override Task SetComplianceDataForRule(Rule rule, ApiConnection apiConnection, Func<ComplianceViolation, string>? formatter = null)
        {
            return base.SetComplianceDataForRule(rule, apiConnection, FormatViolationDetails);
        }


        private string FormatViolationDetails(ComplianceViolation violation)
        {
            return $"Found: ({violation.FoundDate:dd.MM.yyyy - hh:mm}) {violation.Details}";
        }

        protected virtual async Task PostProcessDiffReportsRule(Rule rule, ApiConnection apiConnection)
        {
            if (rule.ViolationDetails == "")
            {
                DateTime from = DateTime.Now.AddDays(-DiffReferenceInDays);
                rule.ViolationDetails = $"No changes between {from:dd.MM.yyyy} - {from:HH:mm} and {DateTime.Now:dd.MM.yyyy} - {DateTime.Now:HH:mm}";
            }

            string managementUid = Managements?.FirstOrDefault(m => m.Id == rule.MgmtId)?.Uid ?? "";

            var variables = new { ruleUid = rule.Uid, mgmtUid = managementUid };
            List<ComplianceViolation>? violations = await apiConnection.SendQueryAsync<List<ComplianceViolation>>(ComplianceQueries.getViolationsByRuleUid, variables: variables);

            if (violations != null)
            {
                rule.Compliance = violations.Where(violation => violation.RemovedDate == null).ToList().Count > 0 ? ComplianceViolationType.MultipleViolations : ComplianceViolationType.None;
            }
        }
        
        protected override Dictionary<string, object> CreateQueryVariables(int offset, int limit, string query)
        {
            Dictionary<string, object> queryVariables = base.CreateQueryVariables(offset, limit, query);

            if (query.Contains("from_date"))
            {
                queryVariables["from_date"] = DateTime.Now.AddDays(-DiffReferenceInDays);
            }

            if (query.Contains("to_date"))
            {
                queryVariables["to_date"] = DateTime.Now;
            }

            return queryVariables;
        }
    }
}