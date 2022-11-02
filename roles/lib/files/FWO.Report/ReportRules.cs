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
    public class ReportRules : ReportBase
    {
        public ReportRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

        private const byte all = 0, nobj = 1, nsrv = 2, user = 3;
        public bool GotReportedRuleIds { get; protected set; } = false;

        public async Task GetReportedRuleIds(ApiConnection apiConnection)
        {
            List<int> relevantDevIds = DeviceFilter.ExtractSelectedDevIds(Managements);
            if (relevantDevIds.Count == 0)
                relevantDevIds = DeviceFilter.ExtractAllDevIds(Managements);

            for (int i = 0; i < Managements.Length; i++)
            {
                Dictionary<string, object> ruleQueryVariables = new Dictionary<string, object>();
                if (Managements[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                {
                    ruleQueryVariables["importId"] = Managements[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId!;
                    ruleQueryVariables["devIds"] = relevantDevIds;
                    Rule[] rules = await apiConnection.SendQueryAsync<Rule[]>(RuleQueries.getRuleIdsOfImport, ruleQueryVariables);
                    Managements[i].ReportedRuleIds = rules.Select(x => x.Id).Distinct().ToList();
                }
            }
            GotReportedRuleIds = true;
        }

        public override async Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<Management[], Task> callback) // to be called when exporting
        {
            // get rule ids per import (= management)
            if (!GotReportedRuleIds)
                await GetReportedRuleIds(apiConnection);

            bool gotAllObjects = true; //whether the fetch count limit was reached during fetching

            if (!GotObjectsInReport)
            {
                for (int i = 0; i < Managements.Length; i++)
                {
                    if (Managements[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId is not null)
                    {
                        // set query variables for object query
                        var objQueryVariables = new Dictionary<string, object>
                        {
                            { "mgmIds", Managements[i].Id },
                            { "limit", objectsPerFetch },
                            { "offset", 0 },
                        };

                        // get objects for this management in the current report
                        gotAllObjects &= await GetObjectsForManagementInReport(objQueryVariables, all, int.MaxValue, apiConnection, callback);
                    }
                }
                GotObjectsInReport = true;
            }

            return gotAllObjects;
        }

        public override async Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, byte objects, int maxFetchCycles, ApiConnection apiConnection, Func<Management[], Task> callback)
        {
            if (!objQueryVariables.ContainsKey("mgmIds") || !objQueryVariables.ContainsKey("limit") || !objQueryVariables.ContainsKey("offset"))
                throw new ArgumentException("Given objQueryVariables dictionary does not contain variable for management id, limit or offset");

            int mid = (int)objQueryVariables.GetValueOrDefault("mgmIds")!;
            Management management = Managements.FirstOrDefault(m => m.Id == mid) ?? throw new ArgumentException("Given management id does not exist for this report");

            if (!GotReportedRuleIds)
                await GetReportedRuleIds(apiConnection);

            objQueryVariables.Add("ruleIds", "{" + string.Join(", ", management.ReportedRuleIds) + "}");
            objQueryVariables.Add("importId", management.Import.ImportAggregate.ImportAggregateMax.RelevantImportId!);

            string query = "";
            switch (objects)
            {
                case all:
                    query = ObjectQueries.getReportFilteredObjectDetails; break;
                case nobj:
                    query = ObjectQueries.getReportFilteredNetworkObjectDetails; break;
                case nsrv:
                    query = ObjectQueries.getReportFilteredNetworkServiceObjectDetails; break;
                case user:
                    query = ObjectQueries.getReportFilteredUserDetails; break;
            }

            bool newObjects = true;
            int fetchCount = 0;
            int elementsPerFetch = (int)objQueryVariables.GetValueOrDefault("limit")!;
            Management filteredObjects;
            Management allFilteredObjects = new Management();
            while (newObjects && ++fetchCount <= maxFetchCycles)
            {
                filteredObjects = (await apiConnection.SendQueryAsync<Management[]>(query, objQueryVariables))[0];

                if (fetchCount == 1)
                {
                    allFilteredObjects = filteredObjects;
                }
                else
                {
                    newObjects = allFilteredObjects.MergeReportObjects(filteredObjects);
                }

                if (objects == all || objects == nobj)
                    management.ReportObjects = allFilteredObjects.ReportObjects;
                if (objects == all || objects == nsrv)
                    management.ReportServices = allFilteredObjects.ReportServices;
                if (objects == all || objects == user)
                    management.ReportUsers = allFilteredObjects.ReportUsers;

                objQueryVariables["offset"] = (int)objQueryVariables["offset"] + elementsPerFetch;

                await callback(Managements);
            }

            Log.WriteDebug("Lazy Fetch", $"Fetched sidebar objects in {fetchCount - 1} cycle(s) ({elementsPerFetch} at a time)");

            return fetchCount <= maxFetchCycles;
        }

        public override async Task Generate(int rulesPerFetch, ApiConnection apiConnection, Func<Management[], Task> callback, CancellationToken ct)
        {
             Query.QueryVariables["limit"] = rulesPerFetch;
            Query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;

            Management[] managementsWithRelevantImportId = await getRelevantImportIds(apiConnection);

            Managements = new Management[managementsWithRelevantImportId.Length];
            int i;
            for (i = 0; i < managementsWithRelevantImportId.Length; i++)
            {
                // setting mgmt and relevantImporId QueryVariables 
                Query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                Query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1 /* managment was not yet imported at that time */;
                Managements[i] = (await apiConnection.SendQueryAsync<Management[]>(Query.FullQuery, Query.QueryVariables))[0];
                Managements[i].Import = managementsWithRelevantImportId[i].Import;
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
                for (i = 0; i < managementsWithRelevantImportId.Length; i++)
                {
                    Query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                    Query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1; /* managment was not yet imported at that time */;
                    gotNewObjects |= Managements[i].Merge((await apiConnection.SendQueryAsync<Management[]>(Query.FullQuery, Query.QueryVariables))[0]);
                }
                await callback(Managements);
            }
       }

        public override string SetDescription()
        {
            int managementCounter = 0;
            int deviceCounter = 0;
            int ruleCounter = 0;
            foreach (Management management in Managements.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.Rules != null && device.Rules.Length > 0)))
            {
                managementCounter++;
                foreach (Device device in management.Devices.Where(dev => dev.Rules != null && dev.Rules.Length > 0))
                {
                    deviceCounter++;
                    ruleCounter += device.Rules.Length;
                }
            }
            return $"{managementCounter} {userConfig.GetText("managements")}, {deviceCounter} {userConfig.GetText("gateways")}, {ruleCounter} {userConfig.GetText("rules")}";
        }

        public override string ExportToCsv()
        {

            if (ReportType == ReportType.ResolvedRules || ReportType == ReportType.ResolvedRulesTech)
            {
                StringBuilder report = new StringBuilder("");
                report.AppendLine($"# report type: {userConfig.GetText("resolved_rules_report")}");
                report.AppendLine($"# report generation date: {DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)");
                report.AppendLine($"# date of configuration shown: {DateTime.Parse(Query.ReportTimeString).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)");
                report.AppendLine($"# device filter: {string.Join("; ", Array.ConvertAll(Managements, management => management.NameAndDeviceNames()))}");
                report.AppendLine($"# other filters: {Query.RawFilter}");
                report.AppendLine($"# report generator: Firewall Orchestrator - https://fwo.cactus.de/en");
                report.AppendLine($"# data protection level: For internal use only");
                // report.AppendLine("# managements\": [");
                RuleDisplayCsv ruleDisplay = new RuleDisplayCsv(userConfig);
                foreach (Management management in Managements.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                        Array.Exists(mgt.Devices, device => device.Rules != null && device.Rules.Length > 0)))
                {
                    foreach (Device gateway in management.Devices)
                    {
                        if (gateway.Rules != null && gateway.Rules.Length > 0)
                        {
                            foreach (Rule rule in gateway.Rules)
                            {
                                if (string.IsNullOrEmpty(rule.SectionHeader))
                                {
                                    report.Append($"\"{management.Name}\",");
                                    report.Append($"\"{gateway.Name}\",");
                                    report.Append(ruleDisplay.DisplayNumber(rule, gateway.Rules));
                                    report.Append(ruleDisplay.DisplayName(rule));
                                    report.Append(ruleDisplay.DisplaySourceZone(rule));
                                    report.Append(ruleDisplay.DisplaySource(rule, location: "", reportType: this.ReportType));
                                    report.Append(ruleDisplay.DisplayDestinationZone(rule));
                                    report.Append(ruleDisplay.DisplayDestination(rule, location: "", reportType: this.ReportType));
                                    report.Append(ruleDisplay.DisplayService(rule, location: "", reportType: this.ReportType));
                                    report.Append(ruleDisplay.DisplayAction(rule));
                                    report.Append(ruleDisplay.DisplayTrack(rule));
                                    report.Append(ruleDisplay.DisplayEnabled(rule, export: true));
                                    report.Append(ruleDisplay.DisplayUid(rule));
                                    report.Append(ruleDisplay.DisplayComment(rule));
                                }
                                else
                                {
                                    // report.AppendLine("\"section header\": \"" + rule.SectionHeader + "\"");
                                }
                                report.AppendLine("");  // EO rule
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
                return null;
            }
        }

        public override string ExportToJson()
        {
            if (ReportType == ReportType.ResolvedRules || ReportType == ReportType.ResolvedRulesTech)
            {
                StringBuilder report = new StringBuilder("{");
                report.AppendLine($"\"report type\": \"{userConfig.GetText("resolved_rules_report")}\",");
                report.AppendLine($"\"report generation date\": \"{DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)\",");
                report.AppendLine($"\"date of configuration shown\": \"{DateTime.Parse(Query.ReportTimeString).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")} (UTC)\",");
                report.AppendLine($"\"device filter\": \"{string.Join("; ", Array.ConvertAll(Managements, management => management.NameAndDeviceNames()))}\",");
                report.AppendLine($"\"other filters\": \"{Query.RawFilter}\",");
                report.AppendLine($"\"report generator\": \"Firewall Orchestrator - https://fwo.cactus.de/en\",");
                report.AppendLine($"\"data protection level\": \"For internal use only\",");
                report.AppendLine("\"managements\": [");
                RuleDisplayJson ruleDisplay = new RuleDisplayJson(userConfig);
                foreach (Management management in Managements.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                        Array.Exists(mgt.Devices, device => device.Rules != null && device.Rules.Length > 0)))
                {
                    report.AppendLine($"{{\"{management.Name}\": {{");
                    report.AppendLine($"\"gateways\": [{{");
                    foreach (Device gateway in management.Devices)
                    {
                        if (gateway.Rules != null && gateway.Rules.Length > 0)
                        {
                            report.Append($"\"{gateway.Name}\": {{\n\"rules\": [");
                            foreach (Rule rule in gateway.Rules)
                            {
                                report.Append($"{{");
                                if (string.IsNullOrEmpty(rule.SectionHeader))
                                {
                                    report.Append(ruleDisplay.DisplayNumber(rule, gateway.Rules));
                                    report.Append(ruleDisplay.DisplayName(rule));
                                    report.Append(ruleDisplay.DisplaySourceZone(rule));
                                    report.Append(ruleDisplay.DisplaySource(rule, location: "", reportType: this.ReportType));
                                    report.Append(ruleDisplay.DisplayDestinationZone(rule));
                                    report.Append(ruleDisplay.DisplayDestination(rule, location: "", reportType: this.ReportType));
                                    report.Append(ruleDisplay.DisplayService(rule, location: "", reportType: this.ReportType));
                                    report.Append(ruleDisplay.DisplayAction(rule));
                                    report.Append(ruleDisplay.DisplayTrack(rule));
                                    report.Append(ruleDisplay.DisplayEnabled(rule, export: true));
                                    report.Append(ruleDisplay.DisplayUid(rule));
                                    report.Append(ruleDisplay.DisplayComment(rule));
                                    report = ruleDisplay.RemoveLastChars(report, 1); // remove last chars (comma)
                                }
                                else
                                {
                                    report.AppendLine("\"section header\": \"" + rule.SectionHeader + "\"");
                                }
                                report.Append("},");  // EO rule
                            } // rules
                            report = ruleDisplay.RemoveLastChars(report, 1); // remove last char (comma)
                            report.Append("]"); // EO rules
                            report.Append("}},"); // EO gateway 2x
                        }
                    } // gateways
                    report = ruleDisplay.RemoveLastChars(report, 1); // remove last char (comma)
                    report.Append("]"); // EO devices
                    report.Append("}},"); // EO management 2x
                } // managements
                report = ruleDisplay.RemoveLastChars(report, 1); // remove last char (comma)
                report.Append("]"); // EO managements
                report.Append("}"); // EO top

                // Debug:
                string repStr = report.ToString();
                dynamic json = JsonConvert.DeserializeObject(report.ToString());
                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.Formatting = Formatting.Indented;
                return Newtonsoft.Json.JsonConvert.SerializeObject(json, settings);
            }
            else if (ReportType == ReportType.Rules)
            {
                return System.Text.Json.JsonSerializer.Serialize(Managements.Where(mgt => !mgt.Ignore), new JsonSerializerOptions { WriteIndented = true });
            }
            else if (ReportType == ReportType.NatRules)
            {
                return System.Text.Json.JsonSerializer.Serialize(Managements.Where(mgt => !mgt.Ignore), new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                return null;
            }
        }

        private const int ColumnCount = 12;

        public override string ExportToHtml()
        {
            StringBuilder report = new StringBuilder();
            RuleDisplayHtml ruleDisplay = new RuleDisplayHtml(userConfig);

            foreach (Management management in Managements.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.Rules != null && device.Rules.Length > 0)))
            {
                management.AssignRuleNumbers();

                report.AppendLine($"<h3>{management.Name}</h3>");
                report.AppendLine("<hr>");

                foreach (Device device in management.Devices)
                {
                    if (device.Rules != null && device.Rules.Length > 0)
                    {
                        report.AppendLine($"<h4>{device.Name}</h4>");
                        report.AppendLine("<hr>");

                        report.AppendLine("<table>");
                        report.AppendLine("<tr>");
                        report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
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

                        foreach (Rule rule in device.Rules)
                        {
                            if (string.IsNullOrEmpty(rule.SectionHeader))
                            {
                                report.AppendLine("<tr>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayNumber(rule, device.Rules)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayName(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplaySourceZone(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplaySource(rule, location: "", reportType: this.ReportType)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayDestinationZone(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayDestination(rule, location: "", reportType: this.ReportType)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayService(rule, location: "", reportType: this.ReportType)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayAction(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayTrack(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayEnabled(rule, export: true)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayUid(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayComment(rule)}</td>");
                                report.AppendLine("</tr>");
                            }
                            else
                            {
                                report.AppendLine("<tr>");
                                report.AppendLine($"<td style=\"background-color: #f0f0f0;\" colspan=\"{ColumnCount}\">{rule.SectionHeader}</td>");
                                report.AppendLine("</tr>");
                            }
                        }

                        report.AppendLine("</table>");
                    }
                }

                // show all objects used in this management's rules

                int objNumber = 1;
                if (management.ReportObjects != null && ReportType == ReportType.Rules)
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
                    foreach (NetworkObject nwobj in management.ReportObjects)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td><a name=nwobj{nwobj.Id}>{nwobj.Name}</a></td>");
                        report.AppendLine($"<td>{nwobj.Type.Name}</td>");
                        report.AppendLine($"<td>{nwobj.IP}{(nwobj.IpEnd != null && nwobj.IpEnd != "" && nwobj.IpEnd != nwobj.IP ? $"-{nwobj.IpEnd}" : "")}</td>");
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

                if (management.ReportServices != null && ReportType == ReportType.Rules)
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
                    foreach (NetworkService svcobj in management.ReportServices)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td>{svcobj.Name}</td>");
                        report.AppendLine($"<td><a name=svc{svcobj.Id}>{svcobj.Name}</a></td>");
                        report.AppendLine($"<td>{((svcobj.Protocol != null) ? svcobj.Protocol.Name : "")}</td>");
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

                if (management.ReportUsers != null && ReportType == ReportType.Rules)
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
                    foreach (NetworkUser userobj in management.ReportUsers)
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

                report.AppendLine("</table>");
            }

            return GenerateHtmlFrame(title: userConfig.GetText("rules_report"), Query.RawFilter, DateTime.Now, report);
        }
    }
}
