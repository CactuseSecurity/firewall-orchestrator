using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
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
            ScheduleTimer.Stop();
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
            await ImportAppData();
            await AdjustAppServerNames();
        }

        private async Task ImportAppData()
        {
            try
            {
                AppDataImport import = new (apiConnection, globalConfig);
                if(!await import.Run())
                {
                    throw new Exception("Import App Data failed.");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import App Data", $"Ran into exception: ", exc);
                string titletext = "Error encountered while trying to import App Data";
                Log.WriteAlert($"source: \"{GlobalConst.kImportAppData}\"",
                    $"userId: \"0\", title: \"{titletext}\", description: \"{exc}\", alertCode: \"{AlertCode.ImportAppData}\"");
                await AddLogEntry(1, globalConfig.GetText("scheduled_app_import"), globalConfig.GetText("ran_into_exception") + exc.Message, GlobalConst.kImportAppData);
                await SetAlert(globalConfig.GetText("scheduled_app_import"), titletext, GlobalConst.kImportAppData, AlertCode.ImportAppData);
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
                Log.WriteError("Check App Server Names", $"Ran into exception: ", exc);
                string titletext = "Error encountered while trying to adjust App Server Names";
                Log.WriteAlert($"source: \"{GlobalConst.kAdjustAppServerNames}\"",
                    $"userId: \"0\", title: \"{titletext}\", description: \"{exc}\", alertCode: \"{AlertCode.AdjustAppServerNames}\"");
                await AddLogEntry(1, globalConfig.GetText("adjust_app_server_name"), globalConfig.GetText("ran_into_exception") + exc.Message, GlobalConst.kAdjustAppServerNames);
                await SetAlert(globalConfig.GetText("adjust_app_server_name"), titletext, GlobalConst.kAdjustAppServerNames, AlertCode.AdjustAppServerNames);
            }
        }
    }
}
