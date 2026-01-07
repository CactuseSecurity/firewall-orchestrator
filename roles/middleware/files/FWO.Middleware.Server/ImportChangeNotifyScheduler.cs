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
            StopAllTimers();
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
            Log.WriteDebug("Import Change Notify", "Process started");
            try
            {
                ImportChangeNotifier notifyImportChanges = new(apiConnection, globalConfig);
                await notifyImportChanges.Run();
            }
            catch (Exception exc)
            {
                await LogErrorsWithAlert(1, "Import Change Notify", GlobalConst.kImportChangeNotify, AlertCode.ImportChangeNotify, exc);
            }
        }
    }
}
