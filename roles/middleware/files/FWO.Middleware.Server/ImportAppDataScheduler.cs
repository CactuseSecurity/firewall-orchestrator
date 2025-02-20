using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Services;
using FWO.Api.Data;
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
        private System.Timers.Timer ScheduleTimer = new();
        private System.Timers.Timer ImportAppDataTimer = new();

		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<ImportAppDataScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ImportAppDataScheduler(apiConnection, globalConfig);
        }
    
        private ImportAppDataScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeImportAppDataConfigChanges)
        {}

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            ScheduleTimer.Stop();
            globalConfig.SubscriptionUpdateHandler(config.ToArray());
            if(globalConfig.ImportAppDataSleepTime > 0)
            {
                ImportAppDataTimer.Interval = globalConfig.ImportAppDataSleepTime * GlobalConst.kHoursToMilliseconds;
                StartScheduleTimer();
            }
        }

		/// <summary>
		/// start the scheduling timer
		/// </summary>
        protected override void StartScheduleTimer()
        {
            if (globalConfig.ImportAppDataSleepTime > 0)
            {
                DateTime startTime = DateTime.Now;
                try
                {
                    startTime = globalConfig.ImportAppDataStartAt;
                    while (startTime < DateTime.Now)
                    {
                        startTime = startTime.AddHours(globalConfig.ImportAppDataSleepTime);
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError("Import App Data scheduler", "Could not calculate start time.", exception);
                }
                TimeSpan interval = startTime - DateTime.Now;

                ScheduleTimer = new();
                ScheduleTimer.Elapsed += Process;
                ScheduleTimer.Elapsed += StartImportAppDataTimer;
                ScheduleTimer.Interval = interval.TotalMilliseconds;
                ScheduleTimer.AutoReset = false;
                ScheduleTimer.Start();
                Log.WriteDebug("Import App Data scheduler", "ImportAppDataScheduleTimer started.");
            }
        }

        private void StartImportAppDataTimer(object? _, ElapsedEventArgs __)
        {
            ImportAppDataTimer.Stop();
            ImportAppDataTimer = new();
            ImportAppDataTimer.Elapsed += Process;
            ImportAppDataTimer.Interval = globalConfig.ImportAppDataSleepTime * GlobalConst.kHoursToMilliseconds;
            ImportAppDataTimer.AutoReset = true;
            ImportAppDataTimer.Start();
            Log.WriteDebug("Import App Data scheduler", "ImportAppDataTimer started.");
        }

        private async void Process(object? _, ElapsedEventArgs __)
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
            }
        }
    }
}
