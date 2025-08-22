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
using FWO.Report.Data.ViewData;
using FWO.Ui.Display;

namespace FWO.Report
{
    public class ReportCompliance : ReportRules
    {

        #region Properties
        
        public List<Rule> Rules { get; set; } = [];
        public List<RuleViewData> RuleViewData = [];
        public bool IsDiffReport { get; set; } = false;
        public Dictionary<ComplianceViolation, char> ViolationDiffs = new();
        public List<ComplianceViolation> Violations { get; set; } = [];
        public int DiffReferenceInDays { get; set; } = 0;

        #endregion

        #region Fields

        private readonly int _maxDegreeOfParallelism;
        private readonly SemaphoreSlim _semaphore;
        private readonly NatRuleDisplayHtml _natRuleDisplayHtml;
        private List<Device>? _devices;
        private readonly List<string> _columnsToExport;
        private readonly bool _includeHeaderInExport;
        private readonly char _separator;
        private readonly DebugConfig _debugConfig;


        #endregion

        #region Constructor

        public ReportCompliance(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {
            _maxDegreeOfParallelism = Environment.ProcessorCount;
            _semaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
            _natRuleDisplayHtml = new NatRuleDisplayHtml(userConfig);

            _includeHeaderInExport = true;
            _separator = ';';
            _columnsToExport = new List<string>
            {
                "MgmtId",
                "Uid",
                "Name",
                "Source",
                "Destination",
                "Services",
                "Action",
                "InstallOn",
                "Compliance",
                "ViolationDetails",
                "ChangeID",
                "AdoITID",
                "Comment",
                "RulebaseId"
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

        #endregion

        #region Methods - Overrides

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            List<Device>? devices =  await apiConnection.SendQueryAsync<List<Device>>(DeviceQueries.getDeviceDetails);

            Log.TryWriteLog(LogType.Debug, "Compliance Report Prototype", $"Fetched {devices?.Count() ?? 0} devices.", _debugConfig.ExtendedLogReportGeneration);

            if (devices != null)
            {
                _devices = devices;
            }

            // Get amount of rules to fetch.

            AggregateCount? result = await apiConnection.SendQueryAsync<AggregateCount>(RuleQueries.countRules);
            int rulesCount = result?.Aggregate?.Count ?? 0;

            // Get data parallelized.

            List<Rule>[]? chunks = await GetDataParallelized<Rule>(rulesCount, rulesPerFetch, apiConnection, ct, RuleQueries.getRulesWithViolationsByChunk);


            if (chunks != null)
            {
                RuleViewData.Clear();
                Rules = await ProcessChunksParallelized(chunks, ct);
                Log.TryWriteLog(LogType.Debug, "Compliance Report Prototype", $"Fetched {Rules.Count} rules for compliance report.", _debugConfig.ExtendedLogReportGeneration);
            }
            else
            {
                Log.TryWriteLog(LogType.Error, "Compliance Report Prototype", "Failed to fetch rules for compliance report.", _debugConfig.ExtendedLogReportGeneration);
                return;
            }

            // Set report data.

            ReportData.RuleViewData = RuleViewData;
            ReportData.RulesFlat = Rules;
            ReportData.ElementsCount = Rules.Count;
        }

        public override string ExportToJson()
        {
            return JsonSerializer.Serialize(ReportData.RuleViewData, new JsonSerializerOptions { WriteIndented = true });
        }

        public override string ExportToCsv()
        {
            string csvString = "";

            if (RuleViewData.Count > 0)
            {
                // Create export string

                try
                {
                    StringBuilder sb = new StringBuilder();
                    Type type = typeof(RuleViewData);
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

                    foreach (RuleViewData ruleViewData in RuleViewData)
                    {
                        sb.AppendLine(GetLineForRule(ruleViewData, properties));
                    }

                    return sb.ToString();
                }
                catch (System.Exception e)
                {
                    Log.TryWriteLog(LogType.Error, "Compliance Report Prototype", $"Error while exporting compliance report to CSV: {e.Message}", _debugConfig.ExtendedLogReportGeneration);
                }
            }

            return csvString;
        }

        private string GetLineForRule(RuleViewData rule, List<PropertyInfo?> properties)
        {
            IEnumerable<string> values = properties.Select(p =>
            {
                if (p is PropertyInfo propertyInfo)
                {
                    object? value = propertyInfo.GetValue(rule);

                    if (value is string str)
                    {
                        str = str.Replace("\r", " ").Replace("\n", " ");

                        if (str.Contains('"'))
                        {
                            // Escape quotation marks

                            str = str.Replace("\"", "\"\"");
                        }

                        return str;
                    }   
                }

                return "";
            });

            return string.Join(_separator, values.Select(value => $"\"{value}\""));
        }

        #endregion

        #region Methods - Public

        public Task<bool> CheckEvaluability(Rule rule)
        {
            string internetZoneObjectUid = "";

            if (userConfig.GlobalConfig is GlobalConfig globalConfig)
            {
                internetZoneObjectUid = globalConfig.ComplianceCheckInternetZoneObject;
            }

            if (internetZoneObjectUid != "")
            {
                return Task.FromResult(!(rule.Froms.Any(from => from.Object.Uid == internetZoneObjectUid) || rule.Tos.Any(to => to.Object.Uid == internetZoneObjectUid)));
            }
            else
            {
                return Task.FromResult(true);
            }
        }

        #endregion

        #region Methods - Private

        private async Task<List<T>[]?> GetDataParallelized<T>(int rulesCount, int elementsPerFetch, ApiConnection apiConnection, CancellationToken ct, string query)
        {
            List<Task<List<T>>> tasks = new();
            List<Dictionary<string, object>> queryVariablesList = new();

            // Create query variables for fetching rules

            for (int offset = 0; offset < rulesCount; offset += elementsPerFetch)
            {
                queryVariablesList.Add(CreateQueryVariables(offset, elementsPerFetch, query));
            }

            // Start fetching tasks

            foreach (Dictionary<string, object> queryVariables in queryVariablesList)
            {
                await _semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        return await apiConnection.SendQueryAsync<List<T>>(query, queryVariables);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, ct);

                tasks.Add(task);
            }

            // Wait for all tasks to complete and return fetched rules in chunks

            return await Task.WhenAll(tasks);
        }

        private async Task<List<Rule>> ProcessChunksParallelized(List<Rule>[] chunks, CancellationToken ct)
        {
            List<Task<List<Rule>>> tasks = new();

            // Start chunk processing tasks

            foreach (List<Rule> chunk in chunks)
            {
                await _semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        foreach (Rule rule in chunk)
                        {
                            await SetComplianceDataForRule(rule);
                            RuleViewData.Add(new RuleViewData(rule, _natRuleDisplayHtml, OutputLocation.report, ShowRule(rule), _devices ?? []));
                        }

                        return chunk;
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, ct);

                tasks.Add(task);

            }

            // Wait for all tasks to complete and return processed rules

            List<Rule>[] processedRules = await Task.WhenAll(tasks);

            List<Rule> processedRulesFlat = new();

            foreach (List<Rule> processedRulesChunk in processedRules)
            {
                processedRulesFlat.AddRange(processedRulesChunk);
            }

            return processedRulesFlat;
        }

