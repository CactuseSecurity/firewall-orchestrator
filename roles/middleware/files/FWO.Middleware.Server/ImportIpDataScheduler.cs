using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Timers;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the scheduler for the import of IP data per area
	/// </summary>
    public class ImportIpDataScheduler : SchedulerBase
    {
		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<ImportIpDataScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ImportIpDataScheduler(apiConnection, globalConfig);
        }
    
        private ImportIpDataScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeImportIpDataConfigChanges, SchedulerInterval.Hours, "ImportAreaIPData")
        {}

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            ScheduleTimer.Stop();
            globalConfig.SubscriptionUpdateHandler([.. config]);
            if (globalConfig.ImportSubnetDataSleepTime > 0)
            {
                StartScheduleTimer(globalConfig.ImportSubnetDataSleepTime, globalConfig.ImportSubnetDataStartAt);
            }
        }

        /// <summary>
        /// define the processing to be done
        /// </summary>
        protected override async void Process(object? _, ElapsedEventArgs __)
        {
            try
            {
                AreaIpDataImport import = new (apiConnection, globalConfig);
                List<string> FailedImports = await import.Run();
                if (FailedImports.Count > 0)
                {
                    throw new ProcessingFailedException($"Import Area IP Data failed for {string.Join(", ", FailedImports)}.");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Area IP Data", $"Ran into exception: ", exc);
                Log.WriteAlert($"source: \"{GlobalConst.kImportAreaSubnetData}\"",
                    $"userId: \"0\", title: \"Error encountered while trying to import Area IP Data\", description: \"{exc}\", alertCode: \"{AlertCode.ImportAreaSubnetData}\"");
                await AddLogEntry(1, globalConfig.GetText("scheduled_subnet_import"), globalConfig.GetText("ran_into_exception") + exc.Message, GlobalConst.kImportAreaSubnetData);
                await SetAlert("Import Area IP Data failed", exc.Message, GlobalConst.kImportAreaSubnetData, AlertCode.ImportAreaSubnetData);
            }
        }
    }
}
