using FWO.Basics;
using System.Text;
using FWO.Report.Filter;
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
        public List<ComplianceViolation> NewViolations { get; set; } = [];
        public List<Rule> Rules { get; set; } = [];

        private readonly bool _includeHeaderInExport = true;
        private readonly char _separator = '#';
        private readonly List<string> _columnsToExport = new List<string>
        {
            "MgmtId",
            "Uid",
            "Name",
            "Comment",
            "Source",
            "Destination",
            "Services",
            "Action",
            "MetaData",
            "CustomFields",
            "InstallOn",
            "IsCompliant",
            "ViolationDetails"
        };

        public override string ExportToCsv()
        {
            string csvString = "";

            if (Rules.Count > 0)
            {
                // Create export string

                StringBuilder sb = new StringBuilder();
                Type type = typeof(Rule);
                List<PropertyInfo?> properties = _columnsToExport
                                                    .Select(name => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance))
                                                    .Where(p => p != null)
                                                    .ToList();

                if (_includeHeaderInExport)
                {
                    sb.AppendLine(string.Join(_separator, properties.Select(property => property?.Name)));
                }

                foreach (Rule rule in Rules)
                {
                    sb.AppendLine(GetLineForRule(rule, properties));
                }

                return sb.ToString();
            }

            return csvString;
        }

        private string GetLineForRule(Rule rule, List<PropertyInfo?> properties)
        {
            IEnumerable<string> values = properties.Select(p =>
            {
                if (p is PropertyInfo propertyInfo)
                {
                    if (p.Name != "Services")
                    {
                        var value = p!.GetValue(rule);
                        if (value == null)
                            return "";
                        if (value is string s)
                            return Escape(s, _separator);
                        return Escape(value.ToString()!, _separator);
                    }
                    else
                    {
                        // Handle Services separately to join them with a pipe character

                        var services = rule.Services.Select(s => s.Content.Name).ToList();
                        return Escape(string.Join(" | ", services), _separator);
                    }
                }
                else
                {
                    return "";
                }
            });
            
            return string.Join(_separator, values);
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

        public async Task SetComplianceData() 
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
                            await SetComplianceDataForRule(currentRule, Violations);
                        }
                    }
                }
            }
        }

        private async Task SetComplianceDataForRule(Rule rule, List<ComplianceViolation> violations) // We will deal with the warning (CS1998) when we are ready for performance optimization 
        {
            rule.Violations.Clear();
            rule.ViolationDetails = "";

            foreach (var violation in violations.Where(v => v.RuleId == rule.Id))
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