        private Dictionary<string, object> CreateQueryVariables(int offset, int limit, string query)
        {
            Dictionary<string, object> queryVariables = new();

            if (query.Contains(QueryVar.ImportIdStart))
            {
                queryVariables[QueryVar.ImportIdStart] = 0;
            }

            if (query.Contains(QueryVar.ImportIdEnd))
            {
                queryVariables[QueryVar.ImportIdEnd] = int.MaxValue;
            }

            if (query.Contains(QueryVar.Offset))
            {
                queryVariables[QueryVar.Offset] = offset;
            }

            if (query.Contains(QueryVar.Limit))
            {
                queryVariables[QueryVar.Limit] = limit;
            }

            return queryVariables;
        }

        private async Task SetComplianceDataForRule(Rule rule)
        {
            try
            {
                rule.ViolationDetails = "";
                rule.Compliance = ComplianceViolationType.None;

                if (await CheckEvaluability(rule))
                {
                    foreach (var violation in rule.Violations)
                    {
                        if (IsDiffReport && ViolationDiffs.TryGetValue(violation, out char changeSign))
                        {
                            violation.Details = $"({changeSign}) {violation.Details}";
                        }

                        if (rule.ViolationDetails != "")
                        {
                            rule.ViolationDetails += "\n";
                        }

                        rule.ViolationDetails += violation.Details;

                        // No need to differentiate between different types of violations here at the moment.

                        rule.Compliance = ComplianceViolationType.MultipleViolations;
                    }
                }
                else
                {
                    rule.Compliance = ComplianceViolationType.NotEvaluable;
                }
            }
            catch (System.Exception e)
            {
                Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while setting compliance data for rule {rule.Id}: {e.Message}", _debugConfig.ExtendedLogReportGeneration);
                return;
            }

        }
        
        private bool ShowRule(Rule rule)
        {
            if (rule.Compliance == ComplianceViolationType.None)
            {
                return false;
            }

            return true;
        }
        
        #endregion








        // public List<ComplianceViolation> Violations { get; set; } = [];
        // public Dictionary<ComplianceViolation, char> ViolationDiffs = new();
        // public List<Rule> Rules { get; set; } = [];
        // public bool IsDiffReport { get; set; } = false;
        // public int DiffReferenceInDays { get; set; } = 0;

