using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report;
using FWO.Services;
using System.Timers;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the scheduler for the variance analysis
	/// </summary>
    public class VarianceAnalysisScheduler : SchedulerBase
    {
		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<VarianceAnalysisScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new VarianceAnalysisScheduler(apiConnection, globalConfig);
        }
    
        private VarianceAnalysisScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeVarianceAnalysisConfigChanges, SchedulerInterval.Minutes, "VarianceAnalysis")
        {}

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            ScheduleTimer.Stop();
            globalConfig.SubscriptionUpdateHandler([.. config]);
            if(globalConfig.VarianceAnalysisSleepTime > 0)
            {
                StartScheduleTimer(globalConfig.VarianceAnalysisSleepTime, globalConfig.VarianceAnalysisStartAt);
            }
        }

        /// <summary>
        /// define the processing to be done
        /// </summary>
        protected override async void Process(object? _, ElapsedEventArgs __)
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
                ReportBase? report = await ReportGenerator.Generate(new ReportTemplate("", new(){ ReportType = (int)ReportType.Connections, ModellingFilter = new(){ SelectedOwners = owners}}), apiConnection, userConfig, DefaultInit.DoNothing);
                if(report == null || report.ReportData.OwnerData.Count == 0)
                {
                    Log.WriteInfo("Scheduled Variance Analysis", $"No data found.");
                    return;
                }
                foreach(var owner in report.ReportData.OwnerData)
                {
                    varianceAnalysis = new(apiConnection, extStateHandler, userConfig, owner.Owner, DefaultInit.DoNothing);
                    if(!await varianceAnalysis.AnalyseConnsForStatusAsync(owner.Connections))
                    {
                        Log.WriteError("Scheduled Variance Analysis", $"Variance Analysis failed for owner {owner.Name}.");
                    }
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Variance Analysis", $"Ran into exception: ", exc);
                string titletext = "Error encountered while trying to perform scheduled Variance Analysis";
                Log.WriteAlert($"source: \"{GlobalConst.kVarianceAnalysis}\"",
                    $"userId: \"0\", title: \"{titletext}\", description: \"{exc}\", alertCode: \"{AlertCode.VarianceAnalysis}\"");
                await AddLogEntry(1, globalConfig.GetText("scheduled_var_analysis"), globalConfig.GetText("ran_into_exception") + exc.Message, GlobalConst.kVarianceAnalysis);
                await SetAlert(globalConfig.GetText("scheduled_var_analysis"), titletext, GlobalConst.kVarianceAnalysis, AlertCode.VarianceAnalysis);
            }
        }
    }
}
