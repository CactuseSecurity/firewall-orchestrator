using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Services;

namespace FWO.Report
{
    public class ReportGenerator
    {
        public static async Task<ReportBase?> Generate(ReportTemplate reportTemplate, ApiConnection apiConnection, UserConfig userConfig, CancellationToken? token = null)
        {
            try
            {
                ReportBase report = ReportBase.ConstructReport(reportTemplate, userConfig);
                CancellationToken canToken = token == null ? new () : (CancellationToken)token;
                await DoGeneration(report, reportTemplate, apiConnection, userConfig, canToken);
                return report;
            }
            catch (Exception exception)
            {
                Log.WriteError("Report Generator", $"Generating report leads to exception.", exception);
                return null;
            }
        }

        private static async Task DoGeneration(ReportBase report, ReportTemplate reportTemplate, ApiConnection apiConnection, UserConfig userConfig, CancellationToken token)
        {
            try
            {
                if(report.ReportType == ReportType.Connections)
                {
                    await GenerateConnectionsReport(report, reportTemplate, apiConnection, userConfig, token);
                }
                else if(report.ReportType == ReportType.Statistics)
                {
                    await GenerateStatisticsReport(report, reportTemplate, apiConnection, token);
                }
                else
                {
                    await report.Generate(userConfig.ElementsPerFetch, apiConnection,
                        rep =>
                        {
                            report.ReportData.ManagementData = rep.ManagementData;
                            SetRelevantManagements(ref report.ReportData.ManagementData, reportTemplate.ReportParams.DeviceFilter);
                            return Task.CompletedTask;
                    }, token);
                    if (report.ReportType == ReportType.Recertification)
                    {
                        PrepareMetadata(report.ReportData.ManagementData, userConfig);
                    }
                }
            }
            catch (OperationCanceledException e)
            {
                Log.WriteDebug("Generate Report", $"Cancelled: {e.Message}");
            }
        }

        private static async Task GenerateConnectionsReport(ReportBase report, ReportTemplate reportTemplate, ApiConnection apiConnection, UserConfig userConfig, CancellationToken token)
        {
            ModellingAppRole dummyAppRole = new();
            List<ModellingAppRole> dummyAppRoles = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getDummyAppRole);
            if(dummyAppRoles.Count > 0)
            {
                dummyAppRole = dummyAppRoles.First();
            }
            foreach(var selectedOwner in reportTemplate.ReportParams.ModellingFilter.SelectedOwners)
            {
                OwnerReport actOwnerData = new(dummyAppRole.Id){ Name = selectedOwner.Display(""), Owner = selectedOwner };
                report.ReportData.OwnerData.Add(actOwnerData);
                await report.Generate(userConfig.ElementsPerFetch, apiConnection,
                    rep =>
                    {
                        actOwnerData.Connections = rep.OwnerData.First().Connections;
                        return Task.CompletedTask;
                    }, token);
                await PrepareConnReportData(selectedOwner, actOwnerData, apiConnection, userConfig);
            }
            List<ModellingConnection> comSvcs = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getCommonServices);
            if(comSvcs.Count > 0)
            {
                report.ReportData.GlobalComSvc = [new(){GlobalComSvcs = comSvcs, Name = userConfig.GetText("global_common_services")}];
            }
        }

        private static async Task PrepareConnReportData(FwoOwner selectedOwner, OwnerReport ownerReport, ApiConnection apiConnection, UserConfig userConfig)
        {
            ModellingHandlerBase handlerBase = new(apiConnection, userConfig, new(), false, DefaultInit.DoNothing);
            foreach(var conn in ownerReport.Connections)
            {
                await handlerBase.ExtractUsedInterface(conn);
            }
            ownerReport.Name = selectedOwner.Name;
            ownerReport.RegularConnections = ownerReport.Connections.Where(x => !x.IsInterface && !x.IsCommonService).ToList();
            ownerReport.Interfaces = ownerReport.Connections.Where(x => x.IsInterface).ToList();
            ownerReport.CommonServices = ownerReport.Connections.Where(x => !x.IsInterface && x.IsCommonService).ToList();
        }

        private static async Task GenerateStatisticsReport(ReportBase report, ReportTemplate reportTemplate, ApiConnection apiConnection, CancellationToken token)
        {
            report.ReportData.GlobalStats = new ();
            await report.Generate(0, apiConnection,
                rep =>
                {
                    report.ReportData.ManagementData = rep.ManagementData;
                    SetRelevantManagements(ref report.ReportData.ManagementData, reportTemplate.ReportParams.DeviceFilter);
                    foreach (var mgm in report.ReportData.ManagementData.Where(mgt => !mgt.Ignore))
                    {
                        report.ReportData.GlobalStats.RuleStatistics.ObjectAggregate.ObjectCount += mgm.RuleStatistics.ObjectAggregate.ObjectCount;
                        report.ReportData.GlobalStats.NetworkObjectStatistics.ObjectAggregate.ObjectCount += mgm.NetworkObjectStatistics.ObjectAggregate.ObjectCount;
                        report.ReportData.GlobalStats.ServiceObjectStatistics.ObjectAggregate.ObjectCount += mgm.ServiceObjectStatistics.ObjectAggregate.ObjectCount;
                        report.ReportData.GlobalStats.UserObjectStatistics.ObjectAggregate.ObjectCount += mgm.UserObjectStatistics.ObjectAggregate.ObjectCount;
                    }
                    return Task.CompletedTask;
                }, token);
        }

        private static bool PrepareMetadata(List<ManagementReport> ManagementReports, UserConfig userConfig)
        {
            bool rulesFound = false;
            foreach (var managementReport in ManagementReports)
            {
                foreach (var device in managementReport.Devices)
                {
                    if (device.ContainsRules())
                    {
                        rulesFound = true;
                        foreach (var rulebaseLink in device.RulebaseLinks)
                        {
                            // rule.Metadata.UpdateRecertPeriods(userConfig.RecertificationPeriod, userConfig.RecertificationNoticePeriod);
                        }
                    }
                }
            }
            return rulesFound;
        }

        private static void SetRelevantManagements(ref List<ManagementReport> managementsReport, DeviceFilter deviceFilter)
        {
            if (deviceFilter.IsAnyDeviceFilterSet())
            {
                List<int> relevantManagements = deviceFilter.GetSelectedManagements();
                foreach (var mgm in managementsReport)
                {
                    mgm.Ignore = !relevantManagements.Contains(mgm.Id);
                }
            }
        }
    }
}