        // protected readonly DebugConfig DebugConfig;

        // private readonly bool _includeHeaderInExport;
        // private readonly char _separator;
        // private readonly List<string> _columnsToExport;




        // public ReportCompliance(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        // {
        //     _includeHeaderInExport = true;
        //     _separator = ';';
        //     _columnsToExport = new List<string>
        //     {
        //         "MgmtId",
        //         "Uid",
        //         "Name",
        //         "Source",
        //         "Destination",
        //         "Services",
        //         "Action",
        //         "InstallOn",
        //         "Compliance",
        //         "ViolationDetails",
        //         "ChangeID",
        //         "AdoITID",
        //         "Comment"
        //     };

        //     if (userConfig.GlobalConfig is GlobalConfig globalConfig && !string.IsNullOrEmpty(globalConfig.DebugConfig))
        //     {
        //         DebugConfig = JsonSerializer.Deserialize<DebugConfig>(globalConfig.DebugConfig) ?? new();
        //     }
        //     else
        //     {
        //         Log.WriteWarning("Compliance Report", "No debug config found, using default values.");

        //         DebugConfig = new();
        //     }

        // }

        // public override string ExportToCsv()
        // {
        //     string csvString = "";

        //     if (Rules.Count > 0)
        //     {
        //         // Create export string


        //         StringBuilder sb = new StringBuilder();
        //         Type type = typeof(Rule);
        //         List<PropertyInfo?> properties = _columnsToExport
        //                                             .Select(name => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance))
        //                                             .Where(p => p != null)
        //                                             .ToList();

        //         List<string> propertyNames = [];

        //         foreach (PropertyInfo? propertyInfo in properties)
        //         {
        //             if (propertyInfo != null)
        //             {
        //                 propertyNames.Add(propertyInfo!.Name);
        //             }
        //         }

        //         if (_includeHeaderInExport)
        //         {
        //             sb.AppendLine(string.Join(_separator, propertyNames.Select(p => $"\"{p}\"")));
        //         }

        //         foreach (Rule rule in Rules)
        //         {
        //             sb.AppendLine(GetLineForRule(rule, properties));
        //         }

        //         return sb.ToString();
        //     }

        //     return csvString;
        // }

        // public override string SetDescription()
        // {
        //     try
        //     {
        //         return base.SetDescription();
        //     }
        //     catch (Exception e)
        //     {
        //         Log.TryWriteLog(LogType.Error, "Compliance Report", "Error while setting description: " + e.Message, DebugConfig.ExtendedLogReportGeneration);
        //         Log.TryWriteLog(LogType.Debug, "Compliance Report", $"Report Data: {JsonSerializer.Serialize(ReportData)}", DebugConfig.ExtendedLogReportGeneration);

        //         return "Compliance Report";
        //     }
        // }

        // private string GetLineForRule(Rule rule, List<PropertyInfo?> properties)
        // {
        //     IEnumerable<string> values = properties.Select(p =>
        //     {
        //         if (p is PropertyInfo propertyInfo)
        //         {
        //             return SerializeProperty(propertyInfo, rule);
        //         }

        //         return "";
        //     });

        //     return string.Join(_separator, values.Select(value => $"\"{value}\""));
        // }

        // private string SerializeProperty(PropertyInfo propertyInfo, Rule rule)
        // {
        //     switch (propertyInfo.Name)
        //     {
        //         case "Services":
        //             var services = rule.Services.Select(s => s.Content.Name).ToList();
        //             return Escape(string.Join(" | ", services), _separator);

        //         case "Compliance":
        //             return rule.Compliance switch
        //             {
        //                 ComplianceViolationType.NotEvaluable => "NOT EVALUABLE",
        //                 ComplianceViolationType.None        => "TRUE",
        //                 _                                    => "FALSE"
        //             };

        //         case "ChangeID":
        //             return GetFromCustomField(rule, "field-2");

        //         case "AdoITID":
        //             return GetFromCustomField(rule, "field-3");

        //         default:
        //             var value = propertyInfo.GetValue(rule);

        //             if (value == null)
        //                 return "";

        //             if (value is string s)
        //                 return Escape(s, _separator);

        //             return Escape(value.ToString()!, _separator);
        //     }
        // }

        // private string GetFromCustomField(Rule rule, string field)
        // {
        //     string customFieldsString = rule.CustomFields.Replace("'", "\"");
        //     Dictionary<string, string>? customFields = JsonSerializer.Deserialize<Dictionary<string, string>>(customFieldsString);
        //     return customFields != null && customFields.TryGetValue(field, out string? value) ? value : "";
        // }

        // private string Escape(string input, char separator)
        // {
        //     // Replace line breaks with space

        //     input = input.Replace("\r", " ").Replace("\n", " ");

