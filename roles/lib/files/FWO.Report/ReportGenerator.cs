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
        public static async Task<ReportBase?> Generate(ReportTemplate reportTemplate, ApiConnection apiConnection, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi, CancellationToken? token = null)
        {
            try
            {
                ReportBase report = ReportBase.ConstructReport(reportTemplate, userConfig);
                CancellationToken canToken = token == null ? new () : (CancellationToken)token;
                await DoGeneration(report, reportTemplate, apiConnection, userConfig, displayMessageInUi, canToken);
                return report;
            }
            catch (Exception exception)
            {
                Log.WriteError("Report Generator", $"Generating report leads to exception.", exception);
                return null;
            }
        }

        private static async Task DoGeneration(ReportBase report, ReportTemplate reportTemplate, ApiConnection apiConnection, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi, CancellationToken token)
        {
            try
            {
                if(report.ReportType.IsOwnerRelatedReport())
                {
                    await GenerateOwnerRelatedReport(report, reportTemplate, apiConnection, userConfig, displayMessageInUi, token);
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

        private static async Task GenerateOwnerRelatedReport(ReportBase report, ReportTemplate reportTemplate, ApiConnection apiConnection, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi, CancellationToken token)
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
                await PrepareConnReportData(selectedOwner, actOwnerData, report.ReportType, reportTemplate.ReportParams.ModellingFilter, apiConnection, userConfig, displayMessageInUi);
            }
            if(report.ReportType == ReportType.Connections)
            {
                List<ModellingConnection> comSvcs = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getCommonServices);
                if(comSvcs.Count > 0)
                {
                    report.ReportData.GlobalComSvc = [new(){GlobalComSvcs = comSvcs, Name = userConfig.GetText("global_common_services")}];
                }
            }
        }

        private static async Task PrepareConnReportData(FwoOwner selectedOwner, OwnerReport ownerReport, ReportType reportType, ModellingFilter modellingFilter,
            ApiConnection apiConnection, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            ModellingHandlerBase handlerBase = new(apiConnection, userConfig, new(), false, displayMessageInUi);
            foreach(var conn in ownerReport.Connections)
            {
                await handlerBase.ExtractUsedInterface(conn);
            }
            if(reportType == ReportType.VarianceAnalysis)
            {
                await PrepareVarianceData(ownerReport, modellingFilter, apiConnection, userConfig, displayMessageInUi);
            }
            ownerReport.Name = selectedOwner.Name;
            ownerReport.RegularConnections = [.. ownerReport.Connections.Where(x => !x.IsInterface && !x.IsCommonService)];
            ownerReport.Interfaces = [.. ownerReport.Connections.Where(x => x.IsInterface)];
            ownerReport.CommonServices = [.. ownerReport.Connections.Where(x => !x.IsInterface && x.IsCommonService)];
        }

        private static async Task PrepareVarianceData(OwnerReport ownerReport, ModellingFilter modellingFilter, ApiConnection apiConnection,
            UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            ownerReport.ExtractConnectionsToAnalyse();
            ExtStateHandler extStateHandler = new(apiConnection);
            ModellingVarianceAnalysis varianceAnalysis = new(apiConnection, extStateHandler, userConfig, ownerReport.Owner, displayMessageInUi);
            ModellingVarianceResult result = await varianceAnalysis.AnalyseRulesVsModelledConnections([.. ownerReport.Connections.Where(x => !x.IsDocumentationOnly())], modellingFilter);
            ownerReport.Connections = result.ConnsNotImplemented;
            ownerReport.RuleDifferences = result.RuleDifferences;
            ownerReport.MissingAppRoles = result.MissingAppRoles;
            ownerReport.DifferingAppRoles = result.DifferingAppRoles;
            ownerReport.AppRoleStats = result.AppRoleStats;
            if(modellingFilter.AnalyseRemainingRules)
            {
                ownerReport.ManagementData = result.MgtDataToReport();
                ownerReport.ManagementData = await ReportAppRules.PrepareAppRulesReport(ownerReport.ManagementData, modellingFilter, apiConnection, ownerReport.Owner.Id);
            }
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
