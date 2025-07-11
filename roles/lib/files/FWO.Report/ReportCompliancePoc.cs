using FWO.Basics;
using System.Text;
using FWO.Report.Filter;
using FWO.Ui.Display;
using FWO.Config.Api;
using FWO.Data;
using FWO.Api.Client;
using FWO.Data.Report;
using FWO.Api.Client.Queries;

namespace FWO.Report
{
    public class ReportCompliancePoc : ReportRules
    {
        public ReportCompliancePoc(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }
        public List<ComplianceViolation> Violations { get; set; } = [];

        public override string ExportToCsv()
        {
            return "";
        }

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            await base.Generate(rulesPerFetch, apiConnection, callback, ct);

            Violations = await apiConnection.SendQueryAsync<List<ComplianceViolation>>(ComplianceQueries.getViolations);
            await SetIsCompliantAndViolationDetails();
        }

        private async Task SetIsCompliantAndViolationDetails()
        {
            foreach (var management in ReportData.ManagementData)
            {
                foreach (var rulebase in management.Rulebases)
                {
                    foreach (var rule in rulebase.Rules)
                    {
                        if (rule is Rule currentRule)
                        {
                            foreach (var violation in Violations.Where(violation => violation.RuleId == currentRule.Id))
                            {
                                rule.IsCompliant = false;
                                rule.ViolationDetails += $"{violation.Details}\n";
                            }

                        }
                    }
                }
            }
        }
    }
}
