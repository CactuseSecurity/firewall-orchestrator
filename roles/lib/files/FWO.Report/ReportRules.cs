using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.FwLogic;
using FWO.Services.RuleTreeBuilder;
using FWO.Logging;
using FWO.Report.Filter;
using FWO.Ui.Display;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

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

    public class ReportRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : ReportDevicesBase(query, userConfig, reportType)
    {
        private const int ColumnCount = 12;
        protected bool UseAdditionalFilter = false;

        private static Dictionary<(int deviceId, int managementId), Rule[]> _rulesCache = new();

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            Query.QueryVariables[QueryVar.Limit] = elementsPerFetch;
            Query.QueryVariables[QueryVar.Offset] = 0;
            bool keepFetching = true;

            List<ManagementReport> managementsWithRelevantImportId = await GetRelevantImportIds(apiConnection);

            ReportData.ManagementData = [];
            foreach (var management in managementsWithRelevantImportId)
            {
                SetMgtQueryVars(management);    // this includes mgm_id AND relevant import ID!
                List<ManagementReport> result = await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables);
                ManagementReport managementReport = result[0];
                managementReport.Import = management.Import;
                ReportData.ManagementData.Add(managementReport);
            }

            while (keepFetching)
            {
                if (ct.IsCancellationRequested)
                {
                    Log.WriteDebug("Generate Rules Report", "Task cancelled");
                    ct.ThrowIfCancellationRequested();
                }
                keepFetching = false;
                Query.QueryVariables[QueryVar.Offset] = (int)Query.QueryVariables[QueryVar.Offset] + elementsPerFetch;
                foreach (var management in managementsWithRelevantImportId)
                {
                    SetMgtQueryVars(management);
                    ManagementReport? mgtToFill = ReportData.ManagementData.FirstOrDefault(m => m.Id == management.Id);
                    if (mgtToFill != null)
                    {
                        (bool newObjects, Dictionary<string, int> maxAddedCounts) = mgtToFill.Merge((await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables))[0]);
                        // new objects might have been added, but if none reached the limit of elementsPerFetch, we can stop fetching
                        keepFetching = newObjects && maxAddedCounts["Rules"] >= elementsPerFetch; // limit is only set on rules for rule report query
                    }
                }
                await callback(ReportData);
            }

            TryBuildRuleTree();
        }

        protected void TryBuildRuleTree()
        {
            int ruleCount = 0;

            foreach (var managementReport in ReportData.ManagementData)
            {
                foreach (var deviceReport in managementReport.Devices)
                {
                    List<Rule> allRules = new();

                    if (Services.ServiceProvider.UiServices?.GetService<IRuleTreeBuilder>() is IRuleTreeBuilder ruleTreeBuilder)
                    {
                        if (ruleTreeBuilder?.BuildRulebaseLinkQueue(deviceReport.RulebaseLinks.Where(link => link.Removed == null).ToArray(), managementReport.Rulebases) != null)
                        {
                            allRules = ruleTreeBuilder.BuildRuleTree();
                            ruleCount += allRules.Count;
                        }
                    }
                    else
                    {
                        // if we are not building the rule tree, we just collect all rules from the rulebases

                        foreach (var rulebase in managementReport.Rulebases)
                        {
                            allRules.AddRange(rulebase.Rules);
                            ruleCount += rulebase.Rules.Count();
                        }
                    }

                    Rule[] rulesArray = allRules.ToArray();
                    _rulesCache[(deviceReport.Id, managementReport.Id)] = rulesArray;

                    // Add all rule ids to ReportedRuleIds of management, that are not already in that list

                    managementReport.ReportedRuleIds.AddRange(
                        rulesArray.Select(r => r.Id).Except(managementReport.ReportedRuleIds)
                    );
                }
            }

            ReportData.ElementsCount = ruleCount;
        }

        protected virtual void SetMgtQueryVars(ManagementReport management)
        {
            Query.QueryVariables[QueryVar.MgmId] = management.Id;
            Query.QueryVariables[QueryVar.ImportIdStart] = management.RelevantImportId ?? -1;
            Query.QueryVariables[QueryVar.ImportIdEnd] = management.RelevantImportId ?? -1;
            // this does not work: Query.QueryVariables[QueryVar.ImportIdStart] = management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1; /* managment was not yet imported at that time */;
            // this does not work: Query.QueryVariables[QueryVar.ImportIdEnd] = management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1; /* managment was not yet imported at that time */;
        }

        public override async Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback) // to be called when exporting
        {
            bool gotAllObjects = true; //whether the fetch count limit was reached during fetching

            if (!GotObjectsInReport)
            {
                foreach (var managementReport in ReportData.ManagementData.Where(x => x.Import.ImportAggregate.ImportAggregateMax.RelevantImportId is not null))
                {
                    // set query variables for object query
                    var objQueryVariables = new Dictionary<string, object>
                    {
                        { QueryVar.MgmIds, managementReport.Id },
                        { QueryVar.Limit, objectsPerFetch },
                        { QueryVar.Offset, 0 },
                    };

                    // get objects for this management in the current report
                    gotAllObjects &= await GetObjectsForManagementInReport(objQueryVariables, ObjCategory.all, int.MaxValue, apiConnection, callback);
                }
                GotObjectsInReport = true;
            }

            return gotAllObjects;
        }

        public override async Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            if (!objQueryVariables.ContainsKey(QueryVar.MgmIds) || !objQueryVariables.ContainsKey(QueryVar.Limit) || !objQueryVariables.ContainsKey(QueryVar.Offset))
                throw new ArgumentException("Given objQueryVariables dictionary does not contain variable for management id, limit or offset");

            int mid = (int)objQueryVariables.GetValueOrDefault(QueryVar.MgmIds)!;
            ManagementReport managementReport = ReportData.ManagementData.FirstOrDefault(m => m.Id == mid) ?? throw new ArgumentException("Given management id does not exist for this report");

            objQueryVariables.Add(QueryVar.RuleIds, "{" + string.Join(", ", managementReport.ReportedRuleIds) + "}");
            objQueryVariables.Add(QueryVar.ImportIdStart, managementReport.Import.ImportAggregate.ImportAggregateMax.RelevantImportId!);
            objQueryVariables.Add(QueryVar.ImportIdEnd, managementReport.Import.ImportAggregate.ImportAggregateMax.RelevantImportId!);

            string getObjQuery = GetQuery(objects);
            bool keepFetching = true;
            int fetchCount = 0;
            int elementsPerFetch = (int)objQueryVariables.GetValueOrDefault(QueryVar.Limit)!;
            ManagementReport filteredObjects;
            ManagementReport allFilteredObjects = new();
            while (keepFetching && ++fetchCount <= maxFetchCycles)
            {
                filteredObjects = (await apiConnection.SendQueryAsync<List<ManagementReport>>(getObjQuery, objQueryVariables))[0];

                if (fetchCount == 1)
                {
                    allFilteredObjects = filteredObjects;
                }
                else
                {
                    (bool newObjects, Dictionary<string, int> maxAddedCounts) = allFilteredObjects.Merge(filteredObjects);
                    keepFetching = newObjects && maxAddedCounts.Values.Any(v => v >= elementsPerFetch);
                }

                FillReport(allFilteredObjects, managementReport, objects);

                objQueryVariables[QueryVar.Offset] = (int)objQueryVariables[QueryVar.Offset] + elementsPerFetch;

                await callback(ReportData);
            }

            Log.WriteDebug("Lazy Fetch", $"Fetched sidebar objects in {fetchCount - 1} cycle(s) ({elementsPerFetch} at a time)");

            return fetchCount <= maxFetchCycles;
        }

        private void FillReport(ManagementReport allFilteredObjects, ManagementReport managementReport, ObjCategory objects)
        {
            if (UseAdditionalFilter)
            {
                AdditionalFilter(allFilteredObjects, managementReport.RelevantObjectIds);
            }

            if (objects == ObjCategory.all || objects == ObjCategory.nobj)
            {
                managementReport.ReportObjects = allFilteredObjects.ReportObjects;
            }
            if (objects == ObjCategory.all || objects == ObjCategory.nsrv)
            {
                managementReport.ReportServices = allFilteredObjects.ReportServices;
            }
            if (objects == ObjCategory.all || objects == ObjCategory.user)
            {
                managementReport.ReportUsers = allFilteredObjects.ReportUsers;
            }
        }

        private static string GetQuery(ObjCategory objects)
        {
            return objects switch
            {
                ObjCategory.all => ObjectQueries.getReportFilteredObjectDetails,
                ObjCategory.nobj => ObjectQueries.getReportFilteredNetworkObjectDetails,
                ObjCategory.nsrv => ObjectQueries.getReportFilteredNetworkServiceDetails,
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
            if (_rulesCache.TryGetValue((deviceReport.Id, managementReport.Id), out Rule[]? allRules))
            {
                return allRules;
            }
            else
            {
                return Array.Empty<Rule>();
            }
        }

        public static int GetRuleCount(ManagementReport mgmReport, RulebaseLink? currentRbLink, RulebaseLink[] rulebaseLinks)
        {
            RulebaseReport? nextRulebase = mgmReport.GetNextRulebase(currentRbLink);
            if (nextRulebase == null)
            {
                return 0;
            }
            int ruleCount = 0;
            foreach (var rule in nextRulebase.Rules)
            {
                if (!string.IsNullOrEmpty(rule.SectionHeader))
                {
                    continue;
                }
                RulebaseLink? nextRbLink = rulebaseLinks.FirstOrDefault(rbl => rbl.FromRuleId == rule.Id);
                if (nextRbLink != null)
                {
                    ruleCount += 1 + GetRuleCount(mgmReport, nextRbLink, rulebaseLinks);
                }
                else
                {
                    ruleCount++;
                }
            }
            return ruleCount;
        }
            return 0;
        }

        public override string SetDescription()
        {
            int managementCounter = 0;
            int deviceCounter = 0;
            int ruleCounter = 0;
            foreach (var mgt in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    mgt.ContainsRules()))
            {
                managementCounter++;
                var managementReport = new ManagementReportController(mgt);
                foreach (var device in managementReport.Devices.Where(dev => dev.ContainsRules()))
                {
                    deviceCounter++;
                    ruleCounter += GetRuleCount(managementReport, device.RulebaseLinks.FirstOrDefault(_ => _.IsInitialRulebase()), device.RulebaseLinks);
                }
            }
            return $"{managementCounter} {userConfig.GetText("managements")}, {deviceCounter} {userConfig.GetText("gateways")}, {ruleCounter} {userConfig.GetText("rules")}";
        }

        private string ExportSingleRulebaseToCsv(StringBuilder report, RuleDisplayCsv ruleDisplayCsv, ManagementReport managementReport, DeviceReport gateway, RulebaseLink? rbLink)
        {
            if (rbLink == null)
            {
                return report.ToString();
                // from develop:
                // foreach (var dev in mgt.Devices.Where(d => d.Rules != null && d.Rules.Length > 0))
                // {
                //     if (dev.Rules != null)
                //     {
                //         foreach (Rule rule in dev.Rules)
                //         {
                //             rule.ManagementName = mgt.Name ?? "";
                //             rule.DeviceName = dev.Name ?? "";
                //             mgt.ReportedRuleIds.Add(rule.Id);
                //         }
                //     }
                // }
                // mgt.ReportedRuleIds = mgt.ReportedRuleIds.Distinct().ToList();
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
                    // NOSONAR
                    // report.AppendLine("\"section header\": \"" + rule.SectionHeader + "\"");
                }
                ExportSingleRulebaseToCsv(report, ruleDisplayCsv, managementReport, gateway, gateway.RulebaseLinks.FirstOrDefault(_ => _.FromRuleId == rule.Id));
            } // rules 
            return report.ToString();
        }
        public override string ExportToCsv()
        {
            if (!ReportType.IsResolvedReport())
            {
                throw new NotImplementedException();
            }
            StringBuilder report = new();
            RuleDisplayCsv ruleDisplayCsv = new(userConfig);

            report.Append(DisplayReportHeaderCsv());
            report.AppendLine(
                $"\"management-name\",\"device-name\",\"rule-number\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"");

            var managementReports = ReportData.ManagementData.Where(mgt => !mgt.Ignore &&
                                                                           Array.Exists(mgt.Devices,
                                                                               device => device.ContainsRules()));
            foreach (var managementReport in managementReports)
            {
                foreach (var gateway in managementReport.Devices)
                {
                    if (!gateway.ContainsRules())
                    {
                        continue;
                    }

                    if (gateway.RulebaseLinks.FirstOrDefault(rbl => rbl.IsInitialRulebase()) is { } rbLink)
                    {
                        ExportSingleRulebaseToCsv(report, ruleDisplayCsv, managementReport, gateway, rbLink);
                    }
                } // gateways
            } // managements
            return report.ToString();
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
            TryBuildRuleTree();
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
                        
                        var rules = _rulesCache[(gateway.Id, managementReport.Id)];

                        foreach (var rule in rules)
                        {
                            report.Append(ruleDisplayJson.DisplayRuleJsonObject(rule, ReportType));
                        }

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

        public void ConstructHtmlReport(ref StringBuilder report, List<ManagementReport> managementData, int chapterNumber, int levelshift = 0)
        {
            RuleDisplayHtml ruleDisplayHtml = new(userConfig);
            Levelshift = levelshift;

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
                report.AppendLine($"<th>{userConfig.GetText("next_recert_date")}</th>");
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

        // Rulebaselink rbLink not used
        private void AppendRulesForRulebaseHtml(ref StringBuilder report, RulebaseLink rbLink, ManagementReport managementReport, DeviceReport device, int chapterNumber, RuleDisplayHtml ruleDisplayHtml)  // RulebaseLink rbLink not used, can be deleted?
        {
            foreach (var rule in _rulesCache[(device.Id, managementReport.Id)])
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
                report.AppendLine($"<td>{RuleDisplayBase.DisplaySourceZones(rule)}</td>");
                report.AppendLine($"<td>{ruleDisplayHtml.DisplaySource(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayDestinationZones(rule)}</td>");
                report.AppendLine($"<td>{ruleDisplayHtml.DisplayDestination(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplayHtml.DisplayServices(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayAction(rule)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayTrack(rule)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayEnabled(rule, OutputLocation.export)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayUid(rule)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayComment(rule)}</td>");
                report.AppendLine("</tr>");
                if (ReportType == ReportType.UnusedRules || ReportType == ReportType.AppRules)
                {
                    report.AppendLine(RuleDisplayHtml.DisplaySectionHeader(rule, ColumnCount));
                }
            }
        }

        private void AppendObjectsForManagementHtml(ref StringBuilder report, int chapterNumber, ManagementReport managementReport)
        {
            AppendNetworkObjectsForManagementHtml(ref report, chapterNumber, managementReport);
            AppendNetworkServicesForManagementHtml(ref report, chapterNumber, managementReport);
            AppendUsersForManagementHtml(ref report, chapterNumber, managementReport);
        }

        protected void AppendNetworkObjectsForManagementHtml(ref StringBuilder report, int chapterNumber, ManagementReport managementReport)
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

        protected void AppendNetworkServicesForManagementHtml(ref StringBuilder report, int chapterNumber, ManagementReport managementReport)
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
                    AppendServiceForManagementHtml(ref report, chapterNumber, objNumber++, svcobj);
                }
                report.AppendLine("</table>");
                report.AppendLine("<hr>");
            }
        }

        private void AppendServiceForManagementHtml(ref StringBuilder report, int chapterNumber, int objNumber, NetworkService svcobj)
        {
            report.AppendLine("<tr>");
            report.AppendLine($"<td>{objNumber}</td>");
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

        protected void AppendUsersForManagementHtml(ref StringBuilder report, int chapterNumber, ManagementReport managementReport)
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
    }
}
