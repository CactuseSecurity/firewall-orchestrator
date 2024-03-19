using FWO.Api.Data;
using System.Text;
using FWO.Api.Client;
using FWO.Report.Filter;
using System.Text.Json;
using FWO.Config.Api;
using FWO.Logging;

namespace FWO.Report
{
    public class ReportStatistics : ReportDevicesBase
    {
        // TODO: Currently generated in Report.razor as well as here, because of export. Remove dupliacte.
        private ManagementReport globalStatisticsManagement = new ();

        public ReportStatistics(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) {}


        public override async Task Generate(int _, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            List<ManagementReport> managementsWithRelevantImportId = await getRelevantImportIds(apiConnection);

            ReportData.ManagementData = new ();

            foreach (var relevantMgmt in managementsWithRelevantImportId)
            {
                if (ct.IsCancellationRequested)
                {
                    Log.WriteDebug("Generate Statistics Report", "Task cancelled");
                    ct.ThrowIfCancellationRequested();
                }

                // setting mgmt and relevantImporId QueryVariables 
                Query.QueryVariables["mgmId"] = relevantMgmt.Id;
                Query.QueryVariables["relevantImportId"] = relevantMgmt.Import.ImportAggregate.ImportAggregateMax.RelevantImportId ?? -1 /* managment was not yet imported at that time */;
                ReportData.ManagementData.Add((await apiConnection.SendQueryAsync<List<ManagementReport>>(Query.FullQuery, Query.QueryVariables))[0]);
            }
            await callback(ReportData);

            foreach (ManagementReport mgm in ReportData.ManagementData.Where(mgt => !mgt.Ignore))
            {
                globalStatisticsManagement.RuleStatistics.ObjectAggregate.ObjectCount += mgm.RuleStatistics.ObjectAggregate.ObjectCount;
                globalStatisticsManagement.NetworkObjectStatistics.ObjectAggregate.ObjectCount += mgm.NetworkObjectStatistics.ObjectAggregate.ObjectCount;
                globalStatisticsManagement.ServiceObjectStatistics.ObjectAggregate.ObjectCount += mgm.ServiceObjectStatistics.ObjectAggregate.ObjectCount;
                globalStatisticsManagement.UserObjectStatistics.ObjectAggregate.ObjectCount += mgm.UserObjectStatistics.ObjectAggregate.ObjectCount;
            }
        }

        public override async Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            await callback(ReportData);
            // currently no further objects to be fetched
            GotObjectsInReport = true;
            return true;
        }

        public override Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            return Task.FromResult<bool>(true);
        }

        public override string ExportToJson()
        {
            globalStatisticsManagement.Name = "global statistics";
            List<ManagementReport> combinedManagements = new (){ globalStatisticsManagement };
            combinedManagements.AddRange(ReportData.ManagementData.Where(mgt => !mgt.Ignore));
            return JsonSerializer.Serialize(combinedManagements, new JsonSerializerOptions { WriteIndented = true });
        }

        public override string ExportToCsv()
        {
            StringBuilder csvBuilder = new ();

            foreach (ManagementReport managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore))
            {
                //foreach (var item in collection)
                //{

                //}
            }

            throw new NotImplementedException();
        }

        public override string ExportToHtml()
        {
            StringBuilder report = new ();

            report.AppendLine($"<h3>{userConfig.GetText("glob_no_obj")}</h3>");
            report.AppendLine("<table>");
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("network_objects")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("service_objects")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("user_objects")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("rules")}</th>");
            report.AppendLine("</tr>");
            report.AppendLine("<tr>");
            report.AppendLine($"<td>{globalStatisticsManagement.NetworkObjectStatistics.ObjectAggregate.ObjectCount}</td>");
            report.AppendLine($"<td>{globalStatisticsManagement.ServiceObjectStatistics.ObjectAggregate.ObjectCount}</td>");
            report.AppendLine($"<td>{globalStatisticsManagement.UserObjectStatistics.ObjectAggregate.ObjectCount}</td>");
            report.AppendLine($"<td>{globalStatisticsManagement.RuleStatistics.ObjectAggregate.ObjectCount }</td>");
            report.AppendLine("</tr>");
            report.AppendLine("</table>");
            report.AppendLine("<hr>");

            foreach (ManagementReport managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore))
            {
                report.AppendLine($"<h4>{userConfig.GetText("no_of_obj")} - {managementReport.Name}</h4>");
                report.AppendLine("<table>");
                report.AppendLine("<tr>");
                report.AppendLine($"<th>{userConfig.GetText("network_objects")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("service_objects")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("user_objects")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("rules")}</th>");
                report.AppendLine("</tr>");
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{managementReport.NetworkObjectStatistics.ObjectAggregate.ObjectCount}</td>");
                report.AppendLine($"<td>{managementReport.ServiceObjectStatistics.ObjectAggregate.ObjectCount}</td>");
                report.AppendLine($"<td>{managementReport.UserObjectStatistics.ObjectAggregate.ObjectCount}</td>");
                report.AppendLine($"<td>{managementReport.RuleStatistics.ObjectAggregate.ObjectCount }</td>");
                report.AppendLine("</tr>");
                report.AppendLine("</table>");
                report.AppendLine("<br>");

                report.AppendLine($"<h4>{userConfig.GetText("no_rules_gtw")}</h4>");
                report.AppendLine("<table>");
                report.AppendLine("<tr>");
                report.AppendLine($"<th>{userConfig.GetText("gateway")}</th>");
                report.AppendLine($"<th>{userConfig.GetText("rules")}</th>");
                report.AppendLine("</tr>");
                foreach (var device in managementReport.Devices)
                {
                    if (device.RuleStatistics != null)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{device.Name}</td>");
                        report.AppendLine($"<td>{device.RuleStatistics.ObjectAggregate.ObjectCount}</td>");
                        report.AppendLine("</tr>");
                    }
                }
                report.AppendLine("</table>");
                report.AppendLine("<hr>");
            }
            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }
    }
}
