using FWO.Api.Client;
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
    
        private ImportAppDataScheduler(ApiConnection apiConnection, GlobalConfig globalConfig) : base(apiConnection, globalConfig)
        {
            globalConfig.OnChange += GlobalConfig_OnChange;
            StartScheduleTimer();
        }

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void GlobalConfig_OnChange(Config.Api.Config globalConfig, ConfigItem[] _)
        {
            ScheduleTimer.Stop();
            if(globalConfig.ImportAppDataSleepTime > 0)
            {
                ImportAppDataTimer.Interval = globalConfig.ImportAppDataSleepTime * 3600000; // convert hours to milliseconds
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
                ScheduleTimer.Elapsed += ImportAppData;
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
            ImportAppDataTimer.Elapsed += ImportAppData;
            ImportAppDataTimer.Interval = globalConfig.ImportAppDataSleepTime * 3600000;  // convert hours to milliseconds
            ImportAppDataTimer.AutoReset = true;
            ImportAppDataTimer.Start();
            Log.WriteDebug("Import App Data scheduler", "ImportAppDataTimer started.");
        }

        private async void ImportAppData(object? _, ElapsedEventArgs __)
        {
            try
            {
                AppDataImport import = new AppDataImport(apiConnection, globalConfig);
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
    }
}
