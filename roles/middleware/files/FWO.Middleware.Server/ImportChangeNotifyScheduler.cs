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
	/// Class handling the scheduler for the import change notifications
	/// </summary>
    public class ImportChangeNotifyScheduler : SchedulerBase
    {
		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<ImportChangeNotifyScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ImportChangeNotifyScheduler(apiConnection, globalConfig);
        }
    
        private ImportChangeNotifyScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeImportNotifyConfigChanges, SchedulerInterval.Seconds, "ImportChangeNotify")
        {}

        /// <summary>
        /// set scheduling timer from config values
        /// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            ScheduleTimer.Stop();
            globalConfig.SubscriptionUpdateHandler([.. config]);
            if(globalConfig.ImpChangeNotifyActive && globalConfig.ImpChangeNotifySleepTime > 0)
            {
                StartScheduleTimer(globalConfig.ImpChangeNotifySleepTime, globalConfig.ImpChangeNotifyStartAt);
            }
        }

        /// <summary>
        /// define the processing to be done
        /// </summary>
        protected override async void Process(object? _, ElapsedEventArgs __)
        {
            try
            {
                ImportChangeNotifier notifyImportChanges = new(apiConnection, globalConfig);
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
                await AddLogEntry(1, globalConfig.GetText("imp_change_notification"), globalConfig.GetText("ran_into_exception") + exc.Message, GlobalConst.kImportChangeNotify);
                await SetAlert(globalConfig.GetText("imp_change_notification"), titletext, GlobalConst.kImportChangeNotify, AlertCode.ImportChangeNotify);
            }
        }
    }
}
