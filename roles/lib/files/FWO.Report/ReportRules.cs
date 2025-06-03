using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.FwLogic;
using FWO.Logging;
using FWO.Report.Filter;
using FWO.Ui.Display;
using Newtonsoft.Json;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;

namespace FWO.Report
{
    public static class DeviceReportExtensions 
    {
        public static bool ContainsRules(this DeviceReport device)
        {
            return device.RulebaseLinks != null && device.RulebaseLinks.Any();
        }

        public static bool ContainsRules(this ManagementReport management)
        {
            return management.Devices != null && management.Devices.Any(d => d.ContainsRules());
        }
    }
    
    public class ReportRules : ReportDevicesBase
    {
        private const int ColumnCount = 12;
        protected bool UseAdditionalFilter = false;
        private bool VarianceMode = false;
        private static TreeItem<Rule> _ruleTree = new TreeItem<Rule>();

        public ReportRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            Query.QueryVariables["limit"] = rulesPerFetch;
            Query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;

            List<ManagementReport> managementsWithRelevantImportId = await GetRelevantImportIds(apiConnection);

            ReportData.ManagementData = [];
            foreach (var management in managementsWithRelevantImportId)
            {
                SetMgtQueryVars(management);    // this includes mgm_id AND relevant import ID!
                ManagementReport managementReport = (await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables))[0];
                managementReport.Import = management.Import;
                ReportData.ManagementData.Add(managementReport);
            }

