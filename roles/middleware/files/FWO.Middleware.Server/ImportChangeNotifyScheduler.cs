using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Timers;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the scheduler for the import change notifications
	/// </summary>
    public class ImportChangeNotifyScheduler : SchedulerBase
    {
        private System.Timers.Timer ScheduleTimer = new();
        private System.Timers.Timer ImportChangeNotifyTimer = new();

		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<ImportChangeNotifyScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ImportChangeNotifyScheduler(apiConnection, globalConfig);
        }
    
        private ImportChangeNotifyScheduler(ApiConnection apiConnection, GlobalConfig globalConfig) : base(apiConnection, globalConfig)
        {}

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void GlobalConfig_OnChange(Config.Api.Config globalConfig, ConfigItem[] _)
        {
            ScheduleTimer.Stop();
            if(globalConfig.ImpChangeNotifySleepTime > 0)
            {
                ImportChangeNotifyTimer.Interval = globalConfig.ImpChangeNotifySleepTime * 1000; // convert seconds to milliseconds
                StartScheduleTimer();
            }
        }

		/// <summary>
		/// start the scheduling timer
		/// </summary>
        protected override void StartScheduleTimer()
        {
            if (globalConfig.ImpChangeNotifySleepTime > 0)
            {
                DateTime startTime = DateTime.Now;
                try
                {
                    startTime = globalConfig.ImpChangeNotifyStartAt;
                    while (startTime < DateTime.Now)
                    {
                        startTime = startTime.AddSeconds(globalConfig.ImpChangeNotifySleepTime);
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError("Import Change Notify scheduler", "Could not calculate start time.", exception);
                }
                TimeSpan interval = startTime - DateTime.Now;

                ScheduleTimer = new();
                ScheduleTimer.Elapsed += ImportChangeNotify;
                ScheduleTimer.Elapsed += StartImportChangeNotifyTimer;
                ScheduleTimer.Interval = interval.TotalMilliseconds;
                ScheduleTimer.AutoReset = false;
                ScheduleTimer.Start();
                Log.WriteDebug("Import Change Notify scheduler", "ImportChangeNotify ScheduleTimer started.");
            }
        }

        private void StartImportChangeNotifyTimer(object? _, ElapsedEventArgs __)
        {
            ImportChangeNotifyTimer.Stop();
            ImportChangeNotifyTimer = new();
            ImportChangeNotifyTimer.Elapsed += ImportChangeNotify;
            ImportChangeNotifyTimer.Interval = globalConfig.ImpChangeNotifySleepTime * 1000;  // convert seconds to milliseconds
            ImportChangeNotifyTimer.AutoReset = true;
            ImportChangeNotifyTimer.Start();
            Log.WriteDebug("Import Change Notify scheduler", "ImportChangeNotifyTimer started.");
        }

        private async void ImportChangeNotify(object? _, ElapsedEventArgs __)
        {
            try
            {
                ImportChangeNotifier notifyImportChanges = new ImportChangeNotifier(apiConnection, globalConfig);
                if(!await notifyImportChanges.Run())
                {
                    throw new Exception("Import Change Notify failed.");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Change Notify", $"Ran into exception: ", exc);
                string titletext = "Error encountered while trying to Notify import Change";
                Log.WriteAlert($"source: \"{GlobalConst.kImportChangeNotify}\"",
                    $"userId: \"0\", title: \"{titletext}\", description: \"{exc}\", alertCode: \"{AlertCode.ImportChangeNotify}\"");
                await AddLogEntry(1, globalConfig.GetText("scheduled_app_import"), globalConfig.GetText("ran_into_exception") + exc.Message, GlobalConst.kImportChangeNotify);
                await SetAlert(globalConfig.GetText("scheduled_app_import"), titletext, GlobalConst.kImportChangeNotify, AlertCode.ImportChangeNotify);
            }
        }
    }
}
