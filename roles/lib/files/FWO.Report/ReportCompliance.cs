using FWO.Basics;
using System.Text;
using FWO.Report.Filter;
using FWO.Config.Api;
using FWO.Data;
using FWO.Api.Client;
using FWO.Data.Report;
using FWO.Api.Client.Queries;
using System.Reflection;
using System.Text.Json;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Basics.Comparer;

namespace FWO.Report
{
    public class ReportCompliance : ReportRules
    {
        public List<ComplianceViolation> Violations { get; set; } = [];
        Dictionary<ComplianceViolation, char> ViolationDiffs = new();
        public List<Rule> Rules { get; set; } = [];
        public bool IsDiffReport { get; set; } = false;
        public int DiffReferenceInDays { get; set; } = 0;

        private readonly bool _includeHeaderInExport;
        private readonly char _separator;
        private readonly List<string> _columnsToExport;
        private readonly DebugConfig _debugConfig;
        


        public ReportCompliance(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {
            _includeHeaderInExport = true;
            _separator = ';';
            _columnsToExport = new List<string>
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

            if (userConfig.GlobalConfig is GlobalConfig globalConfig && !string.IsNullOrEmpty(globalConfig.DebugConfig))
            {
                _debugConfig = JsonSerializer.Deserialize<DebugConfig>(globalConfig.DebugConfig) ?? new();
            }
            else
            {
                Log.WriteWarning("Compliance Report", "No debug config found, using default values.");

                _debugConfig = new();
            }

        }

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

                List<string> propertyNames = [];

                foreach (PropertyInfo? propertyInfo in properties)
                {
                    if (propertyInfo != null)
                    {
                        propertyNames.Add(propertyInfo!.Name);
                    }
                }

                if (_includeHeaderInExport)
                {
                    sb.AppendLine(string.Join(_separator, propertyNames.Select(p => $"\"{p}\"")));
                }

                foreach (Rule rule in Rules)
                {
                    sb.AppendLine(GetLineForRule(rule, properties));
                }

                return sb.ToString();
            }

            return csvString;
        }

        public override string SetDescription()
        {
            try
            {
                return base.SetDescription();
            }
            catch (Exception e)
            {
                Log.TryWriteLog(LogType.Error, "Compliance Report", "Error while setting description: " + e.Message, _debugConfig.ExtendedLogReportGeneration);
                Log.TryWriteLog(LogType.Debug, "Compliance Report", $"Report Data: {JsonSerializer.Serialize(ReportData)}", _debugConfig.ExtendedLogReportGeneration);

                return "Compliance Report";
            }
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

                        return $"{Escape(value.ToString()!, _separator)}";
                    }
                    else
                    {
                        // Handle Services separately to join them with a pipe character

                        var services = rule.Services.Select(s => s.Content.Name).ToList();
                        return $"{Escape(string.Join(" | ", services), _separator)}";
                    }
                }
                else
                {
                    return "";
                }
            });

            return string.Join(_separator, values.Select(value => $"\"{value}\""));

        }

        private string Escape(string input, char separator)
        {
            // Replace line breaks with space

            input = input.Replace("\r", " ").Replace("\n", " ");

            if (input.Contains('"'))
            {
                // Escape quotation marks

                input = input.Replace("\"", "\"\"");
            }

            return input;
        }

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            var baseTask = base.Generate(rulesPerFetch, apiConnection, callback, ct);
            var violationsTask = apiConnection.SendQueryAsync<List<ComplianceViolation>>(ComplianceQueries.getViolations); // TODO: move in DynQuery

            await Task.WhenAll(baseTask, violationsTask);

            List<ComplianceViolation> violationsTaskResult = violationsTask.Result;
            Violations = violationsTaskResult.Where(v => v.RemovedDate == null).ToList();
            
            if (IsDiffReport && DiffReferenceInDays > 0)
            {
                ViolationDiffs = await GetViolationDiffs(violationsTaskResult);
                Violations = ViolationDiffs.Keys.ToList();
            }

            await SetComplianceData();
        }
        
        public async Task<Dictionary<ComplianceViolation, char>> GetViolationDiffs(List<ComplianceViolation> allViolations)
        {
                DateTime referenceDate = DateTime.Now.AddDays(-DiffReferenceInDays);

                Dictionary<ComplianceViolation, char> violationDiffs = new();
                ComplianceViolationComparer comparer = new();

                List<ComplianceViolation> removedViolations = allViolations
                                                                .Where(violation => violation.RemovedDate is DateTime removedDate && removedDate >= referenceDate)
                                                                .Cast<ComplianceViolation>()
                                                                .ToList();

                List<ComplianceViolation> addedViolations = allViolations
                                                                .Where(violation => violation.FoundDate >= referenceDate)
                                                                .Cast<ComplianceViolation>()
                                                                .ToList();

                foreach (var v in removedViolations)
                {
                    violationDiffs[v] = '-';
                }

                foreach (var v in addedViolations)
                {
                    violationDiffs[v] = '+';                    
                }

                return violationDiffs;    
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
                if (IsDiffReport && ViolationDiffs.TryGetValue(violation, out char changeSign))
                {
                    violation.Details = $"({changeSign}) {violation.Details}";
                }
                
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
