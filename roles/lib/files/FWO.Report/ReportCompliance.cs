using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Data.ViewData;
using FWO.Report.Filter;
using FWO.Ui.Display;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FWO.Report
{
    public class ReportCompliance : ReportBase
    {

        #region Properties

        public List<Rule> Rules { get; set; } = [];
        public List<RuleViewData> RuleViewData = [];
        public List<ComplianceViolation> Violations { get; set; } = [];
        public bool ShowNonImpactRules { get; set; }
        public List<Management> Managements { get; set; } = [];
        protected DebugConfig DebugConfig;
        protected readonly GlobalConfig GlobalConfig;

        #endregion

        #region Fields

        private List<Device>? _devices;
        private readonly int _maxDegreeOfParallelism;
        private readonly SemaphoreSlim _semaphore;
        private readonly NatRuleDisplayHtml _natRuleDisplayHtml;
        private List<string> _columnsToExport = [];
        private bool _includeHeaderInExport;
        private char _separator;
        private int _maxCellSize;
        private readonly int _maxPrintedViolations;
        private readonly List<int> _relevanteManagementIDs = new();

        #endregion

        #region Constructors

        public ReportCompliance(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {
            // Getting config values.

            if (userConfig.GlobalConfig != null)
            {
                GlobalConfig = userConfig.GlobalConfig;
            }
            else
            {
                GlobalConfig = new();
            }

            _maxDegreeOfParallelism = GlobalConfig.ComplianceCheckAvailableProcessors > Environment.ProcessorCount ? Environment.ProcessorCount : GlobalConfig.ComplianceCheckAvailableProcessors;
            _semaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
            _natRuleDisplayHtml = new NatRuleDisplayHtml(userConfig);

            // CSV export config.

            SetUpCsvExport();

            _maxPrintedViolations = GlobalConfig.ComplianceCheckMaxPrintedViolations;

            // Apply debug config.

            if (!string.IsNullOrEmpty(GlobalConfig.DebugConfig))
            {
                DebugConfig = JsonSerializer.Deserialize<DebugConfig>(GlobalConfig.DebugConfig) ?? new();
            }
            else
            {
                Log.WriteWarning("Compliance Report", "No debug config found, using default values.");
                DebugConfig = new();
            }

            if (!string.IsNullOrEmpty(GlobalConfig.ComplianceCheckRelevantManagements))
            {
                try
                {
                    _relevanteManagementIDs = GlobalConfig.ComplianceCheckRelevantManagements
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

            await GetManagementAndDevices(apiConnection);

            List<Rule>[]? chunks = await FetchRuleChunks(elementsPerFetch, apiConnection, ct);

            if (chunks != null)
            {
                RuleViewData.Clear();
                Rules = await ProcessChunksParallelized(chunks, ct);
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
            return await GetDataParallelized<T>(
                rulesCount,
                elementsPerFetch,
                apiConnection,
                query,
                (offset, limit) => CreateQueryVariables(offset, limit, query),
                ct);
        }

        /// <summary>
        /// Fetches a known number of records in parallel pages. The variable factory lets specialized reports page a
        /// different source table without coupling their query-specific filters to <see cref="CreateQueryVariables"/>.
        /// </summary>
        protected async Task<List<T>[]> GetDataParallelized<T>(
            int elementCount,
            int elementsPerFetch,
            ApiConnection apiConnection,
            string query,
            Func<int, int, Dictionary<string, object>> createVariables,
            CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(elementsPerFetch);

            List<Task<List<T>>> tasks = [];

            // Task.WhenAll preserves this page order, although the requests themselves run concurrently.
            for (int offset = 0; offset < elementCount; offset += elementsPerFetch)
            {
                Dictionary<string, object> variables = createVariables(offset, elementsPerFetch);
                tasks.Add(FetchDataChunk<T>(query, variables, apiConnection, ct));
            }

            return await Task.WhenAll(tasks);
        }

        public async Task<List<Rule>> ProcessChunksParallelized(List<Rule>[] chunks, CancellationToken ct)
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
                        foreach (Rule rule in chunk)
                        {
                            SetComplianceDataForRule(rule);

                            // Resolve network locations TODO: Move resolving completely to ComplianceCheck or RuleViewData

                            NetworkLocation[] networkLocations = rule.Froms.Concat(rule.Tos).ToArray();
                            List<NetworkLocation> resolvedNetworkLocations = RuleDisplayBase.GetResolvedNetworkLocations(networkLocations);

                            // Add empty groups because display method does not get them

                            await GatherEmptyGroups(networkLocations, resolvedNetworkLocations);
                            RuleViewData ruleViewData = new RuleViewData(rule, _natRuleDisplayHtml, OutputLocation.report, ShowRule(rule), _devices ?? [], Managements, rule.Compliance);
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

        public async Task GetManagementAndDevices(ApiConnection apiConnection)
        {
            // Get management and device info for resolving names.

            List<Management>? managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);

            Log.TryWriteLog(LogType.Debug, "Compliance Report", $"Fetched info for {managements?.Count() ?? 0} managements.", DebugConfig.ExtendedLogReportGeneration);

            if (managements != null)
            {
                Managements = managements.Where(m => _relevanteManagementIDs.Count == 0 || _relevanteManagementIDs.Contains(m.Id)).ToList(); // filter managements by relevant managements config value

                _devices = new();

                foreach (var management in Managements)
                {
                    if (management.Devices != null && management.Devices.Length > 0)
                    {
                        _devices.AddRange(management.Devices);
                    }
                }
            }
        }

        public void GetViewDataFromRules(List<Rule> rules)
        {
            RuleViewData.Clear();

            for (int i = 0; i < rules.Count; i++)
            {
                Rule rule = rules.ElementAt(i);

                ComplianceViolationType ruleCompliance = ComplianceViolationType.None;

                if (rule.Violations.Count > 0)
                {
                    if (rule.Violations.Any(violation => violation.Type == ComplianceViolationType.NotAssessable))
                    {
                        ruleCompliance = ComplianceViolationType.NotAssessable;
                    }
                    else if (rule.Violations.Count == 1)
                    {
                        // TODO: implement

                        ruleCompliance = ComplianceViolationType.MultipleViolations;
                    }
                    else
                    {
                        ruleCompliance = ComplianceViolationType.MultipleViolations;
                    }
                }

                rule.Compliance = ruleCompliance;

                RuleViewData ruleViewData = new RuleViewData(rule, _natRuleDisplayHtml, OutputLocation.report, ShowRule(rule), _devices ?? [], Managements, ruleCompliance);
                RuleViewData.Add(ruleViewData);
            }

        }


        #endregion

        #region Methods - Private

        /// <summary>
        /// Fetches the rule chunks used by a standard compliance report. Specialized reports can override this data
        /// acquisition step while sharing the same rendering and export pipeline.
        /// </summary>
        protected virtual async Task<List<Rule>[]?> FetchRuleChunks(int elementsPerFetch, ApiConnection apiConnection, CancellationToken ct)
        {
            List<int> managementIds = Managements.Select(management => management.Id).ToList();
            AggregateCount? result = await apiConnection.SendQueryAsync<AggregateCount>(
                RuleQueries.countActiveRules,
                new { mgm_ids = managementIds });
            int rulesCount = result?.Aggregate?.Count ?? 0;

            // The standard report needs every active rule, including compliant rules when the display setting requests them.
            return await GetDataParallelized<Rule>(
                rulesCount,
                elementsPerFetch,
                apiConnection,
                ct,
                RuleQueries.getRulesWithCurrentViolationsByChunk);
        }

        /// <summary>
        /// Executes one API page while enforcing the report-wide request concurrency limit.
        /// </summary>
        private async Task<List<T>> FetchDataChunk<T>(string query, Dictionary<string, object> variables, ApiConnection apiConnection, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                return await apiConnection.SendQueryAsync<List<T>>(query, variables);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void SetUpCsvExport()
        {
            _includeHeaderInExport = true;
            _separator = ';';
            _maxCellSize = 32000; // Max size of a cell in Excel is 32,767 characters.
            _columnsToExport =
            [
                "MgmtId",
                "MgmtName",
                "Uid",
                "Name",
                "Source"
            ];
            if (GlobalConfig.ShowShortColumnsInComplianceReports)
            {
                _columnsToExport.Add("SourceShort");
            }
            _columnsToExport.Add("Destination");
            if (GlobalConfig.ShowShortColumnsInComplianceReports)
            {
                _columnsToExport.Add("DestinationShort");
            }
            _columnsToExport.Add("Services");
            if (GlobalConfig.ShowShortColumnsInComplianceReports)
            {
                _columnsToExport.Add("ServicesShort");
            }
            _columnsToExport.AddRange(
            [
                "Action",
                "InstallOn",
                "Compliance",
                "ViolationDetails",
                "ChangeID",
                "AdoITID",
                "Comment",
                "LastModified",
                "ExpirationTime",
                "RulebaseId",
                "RulebaseName",
                "Enabled"
            ]);
        }

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
            if (results == null)
            {
                results = [];
            }
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
                List<int> managementIds = _relevanteManagementIDs;
                if (managementIds.Count == 0)
                {
                    managementIds = Managements.Select(mgmt => mgmt.Id).ToList();
                }
                queryVariables["mgm_ids"] = managementIds;
            }

            return queryVariables;
        }

        protected virtual void SetComplianceDataForRule(Rule rule, Func<ComplianceViolation, string>? formatter = null)
        {
            try
            {
                rule.ViolationDetails = "";
                rule.Compliance = ComplianceViolationType.None;
                int addedViolationDetails = 0;

                // If rule is not assessable only display assessability issues in details.

                List<ComplianceViolation> violations = SelectDecisiveViolations(rule.Violations);

                rule.Compliance = DetermineCompliance(violations);

                foreach (ComplianceViolation violation in violations)
                {
                    // Cut violation details when printed violations limit is reached.

                    if (_maxPrintedViolations > 0 && addedViolationDetails == _maxPrintedViolations)
                    {
                        rule.ViolationDetails += $"<br>Too many violations to display ({rule.Violations.Count}), please check the system for details.";
                        return;
                    }

                    // Make line breaks in violation details between violations.

                    if (rule.ViolationDetails != "")
                    {
                        rule.ViolationDetails += "<br>";
                    }

                    // Add to violation details.

                    string violationDetails = violation.Details;

                    if (formatter != null)
                    {
                        violationDetails = formatter(violation);
                    }

                    rule.ViolationDetails += violationDetails;
                    addedViolationDetails++;
                }
            }
            catch (Exception e)
            {
                Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while setting compliance data for rule {rule.Id}: {e.Message}", DebugConfig.ExtendedLogReportGeneration);
                return;
            }
        }

        /// <summary>
        /// Determines compliance using the same precedence and violation-detail limit as the report formatter.
        /// Accepts either a rule's full violation list or the decisive subset; both yield the same state.
        /// </summary>
        /// <param name="violations">Violations to judge.</param>
        /// <returns>The single state that represents the given violations.</returns>
        protected ComplianceViolationType DetermineCompliance(List<ComplianceViolation> violations)
        {
            // Assessability outranks every other type: several assessability issues still read as not assessable
            // rather than as multiple violations, so this cannot be folded into the count below.

            if (violations.Any(IsNotAssessable))
            {
                return ComplianceViolationType.NotAssessable;
            }

            int processedViolationCount = _maxPrintedViolations > 0
                ? Math.Min(violations.Count, _maxPrintedViolations)
                : violations.Count;

            return processedViolationCount switch
            {
                0 => ComplianceViolationType.None,
                1 => violations[0].Type,
                _ => ComplianceViolationType.MultipleViolations
            };
        }

        /// <summary>
        /// Selects the violations a rule is judged and rendered by. An unassessable rule is represented by its
        /// assessability issues alone, because no other criterion can be evaluated for it.
        /// </summary>
        /// <param name="violations">All violations attached to the rule.</param>
        /// <returns>The decisive subset, or all violations when the rule is assessable.</returns>
        private static List<ComplianceViolation> SelectDecisiveViolations(List<ComplianceViolation> violations)
        {
            return violations.Any(IsNotAssessable)
                ? violations.Where(IsNotAssessable).ToList()
                : violations.ToList();
        }

        /// <summary>
        /// Single definition of what marks a violation as an assessability issue.
        /// </summary>
        /// <param name="violation">Violation to classify.</param>
        /// <returns>True when the violation reports that the rule cannot be assessed.</returns>
        private static bool IsNotAssessable(ComplianceViolation violation)
        {
            return violation.Type == ComplianceViolationType.NotAssessable;
        }

        protected virtual bool ShowRule(Rule rule)
        {
            bool showRule = true;

            if (rule.Compliance == ComplianceViolationType.None || rule.Action != RuleActions.Accept)
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
                        return TransformHtmlToCsv(p.Name, str);
                    }
                }

                return "";
            });

            return string.Join(_separator, values.Select(value => $"\"{value}\""));
        }

        private string TransformHtmlToCsv(string propertyName, string htmlInput)
        {
            if (propertyName == "Enabled")
            {
                if (htmlInput.Contains(Icons.Check))
                {
                    htmlInput = "TRUE";
                }
                else
                {
                    htmlInput = "FALSE";
                }
            }

            htmlInput = htmlInput
                    .Replace("\r\n", " | ")
                    .Replace("\n", " | ")
                    .Replace("<br>", " | ");

            if (htmlInput.Length > _maxCellSize)
            {
                htmlInput = htmlInput.Substring(0, _maxCellSize) + " ... (truncated, original length: " + htmlInput.Length + " characters)";
            }

            return htmlInput;
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