        //     if (input.Contains('"'))
        //     {
        //         // Escape quotation marks

        //         input = input.Replace("\"", "\"\"");
        //     }

        //     return input;
        // }

        // public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        // {
        //     var baseTask = base.Generate(rulesPerFetch, apiConnection, callback, ct);
        //     var violationsTask = apiConnection.SendQueryAsync<List<ComplianceViolation>>(ComplianceQueries.getViolations); // TODO: move in DynQuery

        //     await Task.WhenAll(baseTask, violationsTask);

        //     List<ComplianceViolation> violationsTaskResult = violationsTask.Result;
        //     Violations = violationsTaskResult.Where(v => v.RemovedDate == null).ToList();

        //     if (IsDiffReport && DiffReferenceInDays > 0)
        //     {
        //         ViolationDiffs = await GetViolationDiffs(violationsTaskResult);
        //         Violations = ViolationDiffs.Keys.ToList();
        //     }

        //     await SetComplianceData();
        // }

        // public async Task<Dictionary<ComplianceViolation, char>> GetViolationDiffs(List<ComplianceViolation> allViolations)
        // {
        //     DateTime referenceDate = DateTime.Now.AddDays(-DiffReferenceInDays);

        //     Dictionary<ComplianceViolation, char> violationDiffs = new();
        //     ComplianceViolationComparer comparer = new();

        //     List<ComplianceViolation> removedViolations = allViolations
        //                                                     .Where(violation => violation.RemovedDate is DateTime removedDate && removedDate >= referenceDate)
        //                                                     .Cast<ComplianceViolation>()
        //                                                     .ToList();

        //     List<ComplianceViolation> addedViolations = allViolations
        //                                                     .Where(violation => violation.FoundDate >= referenceDate)
        //                                                     .Cast<ComplianceViolation>()
        //                                                     .ToList();

        //     foreach (var v in removedViolations)
        //     {
        //         violationDiffs[v] = '-';
        //     }

        //     foreach (var v in addedViolations)
        //     {
        //         violationDiffs[v] = '+';
        //     }

        //     return violationDiffs;
        // }

        // public virtual async Task SetComplianceData()
        // {
        //     Rules.Clear();

        //     foreach (var management in ReportData.ManagementData)
        //     {
        //         foreach (var rulebase in management.Rulebases)
        //         {
        //             foreach (var rule in rulebase.Rules)
        //             {
        //                 if (rule is Rule currentRule)
        //                 {
        //                     await SetComplianceDataForRule(currentRule, Violations);
        //                 }
        //             }
        //         }
        //     }
        // }

        // protected async Task SetComplianceDataForRule(Rule rule, List<ComplianceViolation> violations)
        // {
        //     try
        //     {
        //         rule.Violations.Clear();
        //         rule.ViolationDetails = "";
        //         rule.Compliance = ComplianceViolationType.None;

        //         if (await CheckEvaluability(rule))
        //         {
        //             foreach (var violation in violations.Where(v => v.RuleId == rule.Id))
        //             {
        //                 if (IsDiffReport && ViolationDiffs.TryGetValue(violation, out char changeSign))
        //                 {
        //                     violation.Details = $"({changeSign}) {violation.Details}";
        //                 }

        //                 if (rule.ViolationDetails != "")
        //                 {
        //                     rule.ViolationDetails += "\n";
        //                 }

        //                 rule.ViolationDetails += violation.Details;
        //                 rule.Violations.Add(violation);

        //                 // No need to differentiate between different types of violations here at the moment.

        //                 rule.Compliance = ComplianceViolationType.MultipleViolations;
        //             }                
        //         }
        //         else
        //         {
        //             rule.Compliance = ComplianceViolationType.NotEvaluable;
        //         }

        //         if (!Rules.Contains(rule))
        //         {
        //             Rules.Add(rule);
        //         }
        //     }
        //     catch (System.Exception e)
        //     {
        //         Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while setting compliance data for rule {rule.Id}: {e.Message}", DebugConfig.ExtendedLogReportGeneration);
        //         return;
        //     }

        // }

        // public Task<bool> CheckEvaluability(Rule rule)
        // {
        //     string internetZoneObjectUid = "";

        //     if (userConfig.GlobalConfig is GlobalConfig globalConfig)
        //     {
        //         internetZoneObjectUid = globalConfig.ComplianceCheckInternetZoneObject;
        //     }

        //     if (internetZoneObjectUid != "")
        //     {
        //         return Task.FromResult(!(rule.Froms.Any(from => from.Object.Uid == internetZoneObjectUid) || rule.Tos.Any(to => to.Object.Uid == internetZoneObjectUid)));
        //     }
        //     else
        //     {
        //         return Task.FromResult(true);
        //     }


        // }
    }
}
