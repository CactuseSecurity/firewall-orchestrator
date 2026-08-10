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

        protected override void SetComplianceDataForRule(Rule rule, ApiConnection apiConnection, Func<ComplianceViolation, string>? formatter = null)
        {
            base.SetComplianceDataForRule(rule, apiConnection, FormatViolationDetails);
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

            if (query.Contains("violations_where"))
            {
                DateTime reportEnd = DateTime.Now;
                DateTime reportStart = reportEnd.AddDays(-DiffReferenceInDays);
                var violationsWhere = new Dictionary<string, object>
                {
                    ["found_date"] = new Dictionary<string, object?>
                    {
                        ["_gte"] = reportStart,
                        ["_lt"] = reportEnd
                    }
                };
                if (GlobalConfig.ComplianceFilterOutInitialViolations)
                {
                    violationsWhere["is_initial"] = new Dictionary<string, object?>
                    {
                        ["_eq"] = false
                    };
                }
                queryVariables["violations_where"] = violationsWhere;
                // Fetch only rules that were compliant at the start of the report interval, if requested.
                queryVariables["rule_where"] = GlobalConfig.ComplianceDiffFilterExistingViolations
                    ? CreateNoPreviousViolationsWhere(reportStart)
                    : [];
            }
            return queryVariables;
        }

        /// <summary>
        /// Creates a rule filter that excludes violations which made a rule non-compliant at the start of the report interval.
        /// </summary>
        private static Dictionary<string, object> CreateNoPreviousViolationsWhere(DateTime reportStart)
        {
            return new Dictionary<string, object>
            {
                ["_not"] = new Dictionary<string, object>
                {
                    ["compliance_violations_version_agnostic"] = new Dictionary<string, object>
                    {
                        ["found_date"] = new Dictionary<string, object?>
                        {
                            ["_lt"] = reportStart
                        },
                        ["_or"] = new List<Dictionary<string, object>>
                        {
                            new()
                            {
                                ["removed_date"] = new Dictionary<string, object?>
                                {
                                    ["_is_null"] = true
                                }
                            },
                            new()
                            {
                                ["removed_date"] = new Dictionary<string, object?>
                                {
                                    ["_gte"] = reportStart
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
