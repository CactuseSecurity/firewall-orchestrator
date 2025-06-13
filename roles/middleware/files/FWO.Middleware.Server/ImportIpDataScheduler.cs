using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using System.Timers;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the scheduler for the import of IP data per area
	/// </summary>
    public class ImportIpDataScheduler : SchedulerBase
    {
        private const string LogMessageTitle = "Import Area IP Data";

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
                    throw new ProcessingFailedException($"{LogMessageTitle} failed for {string.Join(", ", FailedImports)}.");
                }
            }
            catch (Exception exc)
            {
                await LogErrorsWithAlert(2, LogMessageTitle, GlobalConst.kImportAreaSubnetData, AlertCode.ImportAreaSubnetData, exc);
            }
        }
    }
}
