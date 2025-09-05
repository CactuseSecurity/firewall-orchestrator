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
using System.Collections.Frozen;

namespace FWO.Report
{
    public class ReportCompliance : ReportBase
    {

        #region Properties
        
        public List<Rule> Rules { get; set; } = [];
        public FrozenDictionary<Rule, List<NetworkObject>>? RuleNetworkObjectMap { get; set; }
        public List<RuleViewData> RuleViewData = [];
        public bool IsDiffReport { get; set; } = false;
        public Dictionary<ComplianceViolation, char> ViolationDiffs = new();
        public List<ComplianceViolation> Violations { get; set; } = [];
        public int DiffReferenceInDays { get; set; } = 0;
        public bool ShowAllRules { get; set; }

        #endregion

        #region Fields

        private List<Management>? _managements;
        private List<Device>? _devices;

        private readonly int _maxDegreeOfParallelism;
        private readonly SemaphoreSlim _semaphore;
        private readonly NatRuleDisplayHtml _natRuleDisplayHtml;
        private readonly List<string> _columnsToExport;
        private readonly bool _includeHeaderInExport;
        private readonly char _separator;
        private readonly DebugConfig _debugConfig;
        private readonly int _maxCellSize;
        private readonly int _maxPrintedViolations;


        #endregion

        #region Constructors

        public ReportCompliance(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {
            _maxDegreeOfParallelism = Environment.ProcessorCount;
            _semaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
            _natRuleDisplayHtml = new NatRuleDisplayHtml(userConfig);

            // CSV export config.

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

            if (userConfig.GlobalConfig != null)
            {
                _maxPrintedViolations = userConfig.GlobalConfig.ComplianceCheckMaxPrintedViolations;
            }

            // Apply debug config.

            if (userConfig.GlobalConfig != null && !string.IsNullOrEmpty(userConfig.GlobalConfig.DebugConfig))
            {
                _debugConfig = JsonSerializer.Deserialize<DebugConfig>(userConfig.GlobalConfig.DebugConfig) ?? new();
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
            ShowAllRules = reportParams.ComplianceFilter.ShowCompliantRules;
        }


        #endregion

        #region Methods - Overrides

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            // Get management and device info for resolving names.

            List<Management>? managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);

            Log.TryWriteLog(LogType.Debug, "Compliance Report", $"Fetched info for {managements?.Count() ?? 0} managements.", _debugConfig.ExtendedLogReportGeneration);

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
                Rules = await ProcessChunksParallelized(chunks, ct, apiConnection);
                Log.TryWriteLog(LogType.Debug, "Compliance Report", $"Fetched {Rules.Count} rules for compliance report.", _debugConfig.ExtendedLogReportGeneration);
            }
            else
            {
                Log.TryWriteLog(LogType.Error, "Compliance Report", "Failed to fetch rules for compliance report.", _debugConfig.ExtendedLogReportGeneration);
                return;
            }

            // Set report data.

            ReportData.RuleViewData = RuleViewData;
            ReportData.RulesFlat = Rules;
            ReportData.ElementsCount = RuleViewData.Count;
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

                    TryAppendCsvHeader(sb, propertyNames);

                    foreach (RuleViewData ruleViewData in RuleViewData)
                    {
                        // Skip marked (i.e. compliant rules) rules if configured.

                        if (!ShowAllRules && !ruleViewData.Show)
                        {
                            continue;
                        }

                        sb.AppendLine(GetLineForRule(ruleViewData, properties));
                    }

