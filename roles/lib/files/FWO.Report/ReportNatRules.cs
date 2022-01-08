﻿using FWO.Api.Data;
using System.Text;
using FWO.ApiClient;
using FWO.Report.Filter;
using FWO.ApiClient.Queries;
using FWO.Ui.Display;
using FWO.Logging;
using FWO.Config.Api;

namespace FWO.Report
{
    public class ReportNatRules : ReportBase
    {
        public ReportNatRules(DynGraphqlQuery query, UserConfig userConfig) : base(query, userConfig) { }

        private const byte all = 0, nobj = 1, nsrv = 2, user = 3;
        public bool GotReportedRuleIds { get; protected set; } = false;

        public async Task GetReportedRuleIds(APIConnection apiConnection)
        {
            List<int> relevantDevIds = DeviceFilter.ExtractSelectedDevIds(Managements);
            for (int i = 0; i < Managements.Length; i++)
            {
                Dictionary<string, object> ruleQueryVariables = new Dictionary<string, object>();
                if (Managements[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                {
                    ruleQueryVariables["importId"] = Managements[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId!;
                    ruleQueryVariables["devIds"] = relevantDevIds;
                    Rule[] rules = await apiConnection.SendQueryAsync<Rule[]>(RuleQueries.getRuleIdsOfImport, ruleQueryVariables);
                    Managements[i].ReportedRuleIds = rules.Select(x => Convert.ToInt64(x.Id)).Distinct().ToList();
                }
            }
            GotReportedRuleIds = true;
        }

        public override async Task GetObjectsInReport(int objectsPerFetch, APIConnection apiConnection, Func<Management[], Task> callback) // to be called when exporting
        {
            // get rule ids per import (= management)
            if (!GotReportedRuleIds)
                await GetReportedRuleIds(apiConnection);

            if (!GotObjectsInReport)
            {

                for (int i = 0; i < Managements.Length; i++)
                {
                    if (Managements[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                    {
                        // set query variables for object query
                        Dictionary<string, object> objQueryVariables = new Dictionary<string, object>
                        {
                            { "mgmIds", Managements[i].Id },
                            { "importId", Managements[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId! },
                            { "limit", objectsPerFetch },
                            { "offset", 0 },
                        };

                        // get objects for this management in the current report
                        await GetObjectsForManagementInReport(objQueryVariables, all, int.MaxValue, apiConnection, callback);
                    }
                }
                GotObjectsInReport = true;
            }
        }

        public override async Task GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, byte objects, int maxFetchCycles, APIConnection apiConnection, Func<Management[], Task> callback)
        {
            if (!objQueryVariables.ContainsKey("mgmIds") || !objQueryVariables.ContainsKey("limit") || !objQueryVariables.ContainsKey("offset"))
                throw new ArgumentException("Given objQueryVariables dictionary does not contain variable for management id, limit or offset");

            int mid = (int)objQueryVariables.GetValueOrDefault("mgmIds")!;
            Management management = Managements.FirstOrDefault(m => m.Id == mid) ?? throw new ArgumentException("Given management id does not exist for this report");
            if (management == null)
                throw new ArgumentException("Given management id does not exist for this report");

            if (!GotReportedRuleIds)
                await GetReportedRuleIds(apiConnection);

            objQueryVariables.Add("ruleIds", management.ReportedRuleIds);

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
        }

        public override async Task Generate(int rulesPerFetch, APIConnection apiConnection, Func<Management[], Task> callback, CancellationToken ct)
        {
            Query.QueryVariables["limit"] = rulesPerFetch;
            Query.QueryVariables["offset"] = 0;
            bool gotNewObjects = true;

            // get the filter line
            string TimeFilter = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Dictionary<string, object> ImpIdQueryVariables = new Dictionary<string, object>();
            if (Query.ReportTime != "")
                TimeFilter = Query.ReportTime;

            // get relevant import ids for report time
            ImpIdQueryVariables["time"] = TimeFilter;
            // todo: only get relevant importIds for devices in the filter
            //    need to convert string gateway filter (gateway="checkPoint_demo") into list of mgmIds
            //    ImpIdQueryVariables["mgmIds"] = mgmIds;
            Management[] managementsWithRelevantImportId = await apiConnection.SendQueryAsync<Management[]>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);

            Managements = new Management[managementsWithRelevantImportId.Length];
            int i;
            for (i = 0; i < managementsWithRelevantImportId.Length; i++)
            {
                // setting mgmt and relevantImporId QueryVariables 
                Query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                if (managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                    Query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId!;
                else    // managment was not yet imported at that time
                    Query.QueryVariables["relevantImportId"] = -1;
                Managements[i] = (await apiConnection.SendQueryAsync<Management[]>(Query.FullQuery, Query.QueryVariables))[0];
                Managements[i].Import = managementsWithRelevantImportId[i].Import;
            }

            while (gotNewObjects)
            {
                if (ct.IsCancellationRequested)
                {
                    Log.WriteDebug("Generate NAT Rule Report", "Task cancelled");
                    ct.ThrowIfCancellationRequested();
                }
                gotNewObjects = false;
                Query.QueryVariables["offset"] = (int)Query.QueryVariables["offset"] + rulesPerFetch;
                for (i = 0; i < managementsWithRelevantImportId.Length; i++)
                {
                    if (managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId != null)
                        Query.QueryVariables["relevantImportId"] = managementsWithRelevantImportId[i].Import.ImportAggregate.ImportAggregateMax.RelevantImportId!;
                    else
                        Query.QueryVariables["relevantImportId"] = -1; // managment was not yet imported at that time
                    Query.QueryVariables["mgmId"] = managementsWithRelevantImportId[i].Id;
                    gotNewObjects = gotNewObjects | Managements[i].Merge((await apiConnection.SendQueryAsync<Management[]>(Query.FullQuery, Query.QueryVariables))[0]);
                }
                await callback(Managements);
            }
        }

        public override string ExportToCsv()
        {
            StringBuilder csvBuilder = new StringBuilder();

            foreach (Management management in Managements.Where(mgt => !mgt.Ignore))
            {
                //foreach (var item in collection)
                //{

                //}
            }

            throw new NotImplementedException();
        }

        //public override string ToJson()
        //{
        //    return JsonSerializer.Serialize(Managements, new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = ReferenceHandler.Preserve });
        //}

        private const int ColumnCount = 12;

        public override string ExportToHtml()
        {
            StringBuilder report = new StringBuilder();
            NatRuleDisplay ruleDisplay = new NatRuleDisplay(userConfig);

            foreach (Management management in Managements.Where(mgt => !mgt.Ignore))
            {
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
                        report.AppendLine($"<th>{userConfig.GetText("trans_source")}</th>");
                        report.AppendLine($"<th>{userConfig.GetText("trans_destination")}</th>");
                        report.AppendLine($"<th>{userConfig.GetText("trans_services")}</th>");
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
                                report.AppendLine($"<td>{ruleDisplay.DisplaySource(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayDestinationZone(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayDestination(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayService(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedSource(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedDestination(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedService(rule)}</td>");
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
                if (management.ReportObjects != null)
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
                    foreach (NetworkObjectWrapper nwobj in management.ReportObjects)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td><a name=nwobj{nwobj.Content.Id}>{nwobj.Content.Name}</a></td>");
                        report.AppendLine($"<td>{nwobj.Content.Type.Name}</td>");
                        report.AppendLine($"<td>{nwobj.Content.IP}</td>");
                        if (nwobj.Content.MemberNames != null && nwobj.Content.MemberNames.Contains("|"))
                            report.AppendLine($"<td>{string.Join("<br>", nwobj.Content.MemberNames.Split('|'))}</td>");
                        else
                            report.AppendLine($"<td>{nwobj.Content.MemberNames}</td>");
                        report.AppendLine($"<td>{nwobj.Content.Uid}</td>");
                        report.AppendLine($"<td>{nwobj.Content.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                }

                if (management.ReportServices != null)
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
                    foreach (ServiceWrapper svcobj in management.ReportServices)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td>{svcobj.Content.Name}</td>");
                        report.AppendLine($"<td><a name=svc{svcobj.Content.Id}>{svcobj.Content.Name}</a></td>");
                        report.AppendLine($"<td>{((svcobj.Content.Protocol!=null)?svcobj.Content.Protocol.Name:"")}</td>");
                        if (svcobj.Content.DestinationPortEnd != null && svcobj.Content.DestinationPortEnd != svcobj.Content.DestinationPort)
                            report.AppendLine($"<td>{svcobj.Content.DestinationPort}-{svcobj.Content.DestinationPortEnd}</td>");
                        else
                            report.AppendLine($"<td>{svcobj.Content.DestinationPort}</td>");
                        if (svcobj.Content.MemberNames != null && svcobj.Content.MemberNames.Contains("|"))
                            report.AppendLine($"<td>{string.Join("<br>", svcobj.Content.MemberNames.Split('|'))}</td>");
                        else 
                            report.AppendLine($"<td>{svcobj.Content.MemberNames}</td>");
                        report.AppendLine($"<td>{svcobj.Content.Uid}</td>");
                        report.AppendLine($"<td>{svcobj.Content.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                }

                if (management.ReportUsers != null)
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
                    foreach (UserWrapper userobj in management.ReportUsers)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td>{userobj.Content.Name}</td>");
                        report.AppendLine($"<td><a name=user{userobj.Content.Id}>{userobj.Content.Name}</a></td>");
                        if (userobj.Content.MemberNames != null && userobj.Content.MemberNames.Contains("|"))
                            report.AppendLine($"<td>{string.Join("<br>", userobj.Content.MemberNames.Split('|'))}</td>");
                        else
                            report.AppendLine($"<td>{userobj.Content.MemberNames}</td>");
                        report.AppendLine($"<td>{userobj.Content.Uid}</td>");
                        report.AppendLine($"<td>{userobj.Content.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                }

                report.AppendLine("</table>");
            }

            return GenerateHtmlFrame(title: userConfig.GetText("natrules_report"), Query.RawFilter, DateTime.Now, report);
        }
    }
}
