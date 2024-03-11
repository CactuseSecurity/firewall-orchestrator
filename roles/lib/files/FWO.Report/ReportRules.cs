using FWO.Api.Data;
using System.Text;
using FWO.Api.Client;
using FWO.Report.Filter;
using FWO.Api.Client.Queries;
using FWO.Ui.Display;
using FWO.Logging;
using FWO.Config.Api;
using System.Text.Json;
using Newtonsoft.Json;

namespace FWO.Report
{
    public class ReportRules : ReportDevicesBase
    {
        private const int ColumnCount = 12;

        public ReportRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) {}

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            Query.QueryVariables["limit"] = rulesPerFetch;
            Query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;

            List<ManagementReport> managementsWithRelevantImportId = await getRelevantImportIds(apiConnection);

            ReportData.ManagementData = new ();
            foreach(var management in managementsWithRelevantImportId)
            {
                Query.QueryVariables["mgmId"] = management.Id;
                if (ReportType != ReportType.Recertification)
                {
                    Query.QueryVariables["relevantImportId"] = management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1 /* managment was not yet imported at that time */;
                }
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
                foreach(var management in managementsWithRelevantImportId)
                {
                    Query.QueryVariables["mgmId"] = management.Id;
                    if (ReportType != ReportType.Recertification)
                    {
                        Query.QueryVariables["relevantImportId"] = management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1; /* managment was not yet imported at that time */;
                    }
                    ManagementReport? mgtToFill = ReportData.ManagementData.FirstOrDefault(m => m.Id == management.Id);
                    if(mgtToFill != null)
                    {
                        gotNewObjects |= mgtToFill.Merge((await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables))[0]);
                    }
                }
                await callback(ReportData);
            }
            SetReportedRuleIds();
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
            objQueryVariables.Add("importId", managementReport.Import.ImportAggregate.ImportAggregateMax.RelevantImportId!);

            string query = "";
            switch (objects)
            {
                case ObjCategory.all:
                    query = ObjectQueries.getReportFilteredObjectDetails; break;
                case ObjCategory.nobj:
                    query = ObjectQueries.getReportFilteredNetworkObjectDetails; break;
                case ObjCategory.nsrv:
                    query = ObjectQueries.getReportFilteredNetworkServiceObjectDetails; break;
                case ObjCategory.user:
                    query = ObjectQueries.getReportFilteredUserDetails; break;
            }

            bool newObjects = true;
            int fetchCount = 0;
            int elementsPerFetch = (int)objQueryVariables.GetValueOrDefault("limit")!;
            ManagementReport filteredObjects;
            ManagementReport allFilteredObjects = new ();
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
                foreach (var dev in mgt.Devices.Where(d => (d.Rules != null && d.Rules.Length > 0)))
                {
                    foreach (Rule rule in dev.Rules)
                    {
                        mgt.ReportedRuleIds.Add(rule.Id);
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
                    foreach (var gateway in managementReport.Devices)
                    {
                        if (gateway.Rules != null && gateway.Rules.Length > 0)
                        {
                            foreach (var rule in gateway.Rules)
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
                                    report = ruleDisplayCsv.RemoveLastChars(report, 1); // remove last chars (comma)
                                    report.AppendLine("");  // EO rule
                                }
                                else
                                {
                                    // report.AppendLine("\"section header\": \"" + rule.SectionHeader + "\"");
                                }
                            } // rules
                        }
                    } // gateways
                } // managements
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
                foreach (var gateway in managementReport.Devices)
                {
                    if (gateway.Rules != null && gateway.Rules.Length > 0)
                    {
                        report.Append($"{{\"{gateway.Name}\": {{\n\"rules\": [");
                        foreach (var rule in gateway.Rules)
                        {
                            report.Append("{");
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
                                report = ruleDisplayJson.RemoveLastChars(report, 1); // remove last chars (comma)
                            }
                            else
                            {
                                report.AppendLine("\"section header\": \"" + rule.SectionHeader + "\"");
                            }
                            report.Append("},");  // EO rule
                        } // rules
                        report = ruleDisplayJson.RemoveLastChars(report, 1); // remove last char (comma)
                        report.Append("]"); // EO rules
                        report.Append("}"); // EO gateway internal
                        report.Append("},"); // EO gateway external
                    }
                } // gateways
                report = ruleDisplayJson.RemoveLastChars(report, 1); // remove last char (comma)
                report.Append("]"); // EO gateways
                report.Append("}"); // EO management internal
                report.Append("},"); // EO management external
            } // managements
            report = ruleDisplayJson.RemoveLastChars(report, 1); // remove last char (comma)
            report.Append("]"); // EO managements
            report.Append("}"); // EO top

            dynamic? json = JsonConvert.DeserializeObject(report.ToString());
            JsonSerializerSettings settings = new ();
            settings.Formatting = Formatting.Indented;
            return JsonConvert.SerializeObject(json, settings);            
        }

        public override string ExportToHtml()
        {
            StringBuilder report = new ();
            RuleDisplayHtml ruleDisplayHtml = new (userConfig);

            foreach (var managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.Rules != null && device.Rules.Length > 0)))
            {
                managementReport.AssignRuleNumbers();

                report.AppendLine($"<h3>{managementReport.Name}</h3>");
                report.AppendLine("<hr>");

                foreach (var device in managementReport.Devices)
                {
                    if (device.Rules != null && device.Rules.Length > 0)
                    {
                        appendRulesForDeviceHtml(ref report, device, ruleDisplayHtml);
                    }
                }

                // show all objects used in this management's rules
                appendObjectsForManagementHtml(ref report, managementReport);
            }

            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        private void appendRuleHeadlineHtml(ref StringBuilder report)
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
            if(ReportType == ReportType.UnusedRules)
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

        private void appendRulesForDeviceHtml(ref StringBuilder report, DeviceReport device, RuleDisplayHtml ruleDisplayHtml)
        {
            if (device.ContainsRules())
            {
                report.AppendLine($"<h4>{device.Name}</h4>");
                report.AppendLine("<hr>");
                report.AppendLine("<table>");
                appendRuleHeadlineHtml(ref report);
                foreach (var rule in device.Rules!)
                {
                    if (string.IsNullOrEmpty(rule.SectionHeader))
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplayNumber(rule)}</td>");
                        if(ReportType == ReportType.Recertification)
                        {
                            report.AppendLine($"<td>{ruleDisplayHtml.DisplayNextRecert(rule)}</td>");
                            report.AppendLine($"<td>{ruleDisplayHtml.DisplayOwner(rule)}</td>");
                            report.AppendLine($"<td>{ruleDisplayHtml.DisplayRecertIpMatches(rule)}</td>");
                            report.AppendLine($"<td>{ruleDisplayHtml.DisplayLastHit(rule)}</td>");
                        }
                        if(ReportType == ReportType.UnusedRules)
                        {
                            report.AppendLine($"<td>{ruleDisplayHtml.DisplayLastHit(rule)}</td>");
                        }
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplayName(rule)}</td>");
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplaySourceZone(rule)}</td>");
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplaySource(rule, OutputLocation.export, ReportType)}</td>");
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplayDestinationZone(rule)}</td>");
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplayDestination(rule, OutputLocation.export, ReportType)}</td>");
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplayServices(rule, OutputLocation.export, ReportType)}</td>");
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplayAction(rule)}</td>");
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplayTrack(rule)}</td>");
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplayEnabled(rule, OutputLocation.export)}</td>");
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplayUid(rule)}</td>");
                        report.AppendLine($"<td>{ruleDisplayHtml.DisplayComment(rule)}</td>");
                        report.AppendLine("</tr>");
                    }
                    else
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td class=\"bg-gray\" colspan=\"{ColumnCount}\">{rule.SectionHeader}</td>");
                        report.AppendLine("</tr>");
                    }
                }
                report.AppendLine("</table>");
            }
        }

        private void appendObjectsForManagementHtml(ref StringBuilder report, ManagementReport managementReport)
        {
            int objNumber = 1;
            appendNetworkObjectsForManagementHtml(ref report, ref objNumber, managementReport);
            appendNetworkServicesForManagementHtml(ref report, ref objNumber, managementReport);
            appendUsersForManagementHtml(ref report, ref objNumber, managementReport);
        }

        private void appendNetworkObjectsForManagementHtml(ref StringBuilder report, ref int objNumber, ManagementReport managementReport)
        {
            if (managementReport.ReportObjects != null && !ReportType.IsResolvedReport())
            {
                report.AppendLine($"<h4>{userConfig.GetText("network_objects")}</h4>");
                report.AppendLine("<hr>");
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
                foreach (var nwobj in managementReport.ReportObjects)
                {
                    report.AppendLine("<tr>");
                    report.AppendLine($"<td>{objNumber++}</td>");
                    report.AppendLine($"<td><a name=nwobj{nwobj.Id}>{nwobj.Name}</a></td>");
                    report.AppendLine($"<td>{nwobj.Type.Name}</td>");
                    report.AppendLine($"<td>{NwObjDisplay.DisplayIp(nwobj.IP, nwobj.IpEnd, nwobj.Type.Name)}</td>");
                    if (nwobj.MemberNames != null && nwobj.MemberNames.Contains('|'))
                        report.AppendLine($"<td>{string.Join("<br>", nwobj.MemberNames.Split('|'))}</td>");
                    else
                        report.AppendLine($"<td>{nwobj.MemberNames}</td>");
                    report.AppendLine($"<td>{nwobj.Uid}</td>");
                    report.AppendLine($"<td>{nwobj.Comment}</td>");
                    report.AppendLine("</tr>");
                }
                report.AppendLine("</table>");
            }
        }

        private void appendNetworkServicesForManagementHtml(ref StringBuilder report, ref int objNumber, ManagementReport managementReport)
        {
            if (managementReport.ReportServices != null && !ReportType.IsResolvedReport())
            {
                report.AppendLine($"<h4>{userConfig.GetText("network_services")}</h4>");
                report.AppendLine("<hr>");
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
                objNumber = 1;
                foreach (var svcobj in managementReport.ReportServices)
                {
                    report.AppendLine("<tr>");
                    report.AppendLine($"<td>{objNumber++}</td>");
                    report.AppendLine($"<td>{svcobj.Name}</td>");
                    report.AppendLine($"<td><a name=svc{svcobj.Id}>{svcobj.Name}</a></td>");
                    report.AppendLine($"<td>{((svcobj.Type.Name!=ObjectType.Group && svcobj.Protocol != null) ? svcobj.Protocol.Name : "")}</td>");
                    if (svcobj.DestinationPortEnd != null && svcobj.DestinationPortEnd != svcobj.DestinationPort)
                        report.AppendLine($"<td>{svcobj.DestinationPort}-{svcobj.DestinationPortEnd}</td>");
                    else
                        report.AppendLine($"<td>{svcobj.DestinationPort}</td>");
                    if (svcobj.MemberNames != null && svcobj.MemberNames.Contains("|"))
                        report.AppendLine($"<td>{string.Join("<br>", svcobj.MemberNames.Split('|'))}</td>");
                    else
                        report.AppendLine($"<td>{svcobj.MemberNames}</td>");
                    report.AppendLine($"<td>{svcobj.Uid}</td>");
                    report.AppendLine($"<td>{svcobj.Comment}</td>");
                    report.AppendLine("</tr>");
                }
                report.AppendLine("</table>");
            }
        }

        private void appendUsersForManagementHtml(ref StringBuilder report, ref int objNumber, ManagementReport managementReport)
        {
            if (managementReport.ReportUsers != null && !ReportType.IsResolvedReport())
            {
                report.AppendLine($"<h4>{userConfig.GetText("users")}</h4>");
                report.AppendLine("<hr>");
                report.AppendLine("<table>");
                report.AppendLine("<tr>");
                report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("type")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                report.AppendLine("</tr>");
                objNumber = 1;
                foreach (var userobj in managementReport.ReportUsers)
                {
                    report.AppendLine("<tr>");
                    report.AppendLine($"<td>{objNumber++}</td>");
                    report.AppendLine($"<td>{userobj.Name}</td>");
                    report.AppendLine($"<td><a name=user{userobj.Id}>{userobj.Name}</a></td>");
                    if (userobj.MemberNames != null && userobj.MemberNames.Contains("|"))
                        report.AppendLine($"<td>{string.Join("<br>", userobj.MemberNames.Split('|'))}</td>");
                    else
                        report.AppendLine($"<td>{userobj.MemberNames}</td>");
                    report.AppendLine($"<td>{userobj.Uid}</td>");
                    report.AppendLine($"<td>{userobj.Comment}</td>");
                    report.AppendLine("</tr>");
                }
                report.AppendLine("</table>");
            }
        }
    }
}