                    return sb.ToString();
                }
                catch (Exception e)
                {
                    Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while exporting compliance report to CSV: {e.Message}", _debugConfig.ExtendedLogReportGeneration);
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

        public Task<(bool isAssessable, string violationDetails)> CheckAssessability(List<NetworkObject> networkObjects)
        {
            bool isAssessable = true;
            StringBuilder violationDetailsBuilder = new();

            isAssessable &= !TryAddNotAssessableDetails(
                networkObjects,
                n => n.IP == null && n.IpEnd == null,
                "Network objects in source or destination without IP: ",
                violationDetailsBuilder);

            isAssessable &= !TryAddNotAssessableDetails(
                networkObjects,
                n => (n.IP == "0.0.0.0/32" && n.IpEnd == "255.255.255.255/32")
                || (n.IP == "::/128" && n.IpEnd == "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128"),
                "Network objects in source or destination with 0.0.0.0/0 or ::/0: ",
                violationDetailsBuilder);

            isAssessable &= !TryAddNotAssessableDetails(
                networkObjects,
                n => n.IP == "255.255.255.255/32" && n.IpEnd == "255.255.255.255/32",
                "Network objects in source or destination with 255.255.255.255/32: ",
                violationDetailsBuilder);

            isAssessable &= !TryAddNotAssessableDetails(
                networkObjects,
                n => n.IP == "0.0.0.0/32" && n.IpEnd == "0.0.0.0/32",
                "Network objects in source or destination with 0.0.0.0/32: ",
                violationDetailsBuilder);

            return Task.FromResult((isAssessable, violationDetailsBuilder.ToString()));
        }

        private bool TryAddNotAssessableDetails(IEnumerable<NetworkObject> networkObjects, Func<NetworkObject, bool> predicate, string headerText, StringBuilder details, Func<NetworkObject, string>? itemFormatter = null)
        {
            Func<NetworkObject, string> format = itemFormatter ?? (n => n.Name);

            List<string> notAssessableDetails = networkObjects
                .Where(predicate)
                .Select(format)
                .ToList();

            if (notAssessableDetails.Count == 0)
            {
                return false;
            }

            if (details.Length > 0)
            {
                details.Append("<br>");
            }

            details.Append(headerText);
            details.Append(string.Join(",", notAssessableDetails));
                
            return true;
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

        public async Task<List<Rule>> ProcessChunksParallelized(List<Rule>[] chunks, CancellationToken ct, ApiConnection apiConnection)
        {
            List<Task<(List<Rule> processed, List<RuleViewData> viewData, Dictionary<Rule, List<NetworkObject>> networkObjects)>> tasks = new();

            foreach (List<Rule> chunk in chunks)
            {
                await _semaphore.WaitAsync(ct);

                Task<(List<Rule>, List<RuleViewData>, Dictionary<Rule, List<NetworkObject>>)> task = Task.Run<(List<Rule>, List<RuleViewData>, Dictionary<Rule, List<NetworkObject>>)>(async () =>
                {
                    List<RuleViewData> localViewData = new(chunk.Count);
                    Dictionary<Rule, List<NetworkObject>> networkObjects = new();

                    try
                    {
                        foreach (var rule in chunk)
                        {
                            await SetComplianceDataForRule(rule, apiConnection);
                            networkObjects[rule] = GetAllNetworkObjectsFromRule(rule);
                            (bool isAssessable, string violationDetails) checkAssessabilityResult = await CheckAssessability(networkObjects[rule]);
                            ComplianceViolationType complianceViolationType = checkAssessabilityResult.isAssessable ? rule.Compliance : ComplianceViolationType.NotAssessable;
                            RuleViewData ruleViewData = new RuleViewData(rule, _natRuleDisplayHtml, OutputLocation.report, ShowRule(rule), _devices ?? [], _managements ?? [], complianceViolationType);

                            if (!checkAssessabilityResult.isAssessable )
                            {
                                ruleViewData.ViolationDetails = checkAssessabilityResult.violationDetails;
                            }

                            localViewData.Add(ruleViewData);
                        }

                        return (chunk, localViewData, networkObjects);
                    }
                    catch (Exception e)
                    {
                        Log.TryWriteLog(LogType.Error, "Compliance Report", $"Failed processing chunk: {e.Message}.", _debugConfig.ExtendedLogReportGeneration);

                        return (chunk, localViewData, networkObjects);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, ct);

                tasks.Add(task);
            }

            (List<Rule> processed, List<RuleViewData> viewData, Dictionary<Rule, List<NetworkObject>> networkObjects)[]? results = await Task.WhenAll(tasks);

            // Gather results.

            RuleViewData.Capacity = results.Sum(r => r.viewData.Count);
            List<Rule> processedRulesFlat = new(results.Sum(r => r.processed.Count));
            Dictionary<Rule, List<NetworkObject>> networkObjectsDict = new();

            foreach ((List<Rule> processed, List<RuleViewData> viewData, Dictionary<Rule, List<NetworkObject>> networkObjects) result in results)
            {
                RuleViewData.AddRange(result.viewData);
                processedRulesFlat.AddRange(result.processed);

                foreach (KeyValuePair<Rule, List<NetworkObject>> kvp in result.networkObjects)
                {
                    networkObjectsDict[kvp.Key] = kvp.Value;
                }
            }

            RuleNetworkObjectMap = networkObjectsDict.ToFrozenDictionary();

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

        private async Task SetComplianceDataForRule(Rule rule, ApiConnection apiConnection)
        {
            try
            {
                rule.ViolationDetails = "";
                rule.Compliance = ComplianceViolationType.None;
                int printedViolations = 0;
                bool abbreviated = false;

                for (int violationCount = 1; violationCount <= rule.Violations.Count; violationCount++)
                {
                    ComplianceViolation violation = rule.Violations.ElementAt(violationCount - 1);

                    await AddViolationDataToViolationDetails(rule, violation, ref printedViolations, violationCount, ref abbreviated);
                }

                if (IsDiffReport)
                {
                    await PostProcessDiffReportsRule(rule, apiConnection);
                }
            }
            catch (Exception e)
            {
                Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while setting compliance data for rule {rule.Id}: {e.Message}", _debugConfig.ExtendedLogReportGeneration);
                return;
            }
        }

        protected virtual async Task PostProcessDiffReportsRule(Rule rule, ApiConnection apiConnection)
        {
            if (rule.ViolationDetails == "")
            {
                DateTime from = DateTime.Now.AddDays(-DiffReferenceInDays);
                rule.ViolationDetails = $"No changes between {from:dd.MM.yyyy} - {from:HH:mm} and {DateTime.Now:dd.MM.yyyy} - {DateTime.Now:HH:mm}";                            
            }


            var variables = new { ruleId = rule.Id };
            List<ComplianceViolation>? violations = await apiConnection.SendQueryAsync<List<ComplianceViolation>>(ComplianceQueries.getViolationsByRuleID, variables: variables);

            if (violations != null)
            {
                rule.Compliance = violations.Where(violation => violation.RemovedDate == null).ToList().Count > 0 ? ComplianceViolationType.MultipleViolations : ComplianceViolationType.None;
            }   
        }

        private Task AddViolationDataToViolationDetails(Rule rule, ComplianceViolation violation, ref int printedViolations, int violationCount, ref bool abbreviated)
        {
            if (IsDiffReport)
            {
                if (ViolationIsRelevantForDiff(violation))
                {
                    TransformViolationDetailsToDiff(violation);
                }
            }

            if ((_maxPrintedViolations == 0 || printedViolations < _maxPrintedViolations) && (!IsDiffReport || ViolationIsRelevantForDiff(violation)))
            {
                if (rule.ViolationDetails != "")
                {
                    rule.ViolationDetails += "<br>";
                }

                rule.ViolationDetails += violation.Details;
                printedViolations++;
            }

            // No need to differentiate between different types of violations here at the moment.

            rule.Compliance = ComplianceViolationType.MultipleViolations;

            if (_maxPrintedViolations > 0 && printedViolations == _maxPrintedViolations && violationCount < rule.Violations.Count && !abbreviated)
            {
                rule.ViolationDetails += $"<br>Too many violations to display ({rule.Violations.Count}), please check the system for details.";
                abbreviated = true;
            }

            return Task.CompletedTask;
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
                diffPrefix = $"Found: ({violation.FoundDate:dd.MM.yyyy - hh:mm}) ";
            }
            if (violation.RemovedDate != null && violation.RemovedDate >= referenceDate)
            {
                diffPrefix += $"Removed: ({violation.RemovedDate:dd.MM.yyyy - hh:mm}) ";
            }

            violation.Details = $"{diffPrefix}: {violation.Details}";

            return Task.CompletedTask;
        }

        private bool ShowRule(Rule rule)
        {
            if (rule.Compliance == ComplianceViolationType.None || (rule.Action != "accept" && rule.Action != "ipsec"))
            {
                return false;
            }

            if (IsDiffReport && (rule.ViolationDetails.StartsWith("No changes") || rule.Disabled))
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

        private void TryAppendCsvHeader(StringBuilder sb, List<string> propertyNames)
        {
            if (_includeHeaderInExport)
            {
                sb.AppendLine(string.Join(_separator, propertyNames.Select(p => $"\"{p}\"")));
            }
        }

        private List<NetworkObject> GetAllNetworkObjectsFromRule(Rule rule)
        {
            HashSet<NetworkObject> allObjects = [];

            foreach (NetworkLocation networkLocation in rule.Tos.Concat(rule.Froms).ToList())
            {
                if (networkLocation.Object.ObjectGroupFlats.Any())
                {
                    foreach (GroupFlat<NetworkObject> groupFlat in networkLocation.Object.ObjectGroupFlats)
                    {
                        if (groupFlat.Object != null)
                        {
                            allObjects.Add(groupFlat.Object);
                        }
                    }
                }
                else
                {
                    allObjects.Add(networkLocation.Object);
                }
            }

            return allObjects.ToList();
        }

        public override string ExportToHtml()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
