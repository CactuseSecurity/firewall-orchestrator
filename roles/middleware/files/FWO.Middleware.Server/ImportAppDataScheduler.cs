using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Services;
using FWO.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Timers;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the scheduler for the import of app data
	/// </summary>
    public class ImportAppDataScheduler : SchedulerBase
    {
        private const string LogMessageTitleImport = "Import App Data";
        private const string LogMessageTitleAdjust = "Adjust App Server Names";

		/// <summary>
        /// Async Constructor needing the connection
        /// </summary>
        public static async Task<ImportAppDataScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ImportAppDataScheduler(apiConnection, globalConfig);
        }
    
        private ImportAppDataScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeImportAppDataConfigChanges, SchedulerInterval.Hours, "ImportAppData")
        {}

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            StopAllTimers();
            globalConfig.SubscriptionUpdateHandler([.. config]);
            if(globalConfig.ImportAppDataSleepTime > 0)
            {
                StartScheduleTimer(globalConfig.ImportAppDataSleepTime, globalConfig.ImportAppDataStartAt);
            }
        }

        /// <summary>
        /// define the processing to be done
        /// </summary>
        protected override async void Process(object? _, ElapsedEventArgs __)
        {
            Log.WriteDebug(LogMessageTitleImport, "Process started");
            await ImportAppData();
            await AdjustAppServerNames();
        }

        private async Task ImportAppData()
        {
            try
            {
                AppDataImport import = new (apiConnection, globalConfig);
                List<string> FailedImports = await import.Run();
                if (FailedImports.Count > 0)
                {
                    throw new ProcessingFailedException($"{LogMessageTitleImport} failed for {string.Join(", ", FailedImports)}.");
                }
            }
            catch (Exception exc)
            {
                await LogErrorsWithAlert(2, LogMessageTitleImport, GlobalConst.kImportAppData, AlertCode.ImportAppData, exc);
            }
        }

        private async Task AdjustAppServerNames()
        {
            try
            {
                if(globalConfig.DnsLookup)
                {
                    UserConfig userConfig = new (globalConfig, apiConnection, new(){ Language = GlobalConst.kEnglish });
                    userConfig.User.Name = Roles.MiddlewareServer;
                    await AppServerHelper.AdjustAppServerNames(apiConnection, userConfig);
                }
            }
            catch (Exception exc)
            {
                await LogErrorsWithAlert(1, LogMessageTitleAdjust, GlobalConst.kAdjustAppServerNames, AlertCode.AdjustAppServerNames, exc);
            }
        }
    }
}
