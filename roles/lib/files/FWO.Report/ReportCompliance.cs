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
using FWO.Report.Data.ViewData;
using FWO.Ui.Display;

namespace FWO.Report
{
    public class ReportCompliance : ReportBase
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
        private List<Management>? _managements;
        private List<Device>? _devices;
        private readonly List<string> _columnsToExport;
        private readonly bool _includeHeaderInExport;
        private readonly char _separator;
        private readonly int _maxCellSize;
        private readonly DebugConfig _debugConfig;


        #endregion

        #region Constructors

        public ReportCompliance(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {
            _maxDegreeOfParallelism = Environment.ProcessorCount;
            _semaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
            _natRuleDisplayHtml = new NatRuleDisplayHtml(userConfig);

            _includeHeaderInExport = true;
            _separator = ';';
            _maxCellSize = 32000; // Max size of a cell in Excel is 32,767 characters.
            _columnsToExport = new List<string>
            {
                "MgmtId",
                "MgmtName",
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
                "RulebaseId",
                "RulebaseName"
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
        
        public ReportCompliance(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, ReportParams reportParams) : this(query, userConfig, reportType)
        {
            IsDiffReport = reportParams.ComplianceFilter.IsDiffReport;
            DiffReferenceInDays = reportParams.ComplianceFilter.DiffReferenceInDays;
        }


        #endregion

        #region Methods - Overrides

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            // Get management and device info for resolving names.

            List<Management>? managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);

            Log.TryWriteLog(LogType.Debug, "Compliance Report Prototype", $"Fetched info for {managements?.Count() ?? 0} managements.", _debugConfig.ExtendedLogReportGeneration);

            if (managements != null)
            {
                _managements = managements;

                _devices = new();

                foreach (var management in _managements)
                {
                    if (management.Devices != null && management.Devices.Length > 0)
                    {
                        _devices.AddRange(management.Devices);
                    }
                }
            }

            // Get amount of rules to fetch.

            AggregateCount? result = await apiConnection.SendQueryAsync<AggregateCount>(RuleQueries.countRules);
            int rulesCount = result?.Aggregate?.Count ?? 0;

            // Get data parallelized.

            string query = IsDiffReport ? RuleQueries.getRulesWithViolationsInTimespanByChunk : RuleQueries.getRulesWithCurrentViolationsByChunk;

            List<Rule>[]? chunks = await GetDataParallelized<Rule>(rulesCount, elementsPerFetch, apiConnection, ct, query);


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
        
        public override string SetDescription()
        {
            return "Compliance Report";
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

        public async Task<List<T>[]?> GetDataParallelized<T>(int rulesCount, int elementsPerFetch, ApiConnection apiConnection, CancellationToken ct, string query)
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

        public async Task<List<Rule>> ProcessChunksParallelized(List<Rule>[] chunks, CancellationToken ct)
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
                            RuleViewData.Add(new RuleViewData(rule, _natRuleDisplayHtml, OutputLocation.report, ShowRule(rule), _devices ?? [], _managements ?? []));
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

        #endregion
        
        #region Methods - Private

        private Dictionary<string, object> CreateQueryVariables(int offset, int limit, string query)
        {
            Dictionary<string, object> queryVariables = new();

            if (query.Contains(QueryVar.ImportIdStart))
            {
                queryVariables[QueryVar.ImportIdStart] = int.MaxValue;
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
                        if (IsDiffReport)
                        {
                            if (ViolationIsRelevantForDiff(violation))
                            {
                                await TransformViolationDetailsToDiff(violation);
                            }
                            else
                            {
                                continue; // Skip violations that are not relevant for the diff report
                            }
                        }

                        if (rule.ViolationDetails != "")
                        {
                            rule.ViolationDetails += "\n";
                        }

                        rule.ViolationDetails += violation.Details;

                        // No need to differentiate between different types of violations here at the moment.

                        rule.Compliance = ComplianceViolationType.MultipleViolations;
                    }

                    return;
                }

                // If the rule is not evaluable, set compliance to NotEvaluable.

                rule.Compliance = ComplianceViolationType.NotEvaluable;
            }
            catch (System.Exception e)
            {
                Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while setting compliance data for rule {rule.Id}: {e.Message}", _debugConfig.ExtendedLogReportGeneration);
                return;
            }
        }

        private bool ViolationIsRelevantForDiff(ComplianceViolation violation)
        {
            return violation.FoundDate > DateTime.Now.AddDays(-DiffReferenceInDays)
                || (violation.RemovedDate != null
                && violation.RemovedDate > DateTime.Now.AddDays(-DiffReferenceInDays));
        }

        public Task TransformViolationDetailsToDiff(ComplianceViolation violation)
        {
            DateTime referenceDate = DateTime.Now.AddDays(-DiffReferenceInDays);

            string diffPrefix = "";

            if (violation.FoundDate >= referenceDate)
            {
                diffPrefix = $"+ ({violation.FoundDate:dd.MM.yyyy - hh:mm}) ";
            }
            if (violation.RemovedDate != null && violation.RemovedDate >= referenceDate)
            {
                diffPrefix += $"- ({violation.RemovedDate:dd.MM.yyyy - hh:mm}) ";
            }

            violation.Details = $"{diffPrefix}: {violation.Details}";

            return Task.CompletedTask;
        }

        private bool ShowRule(Rule rule)
        {
            if (rule.Compliance == ComplianceViolationType.None)
            {
                return false;
            }

            return true;
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
                        str = str
                                .Replace("\r\n", " | ")
                                .Replace("\n", " | ")
                                .Replace("<br>", " | ");

                        if (str.Contains('"'))
                        {
                            // Escape quotation marks

                            str = str.Replace("\"", "\"\"");
                        }

                        if (str.Length > _maxCellSize)
                        {
                            str = str.Substring(0, _maxCellSize) + " ... (truncated, original length: " + str.Length + " characters)";
                        }

                        return str;
                    }
                }

                return "";
            });

            return string.Join(_separator, values.Select(value => $"\"{value}\""));
        }

        public override string ExportToHtml()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
