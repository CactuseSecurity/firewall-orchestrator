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
        public List<ComplianceViolation> Violations { get; set; } = [];
        public bool ShowNonImpactRules { get; set; }
        public List<Management>? Managements  { get; set; }
        protected virtual string InternalQuery => RuleQueries.getRulesWithCurrentViolationsByChunk;
        protected DebugConfig DebugConfig;

        #endregion

        #region Fields

        private List<Device>? _devices;
        private readonly int _maxDegreeOfParallelism;
        private readonly SemaphoreSlim _semaphore;
        private readonly NatRuleDisplayHtml _natRuleDisplayHtml;
        private readonly List<string> _columnsToExport;
        private readonly bool _includeHeaderInExport;
        private readonly char _separator;
        private readonly int _maxCellSize;
        private readonly int _maxPrintedViolations;
        private List<int> _relevanteManagementIDs = new();
        private readonly GlobalConfig? _globalConfig;

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
                _globalConfig = userConfig.GlobalConfig;
                _maxPrintedViolations = userConfig.GlobalConfig.ComplianceCheckMaxPrintedViolations;
            }

            // Apply debug config.

            if (userConfig.GlobalConfig != null && !string.IsNullOrEmpty(userConfig.GlobalConfig.DebugConfig))
            {
                DebugConfig = JsonSerializer.Deserialize<DebugConfig>(userConfig.GlobalConfig.DebugConfig) ?? new();
            }
            else
            {
                Log.WriteWarning("Compliance Report", "No debug config found, using default values.");
                DebugConfig = new();
            }

            if (userConfig.GlobalConfig != null && !string.IsNullOrEmpty(userConfig.GlobalConfig.ComplianceCheckRelevantManagements))
            {
                try
                {
                    _relevanteManagementIDs = userConfig.GlobalConfig.ComplianceCheckRelevantManagements
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.Parse(s.Trim()))
                        .ToList();
                }
                catch (Exception e)
                {
                    Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while parsing relevant mangement IDs: {e.Message}", DebugConfig.ExtendedLogReportGeneration);
                }
            }
        }

        public ReportCompliance(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, ReportParams reportParams) : this(query, userConfig, reportType)
        {
            ShowNonImpactRules = reportParams.ComplianceFilter.ShowNonImpactRules;
        }

        #endregion

        #region Methods - Overrides

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            // Get management and device info for resolving names.

            List<Management>? managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);

            Log.TryWriteLog(LogType.Debug, "Compliance Report", $"Fetched info for {managements?.Count() ?? 0} managements.", DebugConfig.ExtendedLogReportGeneration);

            if (managements != null)
            {
                Managements = managements;

                _devices = new();

                foreach (var management in Managements)
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

            List<Rule>[]? chunks = await GetDataParallelized<Rule>(rulesCount, elementsPerFetch, apiConnection, ct, InternalQuery);

            if (chunks != null)
            {
                RuleViewData.Clear();
                Rules = await ProcessChunksParallelized(chunks, ct, apiConnection);
                Log.TryWriteLog(LogType.Debug, "Compliance Report", $"Fetched {Rules.Count} rules for compliance report.", DebugConfig.ExtendedLogReportGeneration);
            }
            else
            {
                Log.TryWriteLog(LogType.Error, "Compliance Report", "Failed to fetch rules for compliance report.", DebugConfig.ExtendedLogReportGeneration);
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
                // Create export string.

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

                        if (!ShowNonImpactRules && !ruleViewData.Show)
                        {
                            continue;
                        }

                        sb.AppendLine(GetLineForRule(ruleViewData, properties));
                    }

                    return sb.ToString();
                }
                catch (Exception e)
                {
                    Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while exporting compliance report to CSV: {e.Message}", DebugConfig.ExtendedLogReportGeneration);
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
            List<Task<(List<Rule> processed, List<RuleViewData> viewData)>> tasks = new();

            foreach (List<Rule> chunk in chunks)
            {
                await _semaphore.WaitAsync(ct);

                Task<(List<Rule>, List<RuleViewData>)> task = Task.Run<(List<Rule>, List<RuleViewData>)>(async () =>
                {
                    List<RuleViewData> localViewData = new(chunk.Count);
                    
                    try
                    {
                        foreach (var rule in chunk)
                        {
                            await SetComplianceDataForRule(rule, apiConnection);

                            // Resolve network locations

                            NetworkLocation[] networkLocations = rule.Froms.Concat(rule.Tos).ToArray();
                            List<NetworkLocation> resolvedNetworkLocations = RuleDisplayBase.GetResolvedNetworkLocations(networkLocations);

                            // Add empty groups because display method does not get them

                            await GatherEmptyGroups(networkLocations, resolvedNetworkLocations);
                            RuleViewData ruleViewData = new RuleViewData(rule, _natRuleDisplayHtml, OutputLocation.report, ShowRule(rule), _devices ?? [], Managements ?? [], rule.Compliance);
                            localViewData.Add(ruleViewData);
                        }

                        return (chunk, localViewData);
                    }
                    catch (Exception e)
                    {
                        Log.TryWriteLog(LogType.Error, "Compliance Report", $"Failed processing chunk: {e.Message}.", DebugConfig.ExtendedLogReportGeneration);

                        return (chunk, localViewData);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, ct);

                tasks.Add(task);
            }

            (List<Rule> processed, List<RuleViewData> viewData)[]? results = await Task.WhenAll(tasks);

            return await GatherReportData(results);
        }


        #endregion

        #region Methods - Private

        private Task GatherEmptyGroups(NetworkLocation[] networkLocations, List<NetworkLocation> resolvedNetworkLocations)
        {
            foreach (NetworkLocation networkLocation in networkLocations)
            {
                foreach (GroupFlat<NetworkObject> groupFlat in networkLocation.Object.ObjectGroupFlats)
                {
                    if (groupFlat.Object != null && groupFlat.Object.Type.Name == "group" && string.IsNullOrWhiteSpace(groupFlat.Object.MemberRefs))
                    {
                        resolvedNetworkLocations.Add(new NetworkLocation(networkLocation.User, groupFlat.Object)); // adding user only for syntax
                    }
                }                               
            }

            return Task.CompletedTask;
        }

        private Task<List<Rule>> GatherReportData((List<Rule> processed, List<RuleViewData> viewData)[]? results)
        {
            RuleViewData.Capacity = results.Sum(r => r.viewData.Count);
            List<Rule> processedRulesFlat = new(results.Sum(r => r.processed.Count));

            foreach ((List<Rule> processed, List<RuleViewData> viewData) result in results)
            {
                RuleViewData.AddRange(result.viewData);
                processedRulesFlat.AddRange(result.processed);
            }

            return Task.FromResult(processedRulesFlat);
        }

        protected virtual Dictionary<string, object> CreateQueryVariables(int offset, int limit, string query)
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

            if (query.Contains("mgm_ids"))
            {
                queryVariables["mgm_ids"] = _relevanteManagementIDs;
            }

            return queryVariables;
        }

        protected virtual async Task SetComplianceDataForRule(Rule rule, ApiConnection apiConnection)
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

                    await AddViolationDataToViolationDetails(rule, violation, ref printedViolations, violationCount, ref abbreviated, true);
                }
            }
            catch (Exception e)
            {
                Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while setting compliance data for rule {rule.Id}: {e.Message}", DebugConfig.ExtendedLogReportGeneration);
                return;
            }
        }

        protected virtual Task AddViolationDataToViolationDetails(Rule rule, ComplianceViolation violation, ref int printedViolations, int violationCount, ref bool abbreviated, bool concatenateDetails)
        {
            if (concatenateDetails && _maxPrintedViolations == 0 || printedViolations < _maxPrintedViolations)
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

        protected virtual bool ShowRule(Rule rule)
        {
            bool showRule = true;

            if (rule.Compliance == ComplianceViolationType.None || rule.Action != "accept")
            {
                showRule = false;
            }

            return showRule;
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

        public override string ExportToHtml()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
