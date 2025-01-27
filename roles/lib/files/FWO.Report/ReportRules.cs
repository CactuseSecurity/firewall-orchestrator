using FWO.Basics;
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
using System.ComponentModel.DataAnnotations;

namespace FWO.Report
{
    public class ReportRules : ReportDevicesBase
    {
        private const int ColumnCount = 12;
        protected bool UseAdditionalFilter = false;

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
                SetMgtQueryVars(management);
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
            objQueryVariables.Add("importId", managementReport.Import.ImportAggregate.ImportAggregateMax.RelevantImportId!);

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

        public override string SetDescription()
        {
            int managementCounter = 0;
            int deviceCounter = 0;
            int ruleCounter = 0;
            foreach (var managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    mgt.ContainsRules()))
            {
                managementCounter++;
                foreach (var device in managementReport.Devices.Where(dev => dev.ContainsRules()))
                {
                    deviceCounter++;
                    foreach (var rb in device.Rulebases)
                    {
                        ruleCounter += rb.Rulebase.RuleMetadata[0].Rules!.Length;
                    }
                }
            }
            return $"{managementCounter} {userConfig.GetText("managements")}, {deviceCounter} {userConfig.GetText("gateways")}, {ruleCounter} {userConfig.GetText("rules")}";
        }

        private void SetReportedRuleIds()
        {
            foreach (var mgt in ReportData.ManagementData)
            {
                foreach (var dev in mgt.Devices.Where(d => d.ContainsRules()))
                {
                    foreach (var orderedRulebase in dev.Rulebases)
                    {
                        foreach (RuleMetadata metaRule in orderedRulebase.Rulebase.RuleMetadata)
                        {
                            if (metaRule.Rules.Length > 0)
                            {
                                foreach (Rule rule in metaRule.Rules)
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
                            foreach (var rb in gateway.Rulebases)
                            {
                                foreach (var rule in rb.Rulebase.RuleMetadata[0].Rules)
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
                                } // rules
                            }
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
                        foreach (var rb in gateway.Rulebases)
                        {
                            foreach (var rule in rb.Rulebase.RuleMetadata[0].Rules)
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
            RuleDisplayHtml ruleDisplayHtml = new(userConfig);
            int chapterNumber = 0;

            foreach (var managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore && mgt.Devices != null &&
                    Array.Exists(mgt.Devices, device => device.Rulebases != null && device.Rulebases.Length > 0)))
            {
                chapterNumber++;
                managementReport.AssignRuleNumbers();
                report.AppendLine($"<h3>{managementReport.Name}</h3>");
                report.AppendLine("<hr>");

                foreach (var device in managementReport.Devices)
                {
                    if (device.Rulebases != null && device.Rulebases.Length > 0)
                    {
                        AppendRulesForDeviceHtml(ref report, device, chapterNumber, ruleDisplayHtml);
                    }
                }

                // show all objects used in this management's rules
                AppendObjectsForManagementHtml(ref report, chapterNumber, managementReport);
            }

            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        private void appendRuleHeadlineHtml(ref StringBuilder report)
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

        private void AppendRulesForDeviceHtml(ref StringBuilder report, DeviceReport device, int chapterNumber, RuleDisplayHtml ruleDisplayHtml)
        {
            if (device.ContainsRules())
            {
                report.AppendLine($"<h4>{device.Name}</h4>");
                report.AppendLine("<table>");
                appendRuleHeadlineHtml(ref report);
                foreach (var rulebase in device.Rulebases!)
                {
                    foreach (var rule in rulebase.Rulebase.RuleMetadata[0].Rules)
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
                    }
                    report.AppendLine("</table>");
                    report.AppendLine("<hr>");
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
            if (managementReport.ReportObjects != null && !ReportType.IsResolvedReport())
            {
                report.AppendLine($"<h4>{userConfig.GetText("network_objects")}</h4>");
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
                    report.AppendLine($"<tr style=\"{(nwobj.Highlighted ? GlobalConst.kStyleHighlighted : "")}\">");
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
            if (managementReport.ReportServices != null && !ReportType.IsResolvedReport())
            {
                report.AppendLine($"<h4>{userConfig.GetText("network_services")}</h4>");
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
            if (managementReport.ReportUsers != null && !ReportType.IsResolvedReport())
            {
                report.AppendLine($"<h4>{userConfig.GetText("users")}</h4>");
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
