using FWO.Basics;
using System.Text;
using FWO.Report.Filter;
using FWO.Ui.Display;
using FWO.Config.Api;
using FWO.Data;
using FWO.Api.Client;
using FWO.Data.Report;
using FWO.Api.Client.Queries;
using System.Reflection;

namespace FWO.Report
{
    public class ReportCompliance : ReportRules
    {
        public ReportCompliance(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }
        public List<ComplianceViolation> Violations { get; set; } = [];
        public List<Rule> Rules { get; set; } = [];

        public override string ExportToCsv()
        {
            string csvString = "";

            if (Rules.Count > 0)
            {
                // Set export configuration

                bool includeHeader = true;
                char separator = ',';

                List<string> columnsToExport = new List<string>
                {
                    "Id",
                    "Name",
                    "Comment",
                    "Source",
                    "Destination",
                    "Action",
                    "IsCompliant",
                    "ViolationDetails"
                };

                // Create export string

                StringBuilder sb = new StringBuilder();
                Type type = typeof(Rule);
                List<PropertyInfo?> properties = columnsToExport
                                                    .Select(name => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance))
                                                    .Where(p => p != null)
                                                    .ToList();

                if (includeHeader)
                {
                    sb.AppendLine(string.Join(separator, properties.Select(property => property?.Name)));
                }

                foreach (Rule rule in Rules)
                {
                    var values = properties.Select(p =>
                    {
                        var value = p!.GetValue(rule);
                        if (value == null)
                            return "";
                        if (value is string s)
                            return Escape(s, separator);
                        return Escape(value.ToString()!, separator);
                    });

                    sb.AppendLine(string.Join(separator, values));
                }

                return sb.ToString();
            }

            return csvString;
        }

        private string Escape(string input, char separator)
        {
            // Replace line breaks with space

            input = input.Replace("\r", " ").Replace("\n", " ");

            if (input.Contains(separator) || input.Contains('"'))
            {
                // Escape quotation marks

                input = input.Replace("\"", "\"\"");

                return $"\"{input}\"";
            }
            return input;
        }

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            var baseTask = base.Generate(rulesPerFetch, apiConnection, callback, ct);
            var violationsTask = apiConnection.SendQueryAsync<List<ComplianceViolation>>(ComplianceQueries.getViolations);

            await Task.WhenAll(baseTask, violationsTask);

            Violations = violationsTask.Result;
            await SetComplianceData();
        }

        private async Task SetComplianceData() // We will deal with the warning (CS1998) when we are ready for performance optimization 
        {
            Rules.Clear();

            foreach (var management in ReportData.ManagementData)
            {
                foreach (var rulebase in management.Rulebases)
                {
                    foreach (var rule in rulebase.Rules)
                    {
                        if (rule is Rule currentRule)
                        {
                            rule.Violations.Clear();
                            rule.ViolationDetails = "";

                            foreach (var violation in Violations.Where(violation => violation.RuleId == currentRule.Id))
                            {
                                if (rule.ViolationDetails != "")
                                {
                                    rule.ViolationDetails += "\n";
                                }

                                rule.IsCompliant = false;
                                rule.ViolationDetails += violation.Details;
                                rule.Violations.Add(violation);
                            }

                            if (!Rules.Contains(rule))
                            {
                                Rules.Add(rule);
                            }

                        }
                    }
                }
            }
        }
    }
}
