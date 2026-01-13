using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report;
using FWO.Services;
using Quartz;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Quartz Job for variance analysis
    /// </summary>
    public class VarianceAnalysisJob : IJob
    {
        private const string LogMessageTitle = "Scheduled Variance Analysis";
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;

        /// <summary>
        /// Creates a new variance analysis job.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        public VarianceAnalysisJob(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <inheritdoc />
        public async Task Execute(IJobExecutionContext context)
        {
            await VarianceAnalysis();
        }

        private async Task VarianceAnalysis()
        {
            try
            {
                ExtStateHandler extStateHandler = new(apiConnection);
                ModellingVarianceAnalysis? varianceAnalysis = null;
                UserConfig userConfig = new(globalConfig)
                {
                    RuleRecognitionOption = globalConfig.RuleRecognitionOption,
                    ModNamingConvention = globalConfig.ModNamingConvention,
                    ModModelledMarkerLocation = globalConfig.ModModelledMarkerLocation,
                    ModModelledMarker = globalConfig.ModModelledMarker
                };

                List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
                ReportBase? report = await ReportGenerator.GenerateFromTemplate(new ReportTemplate("", new() { ReportType = (int)ReportType.Connections, ModellingFilter = new() { SelectedOwners = owners } }), apiConnection, userConfig, DefaultInit.DoNothing);
                if (report == null || report.ReportData.OwnerData.Count == 0)
                {
                    Log.WriteInfo(LogMessageTitle, "No data found.");
                    return;
                }
                foreach (OwnerConnectionReport owner in report.ReportData.OwnerData)
                {
                    varianceAnalysis = new(apiConnection, extStateHandler, userConfig, owner.Owner, DefaultInit.DoNothing);
                    if (!await varianceAnalysis.AnalyseConnsForStatusAsync(owner.Connections))
                    {
                        Log.WriteError(LogMessageTitle, $"Variance Analysis failed for owner {owner.Name}.");
                    }
                }
            }
            catch (Exception exc)
            {
                await SchedulerJobHelper.LogErrorsWithAlert(apiConnection, globalConfig, 1, LogMessageTitle, GlobalConst.kVarianceAnalysis, AlertCode.VarianceAnalysis, exc);
            }
        }
    }
}
