using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Filter;
using FWO.Ui.Display;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;

namespace FWO.Report
{
    public class ReportRules : ReportDevicesBase
    {
        private const int ColumnCount = 12;
        protected bool UseAdditionalFilter = false;
        private bool VarianceMode = false;

        public ReportRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) {}

        public override async Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            Query.QueryVariables[QueryVar.Limit] = elementsPerFetch;
            Query.QueryVariables[QueryVar.Offset] = 0;
            bool keepFetching = true;

            List<ManagementReport> managementsWithRelevantImportId = await GetRelevantImportIds(apiConnection);

            ReportData.ManagementData = [];
            foreach(var management in managementsWithRelevantImportId)
            {
                SetMgtQueryVars(management);
                ManagementReport managementReport = (await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables))[0];
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
                foreach(var management in managementsWithRelevantImportId)
                {
                    SetMgtQueryVars(management);
                    ManagementReport? mgtToFill = ReportData.ManagementData.FirstOrDefault(m => m.Id == management.Id);
                    if(mgtToFill != null)
                    {
                        (bool newObjects, Dictionary<string, int> maxAddedCounts) = mgtToFill.Merge((await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables))[0]);
                        // new objects might have been added, but if none reached the limit of elementsPerFetch, we can stop fetching
                        keepFetching = newObjects && maxAddedCounts.Values.Any(v => v >= elementsPerFetch);
                    }
                }
                await callback(ReportData);
            }
            SetReportedRuleIds();
        }

        private void SetMgtQueryVars(ManagementReport management)
        {
            Query.QueryVariables[QueryVar.MgmId] = management.Id;
            Query.QueryVariables[QueryVar.ImportIdStart] = management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1; /* managment was not yet imported at that time */;
            Query.QueryVariables[QueryVar.ImportIdEnd]   = management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1; /* managment was not yet imported at that time */;
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

            string query = GetQuery(objects);
            bool keepFetching = true;
            int fetchCount = 0;
            int elementsPerFetch = (int)objQueryVariables.GetValueOrDefault(QueryVar.Limit)!;
            ManagementReport filteredObjects;
            ManagementReport allFilteredObjects = new ();
            while (keepFetching && ++fetchCount <= maxFetchCycles)
            {
                filteredObjects = (await apiConnection.SendQueryAsync<List<ManagementReport>>(query, objQueryVariables))[0];

                if (fetchCount == 1)
                {
                    allFilteredObjects = filteredObjects;
                }
                else
                {
                    (bool newObjects, Dictionary<string, int> maxAddedCounts) = allFilteredObjects.MergeReportObjects(filteredObjects);
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
            if(UseAdditionalFilter)
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

        public override string SetDescription()
        {
            int managementCounter = 0;
            int deviceCounter = 0;
            int ruleCounter = 0;
            foreach (var managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.Rules != null && device.Rules.Length > 0)))
            {
                managementCounter++;
                foreach (var device in managementReport.Devices.Where(dev => dev.Rules != null && dev.Rules.Length > 0))
                {
                    deviceCounter++;
                    ruleCounter += device.Rules!.Length;
                }
            }
            return $"{managementCounter} {userConfig.GetText("managements")}, {deviceCounter} {userConfig.GetText("gateways")}, {ruleCounter} {userConfig.GetText("rules")}";
        }

        private void SetReportedRuleIds()
        {
            foreach (var mgt in ReportData.ManagementData)
            {
                foreach (var dev in mgt.Devices.Where(d => d.Rules != null && d.Rules.Length > 0))
                {
                    if (dev.Rules !=null)
                    {
                        foreach (Rule rule in dev.Rules)
                        {
                            rule.ManagementName = mgt.Name ?? "";
                            rule.DeviceName = dev.Name ?? "";
                            mgt.ReportedRuleIds.Add(rule.Id);
                        }
                    }
                }
                mgt.ReportedRuleIds = mgt.ReportedRuleIds.Distinct().ToList();
            }
        }

        public override string ExportToCsv()
        {
            if (ReportType.IsResolvedReport())
            {
                StringBuilder report = new ();
                RuleDisplayCsv ruleDisplayCsv = new (userConfig);

                report.Append(DisplayReportHeaderCsv());
                report.AppendLine($"\"management-name\",\"device-name\",\"rule-number\",\"rule-name\",\"source-zone\",\"source\",\"destination-zone\",\"destination\",\"service\",\"action\",\"track\",\"rule-enabled\",\"rule-uid\",\"rule-comment\"");

                foreach (var managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                        Array.Exists(mgt.Devices, device => device.Rules != null && device.Rules.Length > 0)))
                {
                    foreach (var gateway in managementReport.Devices.Where(g => g.Rules != null && g.Rules.Length > 0))
                    {
                        foreach (var rule in gateway.Rules!.Where(r => string.IsNullOrEmpty(r.SectionHeader)))
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
                            report.AppendLine("");
                        }
                    }
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
            StringBuilder report = new ("{");
            report.Append(DisplayReportHeaderJson());
            report.AppendLine("\"managements\": [");
            RuleDisplayJson ruleDisplayJson = new (userConfig);
            foreach (var managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.Rules != null && device.Rules.Length > 0)))
            {
                report.AppendLine($"{{\"{managementReport.Name}\": {{");
                report.AppendLine($"\"gateways\": [");
                foreach (var gateway in managementReport.Devices.Where(g => g.Rules != null && g.Rules.Length > 0))
                {
                    report.Append($"{{\"{gateway.Name}\": {{\n\"rules\": [");
                    foreach (var rule in gateway.Rules!)
                    {
                        report.Append('{');
                        if (string.IsNullOrEmpty(rule.SectionHeader))
                        {
                            report.Append(ruleDisplayJson.DisplayNumber(rule));
                            report.Append(ruleDisplayJson.DisplayName(rule.Name));
                            report.Append(ruleDisplayJson.DisplaySourceZone(rule.SourceZone?.Name));
                            report.Append(ruleDisplayJson.DisplaySourceNegated(rule.SourceNegated));
                            report.Append(ruleDisplayJson.DisplaySource(rule, ReportType));
                            report.Append(ruleDisplayJson.DisplayDestinationZone(rule.DestinationZone?.Name));
                            report.Append(ruleDisplayJson.DisplayDestinationNegated(rule.DestinationNegated));
                            report.Append(ruleDisplayJson.DisplayDestination(rule, ReportType));
                            report.Append(ruleDisplayJson.DisplayServiceNegated(rule.ServiceNegated));
                            report.Append(ruleDisplayJson.DisplayServices(rule, ReportType));
                            report.Append(ruleDisplayJson.DisplayAction(rule.Action));
                            report.Append(ruleDisplayJson.DisplayTrack(rule.Track));
                            report.Append(ruleDisplayJson.DisplayEnabled(rule.Disabled));
                            report.Append(ruleDisplayJson.DisplayUid(rule.Uid));
                            report.Append(ruleDisplayJson.DisplayComment(rule.Comment));
                            report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last chars (comma)
                        }
                        else
                        {
                            report.AppendLine("\"section header\": \"" + rule.SectionHeader + "\"");
                        }
                        report.Append("},");  // EO rule
                    } // rules
                    report = RuleDisplayBase.RemoveLastChars(report, 1); // remove last char (comma)
                    report.Append(']'); // EO rules
                    report.Append('}'); // EO gateway internal
                    report.Append("},"); // EO gateway external
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
            StringBuilder report = new ();
            int chapterNumber = 0;
            ConstructHtmlReport(ref report, ReportData.ManagementData, chapterNumber);
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        public void ConstructHtmlReport(ref StringBuilder report, List<ManagementReport> managementData, int chapterNumber, bool varianceMode = false)
        {
            RuleDisplayHtml ruleDisplayHtml = new (userConfig);
            VarianceMode = varianceMode;

            foreach (var managementReport in managementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.Rules != null && device.Rules.Length > 0)))
            {
                chapterNumber++;
                managementReport.AssignRuleNumbers();
                report.AppendLine(Headline(managementReport.Name, 3));
                report.AppendLine("<hr>");

                foreach (var device in managementReport.Devices)
                {
                    if (device.Rules != null && device.Rules.Length > 0)
                    {
                        AppendRulesForDeviceHtml(ref report, device, chapterNumber, ruleDisplayHtml);
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
            if(ReportType == ReportType.Recertification)
            {
                report.AppendLine($"<th>{userConfig.GetText("next_recert")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("owner")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("ip_matches")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("last_hit")}</th>");
            }
            if(ReportType == ReportType.UnusedRules || ReportType == ReportType.AppRules)
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

        private void AppendRulesForDeviceHtml(ref StringBuilder report, DeviceReport device, int chapterNumber, RuleDisplayHtml ruleDisplayHtml)
        {
            if (device.ContainsRules())
            {
                report.AppendLine(Headline(device.Name, 4));
                report.AppendLine("<table>");
                AppendRuleHeadlineHtml(ref report);
                foreach (var rule in device.Rules!)
                {
                    AppendRuleForManagementHtml(ref report, chapterNumber, rule, ruleDisplayHtml);
                }
                report.AppendLine("</table>");
                report.AppendLine("<hr>");
            }
        }

        private void AppendRuleForManagementHtml(ref StringBuilder report, int chapterNumber, Rule rule, RuleDisplayHtml ruleDisplayHtml)
        {
            if (string.IsNullOrEmpty(rule.SectionHeader))
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{ruleDisplayHtml.DisplayNumber(rule)}</td>");
                if(ReportType == ReportType.Recertification)
                {
                    report.AppendLine($"<td>{RuleDisplayHtml.DisplayNextRecert(rule)}</td>");
                    report.AppendLine($"<td>{RuleDisplayHtml.DisplayOwner(rule)}</td>");
                    report.AppendLine($"<td>{RuleDisplayHtml.DisplayRecertIpMatches(rule)}</td>");
                    report.AppendLine($"<td>{RuleDisplayHtml.DisplayLastHit(rule)}</td>");
                }
                if(ReportType == ReportType.UnusedRules || ReportType == ReportType.AppRules)
                {
                    report.AppendLine($"<td>{RuleDisplayHtml.DisplayLastHit(rule)}</td>");
                }
                report.AppendLine($"<td>{ruleDisplayHtml.DisplayName(rule)}</td>");
                report.AppendLine($"<td>{ruleDisplayHtml.DisplaySourceZone(rule)}</td>");
                report.AppendLine($"<td>{ruleDisplayHtml.DisplaySource(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplayHtml.DisplayDestinationZone(rule)}</td>");
                report.AppendLine($"<td>{ruleDisplayHtml.DisplayDestination(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplayHtml.DisplayServices(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayAction(rule)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayTrack(rule)}</td>");
                report.AppendLine($"<td>{RuleDisplayHtml.DisplayEnabled(rule, OutputLocation.export)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayUid(rule)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayComment(rule)}</td>");
                report.AppendLine("</tr>");
            }
            else
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td class=\"bg-gray\" colspan=\"{ColumnCount}\">{rule.SectionHeader}</td>");
                report.AppendLine("</tr>");
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
            report.AppendLine($"<td>{((svcobj.Type.Name!=ServiceType.Group && svcobj.Protocol != null) ? svcobj.Protocol.Name : "")}</td>");
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

        private string Headline (string? title, int level)
        {
            int Level = VarianceMode ? level + 2 : level;
            return  $"<h{Level} id=\"{Guid.NewGuid()}\">{title}</h{Level}>";
        }
    }
}
