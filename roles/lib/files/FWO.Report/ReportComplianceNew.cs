using System.Reflection;
using System.Text;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Data.ViewData;
using FWO.Report.Filter;
using FWO.Ui.Display;

namespace FWO.Report
{
    public class ReportComplianceNew : ReportCompliance
    {
        #region Properties

        public List<RuleViewData> RuleViewData = [];

        #endregion

        #region Fields

        private readonly int _maxDegreeOfParallelism;
        private readonly SemaphoreSlim _semaphore;
        private readonly NatRuleDisplayHtml _natRuleDisplayHtml;
        private List<Device>? _devices;
        private readonly List<string> _columnsToExport;
        private readonly bool _includeHeaderInExport;
        private readonly char _separator;


        #endregion

        #region Constructor

        public ReportComplianceNew(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {
            _maxDegreeOfParallelism = Environment.ProcessorCount;
            _semaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
            _natRuleDisplayHtml = new NatRuleDisplayHtml(userConfig);
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
            _includeHeaderInExport = true;
            _separator = ';';
        }

        #endregion

        #region Methods - Overrides

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            List<Device>? devices =  await apiConnection.SendQueryAsync<List<Device>>(DeviceQueries.getDeviceDetails);

            Log.TryWriteLog(LogType.Debug, "Compliance Report Prototype", $"Fetched {devices?.Count() ?? 0} devices.", DebugConfig.ExtendedLogReportGeneration);

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
                Log.TryWriteLog(LogType.Debug, "Compliance Report Prototype", $"Fetched {Rules.Count} rules for compliance report.", DebugConfig.ExtendedLogReportGeneration);
            }
            else
            {
                Log.TryWriteLog(LogType.Error, "Compliance Report Prototype", "Failed to fetch rules for compliance report.", DebugConfig.ExtendedLogReportGeneration);
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

        #endregion

        #region Methods - Public

        public async Task SetComplianceData(CancellationToken ct)
        {
            List<Task> tasks = new();

            foreach (Rule rule in Rules)
            {
                await _semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await SetComplianceDataForRule(rule);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, ct);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
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
                Log.TryWriteLog(LogType.Error, "Compliance Report", $"Error while setting compliance data for rule {rule.Id}: {e.Message}", DebugConfig.ExtendedLogReportGeneration);
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
    }
}