            while (gotNewObjects)
            {
                if (ct.IsCancellationRequested)
                {
                    Log.WriteDebug("Generate Rules Report", "Task cancelled");
                    ct.ThrowIfCancellationRequested();
                }
                gotNewObjects = false;
                Query.QueryVariables["offset"] = (int)Query.QueryVariables["offset"] + rulesPerFetch;
                foreach (var management in managementsWithRelevantImportId)
                {
                    SetMgtQueryVars(management);
                    ManagementReport? mgtToFill = ReportData.ManagementData.FirstOrDefault(m => m.Id == management.Id);
                    if (mgtToFill != null)
                    {
                        gotNewObjects |= mgtToFill.Merge((await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables))[0]);
                    }
                }
                await callback(ReportData);
            }
            SetReportedRuleIds();
        }

        private void SetMgtQueryVars(ManagementReport management)
        {
            Query.QueryVariables["mgmId"] = management.Id;
            if (ReportType != ReportType.Recertification)
            {
                Query.QueryVariables["relevantImportId"] = management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1; /* managment was not yet imported at that time */;
            }
        }

        public override async Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback) // to be called when exporting
        {
            bool gotAllObjects = true; //whether the fetch count limit was reached during fetching

            if (!GotObjectsInReport)
            {
                foreach (var managementReport in ReportData.ManagementData)
                {
                    if (managementReport.Import.ImportAggregate.ImportAggregateMax.RelevantImportId is not null)
                    {
                        // set query variables for object query
                        var objQueryVariables = new Dictionary<string, object>
                        {
                            { "mgmIds", managementReport.Id },
                            { "limit", objectsPerFetch },
                            { "offset", 0 },
                        };

                        // get objects for this management in the current report
                        gotAllObjects &= await GetObjectsForManagementInReport(objQueryVariables, ObjCategory.all, int.MaxValue, apiConnection, callback);
                    }
                }
                GotObjectsInReport = true;
            }

            return gotAllObjects;
        }

        public override async Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            if (!objQueryVariables.ContainsKey("mgmIds") || !objQueryVariables.ContainsKey("limit") || !objQueryVariables.ContainsKey("offset"))
                throw new ArgumentException("Given objQueryVariables dictionary does not contain variable for management id, limit or offset");

            int mid = (int)objQueryVariables.GetValueOrDefault("mgmIds")!;
            ManagementReport managementReport = ReportData.ManagementData.FirstOrDefault(m => m.Id == mid) ?? throw new ArgumentException("Given management id does not exist for this report");

            objQueryVariables.Add("ruleIds", "{" + string.Join(", ", managementReport.ReportedRuleIds) + "}");
            objQueryVariables.Add("importId", managementReport.Import.ImportAggregate.ImportAggregateMax.RelevantImportId!); // TODO: replaced with below - check if not needed anymore and remove
            objQueryVariables.Add("import_id_start", managementReport.Import.ImportAggregate.ImportAggregateMax.RelevantImportId!);
            objQueryVariables.Add("import_id_end", managementReport.Import.ImportAggregate.ImportAggregateMax.RelevantImportId!);

            string query = GetQuery(objects);
            bool newObjects = true;
            int fetchCount = 0;
            int elementsPerFetch = (int)objQueryVariables.GetValueOrDefault("limit")!;
            ManagementReport filteredObjects;
            ManagementReport allFilteredObjects = new();
            while (newObjects && ++fetchCount <= maxFetchCycles)
            {
                filteredObjects = (await apiConnection.SendQueryAsync<List<ManagementReport>>(query, objQueryVariables))[0];

                if (fetchCount == 1)
                {
                    allFilteredObjects = filteredObjects;
                }
                else
                {
                    newObjects = allFilteredObjects.MergeReportObjects(filteredObjects);
                }

                if (UseAdditionalFilter)
                {
                    AdditionalFilter(allFilteredObjects, managementReport.RelevantObjectIds);
                }

                if (objects == ObjCategory.all || objects == ObjCategory.nobj)
                    managementReport.ReportObjects = allFilteredObjects.ReportObjects;
                if (objects == ObjCategory.all || objects == ObjCategory.nsrv)
                    managementReport.ReportServices = allFilteredObjects.ReportServices;
                if (objects == ObjCategory.all || objects == ObjCategory.user)
                    managementReport.ReportUsers = allFilteredObjects.ReportUsers;

                objQueryVariables["offset"] = (int)objQueryVariables["offset"] + elementsPerFetch;

                await callback(ReportData);
            }

            Log.WriteDebug("Lazy Fetch", $"Fetched sidebar objects in {fetchCount - 1} cycle(s) ({elementsPerFetch} at a time)");

            return fetchCount <= maxFetchCycles;
        }

        private static string GetQuery(ObjCategory objects)
        {
            return objects switch
            {
                ObjCategory.all => ObjectQueries.getReportFilteredObjectDetails,
                ObjCategory.nobj => ObjectQueries.getReportFilteredNetworkObjectDetails,
                ObjCategory.nsrv => ObjectQueries.getReportFilteredNetworkServiceObjectDetails,
                ObjCategory.user => ObjectQueries.getReportFilteredUserDetails,
                _ => "",
            };
        }

        private static void AdditionalFilter(ManagementReport mgt, List<long> relevantObjectIds)
        {
            mgt.ReportObjects = [.. mgt.ReportObjects.Where(o => relevantObjectIds.Contains(o.Id))];
        }

        public static Rule[] GetRulesByRulebaseId(int rulebaseId, ManagementReport managementReport)
        {
            Rule[]? rules = managementReport.Rulebases.FirstOrDefault(rb => rb.Id == rulebaseId)?.Rules;
            if (rules != null)
            {
                return rules;
            }
            return [];
        }
        public static Rule[] GetInitialRulesOfGateway(DeviceReportController deviceReport, ManagementReport managementReport)
        {
            int? initialRulebaseId = deviceReport.GetInitialRulebaseId(managementReport);
            if (initialRulebaseId != null)
            {
                Rule[]? rules = GetRulesByRulebaseId((int)initialRulebaseId, managementReport);
                if (rules != null)
                {
                    return rules;
                }
            }
            return [];
        }
        public static Rule[] GetAllRulesOfGateway(DeviceReportController deviceReport, ManagementReport managementReport)
        {
            _ruleTree = new();
            List<Rule> visitedRules = new();
            List<Rule> allRules = new();
            Dictionary<int, int> concatenationCounters = new();

            // Create map of rulebase link to target rulebase.

            Dictionary<int, RulebaseReport> reportById = managementReport.Rulebases.ToDictionary(r => r.Id);

            Dictionary<RulebaseLink, RulebaseReport> rulebaseByLink = deviceReport
                .RulebaseLinks
                .Where(link => reportById.ContainsKey(link.NextRulebaseId))
                .ToDictionary(
                    link => link,
                    link => reportById[link.NextRulebaseId]
                );

            // Get all rules.

            foreach (var rulebaseByLinkItem in rulebaseByLink)
            {
                foreach (var rule in rulebaseByLinkItem.Value.Rules)
                {
                    if (!allRules.Contains(rule))
                    {
                        allRules.Add(rule);
                    }
                }
            }

            CreateOrderNumbers(allRules, deviceReport, rulebaseByLink, managementReport, concatenationCounters);

            return allRules.ToArray();
        }
        
        public static int GetRuleCount(ManagementReport mgmReport, RulebaseLink? currentRbLink, RulebaseLink[] rulebaseLinks)
        {
            if (currentRbLink != null)
            {
                int ruleCount = 0;
                if (currentRbLink != null)
                {
                    int nextRulebaseId = currentRbLink.NextRulebaseId;
                    RulebaseReport? nextRulebase = mgmReport.Rulebases.FirstOrDefault(_ => _.Id == nextRulebaseId);
                    if (nextRulebase != null)
                    {
                        foreach (var rule in nextRulebase.Rules)
                        {
                            if (string.IsNullOrEmpty(rule.SectionHeader))
                            {
                                RulebaseLink? nextRbLink = rulebaseLinks.FirstOrDefault(_ => _.FromRuleId == rule.Id);
                                if (nextRbLink != null)
                                {
                                    ruleCount += 1 + GetRuleCount(mgmReport, nextRbLink, rulebaseLinks);
                                }
                                else
                                {
                                    ruleCount++;
                                }
                            }
                        }
                        return ruleCount;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// Creates multi-level (dotted) order numbers for display and sets internal numeric order for sorting.
        /// </summary>
        public static void CreateOrderNumbers(List<Rule> rules, DeviceReport device, Dictionary<RulebaseLink, RulebaseReport> rulebaseByLink, ManagementReport managementReport, Dictionary<int, int> concatenationCounters)
        {
            // Creates a dictionary with rulebase IDs as keys and lists of the corresponding rows as values.

            Dictionary<int, List<Rule>> rulesByRulebase = rules
                .GroupBy(r => r.RulebaseId)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.OrderNumber).ToList());

            // Normalize actual order numbers to incremental int like form.

            NormalizeOrderNumbers(rulesByRulebase);

            // Creates a dictionary with rule IDs as keys and the rulebase link that has that rule as its from rule as values.

            SetMissingFromRuleIds(rulesByRulebase, rulebaseByLink);

            // Initialize other needed variables.

            List<Rule> changedRules = new();
            List<int> initialPath = new();
            int positionCounter = 1;
            RulebaseLink rulebaseLink = device.RulebaseLinks.First(link => link.IsInitialRulebase());
            int firstRulebaseId = rulebaseLink.NextRulebaseId;
            List<RulebaseLink> processedLinks = new();
            processedLinks.Add(rulebaseLink);

            // If there are more than one layer path needs to be initialized here.

            if (device.RulebaseLinks.Any(link => link.LinkType == 2))    // ordered
            {
                initialPath.Add(1);
            }

            BuildOrderNumberTree(rulebaseLink, firstRulebaseId, initialPath, rulesByRulebase, rulebaseByLink, changedRules, ref positionCounter, rules, processedLinks, managementReport, concatenationCounters);
        }

        private static void BuildOrderNumberTree(RulebaseLink currentLink,
                                                    int rulebaseId,
                                                    List<int> currentPath,
                                                    Dictionary<int, List<Rule>> rulesByRulebase,
                                                    Dictionary<RulebaseLink,
                                                    RulebaseReport> rulebaseByLink,
                                                    List<Rule> changedRules,
                                                    ref int positionCounter,
                                                    List<Rule> rulebaseRules,
                                                    List<RulebaseLink> processedLinks,
                                                    ManagementReport managementReport,
                                                    Dictionary<int, int> concatenationCounters)
        {

            if (!rulesByRulebase.TryGetValue(rulebaseId, out var rules))
            {
                if (currentLink.LinkType == 3 && GetNextRulebaseLink(null,processedLinks,rulebaseByLink,rulebaseId)?.LinkType == 4)
                {
                    currentPath.Add(0);
                }

                HandleOrderNumberTreeNode(currentLink, currentLink.NextRulebaseId, currentPath, rulesByRulebase, rulebaseByLink, changedRules, ref positionCounter, rulebaseRules, processedLinks, null, null, managementReport, concatenationCounters);
                return;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                Rule rule = rules[i];

                // Create duplicate if rulebase links make it necessary.

                if (changedRules.Contains(rule))
                {
                    rule = rule.CreateClone();
                    rulebaseRules.Add(rule);
                }

                // Write order numbers to rule object.

                List<int> path = currentPath;

                if (currentLink.LinkType == 4)
                {
                    path[path.Count() - 1] = GetIncrementedConcatenationCounter(rulebaseId, concatenationCounters);
                }
                else
                {
                    path = new List<int>(currentPath) { rule.RuleOrderNumber };
                }

                string dotted = string.Join(".", path);
                rule.DisplayOrderNumberString = dotted;
                rule.OrderNumber = positionCounter++;

                // Gather changed rules, to recognize if duplicate is necessary

                changedRules.Add(rule);

                if (rule == GetRulesByRulebaseId(rulebaseId, managementReport).Last())
                {
                    RulebaseLink? link = GetNextRulebaseLink(rule, processedLinks, rulebaseByLink, rulebaseId);

                    if (link != null && link.LinkType == 4 && concatenationCounters.TryGetValue(rulebaseId, out int increment))
                    {
                        concatenationCounters[link.NextRulebaseId] = increment;
                    }
                }

                HandleOrderNumberTreeNode(currentLink, currentLink.NextRulebaseId, path, rulesByRulebase, rulebaseByLink, changedRules, ref positionCounter, rulebaseRules, processedLinks, rule, rules, managementReport, concatenationCounters);
            }
        }

        private static void HandleOrderNumberTreeNode(RulebaseLink currentLink,
                                                        int rulebaseId,
                                                        List<int> currentPath,
                                                        Dictionary<int,
                                                        List<Rule>> rulesByRulebase,
                                                        Dictionary<RulebaseLink, RulebaseReport> rulebaseByLink,
                                                        List<Rule> changedRules,
                                                        ref int positionCounter,
                                                        List<Rule> rulebaseRules,
                                                        List<RulebaseLink> processedLinks,
                                                        Rule? rule,
                                                        List<Rule>? rules,
                                                        ManagementReport managementReport,
                                                        Dictionary<int, int> concatenationCounters)
        {
            RulebaseLink? link = GetNextRulebaseLink(rule, processedLinks, rulebaseByLink, rulebaseId);

            if (link != null)
            {
                processedLinks.Add(link);

                switch (link.LinkType)
                {
                    case 2: // ordered
                        List<int> newPath = new() { currentPath[0] + 1, 0 };
                        BuildOrderNumberTree(link, link.NextRulebaseId, newPath, rulesByRulebase, rulebaseByLink, changedRules, ref positionCounter, rulebaseRules, processedLinks, managementReport, concatenationCounters);
                        break;

                    case 3: // inline
                        BuildOrderNumberTree(link, link.NextRulebaseId, currentPath, rulesByRulebase, rulebaseByLink, changedRules, ref positionCounter, rulebaseRules, processedLinks, managementReport, concatenationCounters);
                        break;

                    case 4: // concatenated
                        if (currentLink.LinkType == 3 && rules != null && rule != null && rule == GetRulesByRulebaseId(rulebaseId, managementReport).Last())
                        {
                            concatenationCounters[link.NextRulebaseId] = changedRules.Where(rule => rulebaseId == rule.RulebaseId).Count();
                        }

                        BuildOrderNumberTree(link, link.NextRulebaseId, currentPath, rulesByRulebase, rulebaseByLink, changedRules, ref positionCounter, rulebaseRules, processedLinks, managementReport, concatenationCounters);
                        break;
                }
            }
        }

        private static int GetIncrementedConcatenationCounter(int rulebaseId, Dictionary<int, int> concatenationCounters)
        {
            if (concatenationCounters.TryGetValue(rulebaseId, out var currentCount))
            {
                concatenationCounters[rulebaseId] = currentCount + 1;
            }
            else
            {
                concatenationCounters[rulebaseId] = 1;
            }

            return concatenationCounters[rulebaseId];
        }


        private static RulebaseLink? GetNextRulebaseLink(Rule? currentRule, List<RulebaseLink> processedLinks, Dictionary<RulebaseLink, RulebaseReport> rulebaseByLink, int rulebaseId)
        {
            RulebaseLink? nextRulebaseLink = null;

            if (currentRule != null)
            {
                nextRulebaseLink = rulebaseByLink.Keys
                    .Where(rulebaseLink => !processedLinks.Contains(rulebaseLink))
                    .FirstOrDefault(rulebaseLink => rulebaseLink.FromRuleId == currentRule.Id);

                if (nextRulebaseLink != null)
                {
                    return nextRulebaseLink;
                }

                nextRulebaseLink = rulebaseByLink.Keys
                    .Where(rulebaseLink => !processedLinks.Contains(rulebaseLink))
                    .FirstOrDefault(rulebaseLink => rulebaseLink.FromRulebaseId == rulebaseId);
            }
            else
            {
                nextRulebaseLink = rulebaseByLink.Keys
                    .Where(rulebaseLink => !processedLinks.Contains(rulebaseLink))
                    .FirstOrDefault(rulebaseLink => rulebaseLink.FromRulebaseId == rulebaseId);
            }




            return nextRulebaseLink;
        }

        /// <summary>
        /// Normalizes float values within rule groups (grouped by rulebase ID) to ascending integers 
        /// while preserving their relative order (e.g., [1.4, 4.645, 13.65] -> [1, 2, 3]).
        /// </summary>
        /// <param name="rulesByRulebase"></param>
        private static void NormalizeOrderNumbers(Dictionary<int, List<Rule>> rulesByRulebase)
        {
            foreach (KeyValuePair<int, List<Rule>> rulebaseRules in rulesByRulebase)
            {
                int relativeOrderNumber = 1;

                foreach (Rule rule in rulebaseRules.Value.ToList())
                {
                    rule.RuleOrderNumber = relativeOrderNumber;
                    relativeOrderNumber++;
                }
            }
        }

        /// <summary>
        /// </summary>
        private static void SetMissingFromRuleIds(Dictionary<int, List<Rule>> rulesByRulebase, Dictionary<RulebaseLink, RulebaseReport> rulebaseByLink)
        {
            int? lastSetId = 0;

            foreach (RulebaseLink rulebaseLink in rulebaseByLink.Keys.Where(rulebaseLink => rulebaseLink.FromRuleId == null).Where(rulebaseLink => !rulebaseLink.IsInitial))
            {
                KeyValuePair<RulebaseLink, RulebaseReport> previousRulebaseByLinkItem = rulebaseByLink.First(rulebaseByLinkItem => rulebaseByLinkItem.Value.Id == rulebaseLink.FromRulebaseId);
                Rule? rule = previousRulebaseByLinkItem.Value.Rules.LastOrDefault();
                rulebaseLink.FromRuleId = (rule != null ? (int)rule.Id : lastSetId);
                lastSetId = rulebaseLink.FromRuleId;
            }
        }

        public override string SetDescription()
        {
            int managementCounter = 0;
            int deviceCounter = 0;
            int ruleCounter = 0;
            foreach (ManagementReportController managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    mgt.ContainsRules()))
            {
                managementCounter++;
                foreach (var device in managementReport.Devices.Where(dev => dev.ContainsRules()))
                {
                    deviceCounter++;
                    ruleCounter += GetRuleCount(managementReport, device.RulebaseLinks.FirstOrDefault(_ => _.IsInitialRulebase()), device.RulebaseLinks);
                }
            }
            return $"{managementCounter} {userConfig.GetText("managements")}, {deviceCounter} {userConfig.GetText("gateways")}, {ruleCounter} {userConfig.GetText("rules")}";
        }

        // here we can simply traverse all rulebases (disregarding any order) and add their ids to the list
        private void SetReportedRuleIds()
        {
            foreach (var mgt in ReportData.ManagementData)
            {
                foreach (var dev in mgt.Devices.Where(b => b.ContainsRules()))
                {
                    DeviceReportController deviceController = DeviceReportController.FromDeviceReport(dev);
                    if (deviceController.RulebaseLinks != null)
                    {
                        foreach (var rbLink in deviceController.RulebaseLinks)
                        {
                            RulebaseReport? rulebase = mgt.Rulebases.FirstOrDefault(_ => _.Id == rbLink.NextRulebaseId);
                            if (rulebase != null)
                            {
                                foreach (Rule rule in rulebase.Rules)
                                {
                                    mgt.ReportedRuleIds.Add(rule.Id);
                                }
                            }
                        }
                    }
                }
                mgt.ReportedRuleIds = mgt.ReportedRuleIds.Distinct().ToList();
            }
        }

        private string ExportSingleRulebaseToCsv(StringBuilder report, RuleDisplayCsv ruleDisplayCsv, ManagementReport managementReport, DeviceReport gateway, RulebaseLink? rbLink)
        {
            if (rbLink == null)
            {
                return report.ToString();
            }
            foreach (var rule in GetRulesByRulebaseId(rbLink.NextRulebaseId, managementReport)) // just dealing with the first rb for starters
            {
                if (string.IsNullOrEmpty(rule.SectionHeader))
                {
                    report.Append(ruleDisplayCsv.OutputCsv(managementReport.Name));
                    report.Append(ruleDisplayCsv.OutputCsv(gateway.Name));
                    report.Append(ruleDisplayCsv.DisplayNumberCsv(rule));
                    report.Append(ruleDisplayCsv.DisplayNameCsv(rule));
                    report.Append(ruleDisplayCsv.DisplaySourceZoneCsv(rule));
                    report.Append(ruleDisplayCsv.DisplaySourceCsv(rule, ReportType));
                    report.Append(ruleDisplayCsv.DisplayDestinationZoneCsv(rule));
                    report.Append(ruleDisplayCsv.DisplayDestinationCsv(rule, ReportType));
                    report.Append(ruleDisplayCsv.DisplayServicesCsv(rule, ReportType));
                    report.Append(ruleDisplayCsv.DisplayActionCsv(rule));
                    report.Append(ruleDisplayCsv.DisplayTrackCsv(rule));
                    report.Append(ruleDisplayCsv.DisplayEnabledCsv(rule));
                    report.Append(ruleDisplayCsv.DisplayUidCsv(rule));
                    report.Append(ruleDisplayCsv.DisplayCommentCsv(rule));
                    report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last chars (comma)
                    report.AppendLine("");  // EO rule
                }
                else
                {
                    // report.AppendLine("\"section header\": \"" + rule.SectionHeader + "\"");
                }
                ExportSingleRulebaseToCsv(report, ruleDisplayCsv, managementReport, gateway, gateway.RulebaseLinks.FirstOrDefault(_ => _.FromRuleId == rule.Id));
            } // rules 
            return report.ToString();
        }
        public override string ExportToCsv()
        {
            if (ReportType.IsResolvedReport())
            {
                StringBuilder report = new();
                RuleDisplayCsv ruleDisplayCsv = new(userConfig);

                report.Append(DisplayReportHeaderCsv());
                report.AppendLine($"\"management-name\",\"device-name\",\"rule-number\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"");

                foreach (var managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                        Array.Exists(mgt.Devices, device => device.ContainsRules())))
                {
                    foreach (var gateway in managementReport.Devices)
                    {
                        if (gateway.ContainsRules())
                        {
                            if (gateway.RulebaseLinks != null)
                            {
                                RulebaseLink? rbLink = gateway.RulebaseLinks.FirstOrDefault(rbl => rbl.IsInitialRulebase());
                                if (rbLink != null)
                                {
                                    ExportSingleRulebaseToCsv(report, ruleDisplayCsv, managementReport, gateway, rbLink);
                                }
                            }
                        } // gateways
                    } // managements
                }
                string reportStr = report.ToString();
                return reportStr;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override string ExportToJson()
        {
            if (ReportType.IsResolvedReport())
            {
                // JSON code for resolved rules is stripped from all unneccessary balast, only containing the resolved rules
                // object tables are not needed as the objects within the rules fully describe the rules (no groups)
                return ExportResolvedRulesToJson();
            }
            else if (ReportType.IsRuleReport())
            {
                return System.Text.Json.JsonSerializer.Serialize(ReportData.ManagementData.Where(mgt => !mgt.Ignore), new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                return "";
            }
        }

        private string ExportResolvedRulesToJson()
        {
            StringBuilder report = new("{");
            report.Append(DisplayReportHeaderJson());
            report.AppendLine("\"managements\": [");
            RuleDisplayJson ruleDisplayJson = new(userConfig);
            foreach (var managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.ContainsRules())))
            {
                report.AppendLine($"{{\"{managementReport.Name}\": {{");
                report.AppendLine($"\"gateways\": [");
                foreach (var gateway in managementReport.Devices)
                {
                    if (gateway.ContainsRules())
                    {
                        report.Append($"{{\"{gateway.Name}\": {{\n\"rules\": [");
                        // TODO: migrate this
                        // foreach (var rb in gateway.Rulebases)
                        // {
                        //     foreach (var rule in rb.Rulebase.RuleMetadata[0].Rules)
                        //     {
                        //         report.Append('{');
                        //         if (string.IsNullOrEmpty(rule.SectionHeader))
                        //         {
                        //             report.Append(ruleDisplayJson.DisplayNumber(rule));
                        //             report.Append(ruleDisplayJson.DisplayName(rule.Name));
                        //             report.Append(ruleDisplayJson.DisplaySourceZone(rule.SourceZone?.Name));
                        //             report.Append(ruleDisplayJson.DisplaySourceNegated(rule.SourceNegated));
                        //             report.Append(ruleDisplayJson.DisplaySource(rule, ReportType));
                        //             report.Append(ruleDisplayJson.DisplayDestinationZone(rule.DestinationZone?.Name));
                        //             report.Append(ruleDisplayJson.DisplayDestinationNegated(rule.DestinationNegated));
                        //             report.Append(ruleDisplayJson.DisplayDestination(rule, ReportType));
                        //             report.Append(ruleDisplayJson.DisplayServiceNegated(rule.ServiceNegated));
                        //             report.Append(ruleDisplayJson.DisplayServices(rule, ReportType));
                        //             report.Append(ruleDisplayJson.DisplayAction(rule.Action));
                        //             report.Append(ruleDisplayJson.DisplayTrack(rule.Track));
                        //             report.Append(ruleDisplayJson.DisplayEnabled(rule.Disabled));
                        //             report.Append(ruleDisplayJson.DisplayUid(rule.Uid));
                        //             report.Append(ruleDisplayJson.DisplayComment(rule.Comment));
                        //             report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last chars (comma)
                        //         }
                        //         else
                        //         {
                        //             report.AppendLine("\"section header\": \"" + rule.SectionHeader + "\"");
                        //         }
                        //         report.Append("},");  // EO rule
                        //     } // rules
                        // }
                        report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last char (comma)
                        report.Append(']'); // EO rules
                        report.Append('}'); // EO gateway internal
                        report.Append("},"); // EO gateway external
                    }
                } // gateways
                report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last char (comma)
                report.Append(']'); // EO gateways
                report.Append('}'); // EO management internal
                report.Append("},"); // EO management external
            } // managements
            report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last char (comma)
            report.Append(']'); // EO managements
            report.Append('}'); // EO top

            dynamic? json = JsonConvert.DeserializeObject(report.ToString());
            JsonSerializerSettings settings = new()
            {
                Formatting = Formatting.Indented
            };
            return JsonConvert.SerializeObject(json, settings);
        }

        public override string ExportToHtml()
        {
            StringBuilder report = new();
            int chapterNumber = 0;
            ConstructHtmlReport(ref report, ReportData.ManagementData, chapterNumber);
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        public void ConstructHtmlReport(ref StringBuilder report, List<ManagementReport> managementData, int chapterNumber, bool varianceMode = false)
        {
            RuleDisplayHtml ruleDisplayHtml = new(userConfig);
            VarianceMode = varianceMode;

            foreach (ManagementReport managementReport in managementData.Where(mgt => !mgt.Ignore && mgt.ContainsRules()))
            {
                chapterNumber++;
                new ManagementReportController(managementReport).AssignRuleNumbers();
                report.AppendLine(Headline(managementReport.Name, 3));
                report.AppendLine("<hr>");

                foreach (var device in managementReport.Devices)
                {
                    if (device.RulebaseLinks != null)
                    {
                        AppendRulesForDeviceHtml(ref report, managementReport, DeviceReportController.FromDeviceReport(device), chapterNumber, ruleDisplayHtml);
                    }
                }

                // show all objects used in this management's rules
                AppendObjectsForManagementHtml(ref report, chapterNumber, managementReport);
            }
        }

        private void AppendRuleHeadlineHtml(ref StringBuilder report)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
            if (ReportType == ReportType.Recertification)
            {
                report.AppendLine($"<th>{userConfig.GetText("next_recert")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("owner")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("ip_matches")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("last_hit")}</th>");
            }
            if (ReportType == ReportType.UnusedRules || ReportType == ReportType.AppRules)
            {
                report.AppendLine($"<th>{userConfig.GetText("last_hit")}</th>");
            }
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("source_zone")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("source")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("destination_zone")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("destination")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("services")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("action")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("track")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("enabled")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
            report.AppendLine("</tr>");
        }

        private void AppendRulesForDeviceHtml(ref StringBuilder report, ManagementReport managementReport, DeviceReportController device, int chapterNumber, RuleDisplayHtml ruleDisplayHtml)
        {
            if (device.ContainsRules())
            {
                report.AppendLine(Headline(device.Name, 4));
                report.AppendLine("<table>");
                AppendRuleHeadlineHtml(ref report);

                RulebaseLink? nextRbLink = device.RulebaseLinks.FirstOrDefault(_ => _.IsInitialRulebase());

                if (nextRbLink != null)
                {
                    AppendRulesForRulebaseHtml(ref report, nextRbLink, managementReport, device, chapterNumber, ruleDisplayHtml);
                }
                report.AppendLine("</table>");
                report.AppendLine("<hr>");
            }
        }


        private void AppendRulesForRulebaseHtml(ref StringBuilder report, RulebaseLink rbLink, ManagementReport managementReport, DeviceReport device, int chapterNumber, RuleDisplayHtml ruleDisplayHtml)
        {
            Rule[]? rb = GetRulesByRulebaseId(rbLink.NextRulebaseId, managementReport);

            if (rb == null)
            {
                return;
            }

            foreach (var rule in rb)
            {
                if (string.IsNullOrEmpty(rule.SectionHeader))
                {
                    report.AppendLine("<tr>");
                    report.AppendLine($"<td>{RuleDisplayBase.DisplayNumber(rule)}</td>");
                    if (ReportType == ReportType.Recertification)
                    {
                        report.AppendLine($"<td>{RuleDisplayHtml.DisplayNextRecert(rule.Metadata)}</td>");
                        report.AppendLine($"<td>{RuleDisplayHtml.DisplayOwner(rule.Metadata)}</td>");
                        report.AppendLine($"<td>{RuleDisplayHtml.DisplayRecertIpMatches(rule.Metadata)}</td>");
                        report.AppendLine($"<td>{RuleDisplayHtml.DisplayLastHit(rule.Metadata)}</td>");
                    }
                    if (ReportType == ReportType.UnusedRules) // || ReportType == ReportType.AppRules)
                    {
                        report.AppendLine($"<td>{RuleDisplayHtml.DisplayLastHit(rule.Metadata)}</td>");
                    }
                    report.AppendLine($"<td>{RuleDisplayBase.DisplayName(rule)}</td>");
                    report.AppendLine($"<td>{RuleDisplayBase.DisplaySourceZone(rule)}</td>");
                    report.AppendLine($"<td>{ruleDisplayHtml.DisplaySource(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                    report.AppendLine($"<td>{RuleDisplayBase.DisplayDestinationZone(rule)}</td>");
                    report.AppendLine($"<td>{ruleDisplayHtml.DisplayDestination(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                    report.AppendLine($"<td>{ruleDisplayHtml.DisplayServices(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                    report.AppendLine($"<td>{RuleDisplayBase.DisplayAction(rule)}</td>");
                    report.AppendLine($"<td>{RuleDisplayBase.DisplayTrack(rule)}</td>");
                    report.AppendLine($"<td>{RuleDisplayBase.DisplayEnabled(rule, OutputLocation.export)}</td>");
                    report.AppendLine($"<td>{RuleDisplayBase.DisplayUid(rule)}</td>");
                    report.AppendLine($"<td>{RuleDisplayBase.DisplayComment(rule)}</td>");
                    report.AppendLine("</tr>");
                }
                else
                {
                    report.AppendLine(RuleDisplayHtml.DisplaySectionHeader(rule, ColumnCount));
                }

                // if there is a rulebase link starting from the current rule id, follow it
                RulebaseLink? nextRbLink = device.RulebaseLinks.FirstOrDefault(_ => _.FromRuleId == rule.Id);
                if (nextRbLink != null)
                {
                    AppendRulesForRulebaseHtml(ref report, nextRbLink, managementReport, device, chapterNumber, ruleDisplayHtml);
                }
            }
        }

        private void AppendObjectsForManagementHtml(ref StringBuilder report, int chapterNumber, ManagementReport managementReport)
        {
            AppendNetworkObjectsForManagementHtml(ref report, chapterNumber, managementReport);
            AppendNetworkServicesForManagementHtml(ref report, chapterNumber, managementReport);
            AppendUsersForManagementHtml(ref report, chapterNumber, managementReport);
        }

        private void AppendNetworkObjectsForManagementHtml(ref StringBuilder report, int chapterNumber, ManagementReport managementReport)
        {
            if (managementReport.ReportObjects != null && managementReport.ReportObjects.Length > 0 && !ReportType.IsResolvedReport())
            {
                report.AppendLine(Headline(userConfig.GetText("network_objects"), 4));
                report.AppendLine("<table>");
                report.AppendLine("<tr>");
                report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("type")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("ip_address")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                report.AppendLine("</tr>");
                int objNumber = 1;
                foreach (var nwobj in managementReport.ReportObjects)
                {
                    report.AppendLine($"<tr style=\"{(nwobj.Highlighted ? GlobalConst.kStyleHighlightedRed : "")}\">");
                    report.AppendLine($"<td>{objNumber++}</td>");
                    report.AppendLine($"<td><a name={ObjCatString.NwObj}{chapterNumber}x{nwobj.Id}>{nwobj.Name}</a></td>");
                    report.AppendLine($"<td>{(nwobj.Type.Name != "" ? userConfig.GetText(nwobj.Type.Name) : "")}</td>");
                    report.AppendLine($"<td>{NwObjDisplay.DisplayIp(nwobj.IP, nwobj.IpEnd, nwobj.Type.Name)}</td>");
                    report.AppendLine(nwobj.MemberNamesAsHtml());
                    report.AppendLine($"<td>{nwobj.Uid}</td>");
                    report.AppendLine($"<td>{nwobj.Comment}</td>");
                    report.AppendLine("</tr>");
                }
                report.AppendLine("</table>");
                report.AppendLine("<hr>");
            }
        }

        private void AppendNetworkServicesForManagementHtml(ref StringBuilder report, int chapterNumber, ManagementReport managementReport)
        {
            if (managementReport.ReportServices != null && managementReport.ReportServices.Length > 0 && !ReportType.IsResolvedReport())
            {
                report.AppendLine(Headline(userConfig.GetText("network_services"), 4));
                report.AppendLine("<table>");
                report.AppendLine("<tr>");
                report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("type")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("protocol")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("port")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                report.AppendLine("</tr>");
                int objNumber = 1;
                foreach (var svcobj in managementReport.ReportServices)
                {
                    report.AppendLine("<tr>");
                    report.AppendLine($"<td>{objNumber++}</td>");
                    report.AppendLine($"<td><a name={ObjCatString.Svc}{chapterNumber}x{svcobj.Id}>{svcobj.Name}</a></td>");
                    report.AppendLine($"<td>{(svcobj.Type.Name != "" ? userConfig.GetText(svcobj.Type.Name) : "")}</td>");
                    report.AppendLine($"<td>{((svcobj.Type.Name != ServiceType.Group && svcobj.Protocol != null) ? svcobj.Protocol.Name : "")}</td>");
                    if (svcobj.DestinationPortEnd != null && svcobj.DestinationPortEnd != svcobj.DestinationPort)
                    {
                        report.AppendLine($"<td>{svcobj.DestinationPort}-{svcobj.DestinationPortEnd}</td>");
                    }
                    else
                    {
                        report.AppendLine($"<td>{svcobj.DestinationPort}</td>");
                    }
                    report.AppendLine(svcobj.MemberNamesAsHtml());
                    report.AppendLine($"<td>{svcobj.Uid}</td>");
                    report.AppendLine($"<td>{svcobj.Comment}</td>");
                    report.AppendLine("</tr>");
                }
                report.AppendLine("</table>");
                report.AppendLine("<hr>");
            }
        }

        private void AppendUsersForManagementHtml(ref StringBuilder report, int chapterNumber, ManagementReport managementReport)
        {
            if (managementReport.ReportUsers != null && managementReport.ReportUsers.Length > 0 && !ReportType.IsResolvedReport())
            {
                report.AppendLine(Headline(userConfig.GetText("users"), 4));
                report.AppendLine("<table>");
                report.AppendLine("<tr>");
                report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("type")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                report.AppendLine("</tr>");
                int objNumber = 1;
                foreach (var userobj in managementReport.ReportUsers)
                {
                    report.AppendLine("<tr>");
                    report.AppendLine($"<td>{objNumber++}</td>");
                    report.AppendLine($"<td><a name={ObjCatString.User}{chapterNumber}x{userobj.Id}>{userobj.Name}</a></td>");
                    report.AppendLine($"<td>{(userobj.Type.Name != "" ? userConfig.GetText(userobj.Type.Name) : "")}</td>");
                    report.AppendLine(userobj.MemberNamesAsHtml());
                    report.AppendLine($"<td>{userobj.Uid}</td>");
                    report.AppendLine($"<td>{userobj.Comment}</td>");
                    report.AppendLine("</tr>");
                }
                report.AppendLine("</table>");
                report.AppendLine("<hr>");
            }
        }

        private string Headline(string? title, int level)
        {
            int Level = VarianceMode ? level + 2 : level;
            return $"<h{Level} id=\"{Guid.NewGuid()}\">{title}</h{Level}>";
        }
    }
}
