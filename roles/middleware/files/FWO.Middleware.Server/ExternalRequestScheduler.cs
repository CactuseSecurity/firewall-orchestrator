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
	/// Class handling the scheduler for sending external requests
	/// </summary>
    public class ExternalRequestScheduler : SchedulerBase
    {
		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<ExternalRequestScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ExternalRequestScheduler(apiConnection, globalConfig);
        }
    
        private ExternalRequestScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeExternalRequestConfigChanges, SchedulerInterval.Seconds, "ExternalRequest")
        {}

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            ScheduleTimer.Stop();
            globalConfig.SubscriptionUpdateHandler([.. config]);
            if(globalConfig.ExternalRequestSleepTime > 0)
            {
                StartScheduleTimer(globalConfig.ExternalRequestSleepTime, globalConfig.ExternalRequestStartAt);
            }
        }

        /// <summary>
        /// define the processing to be done
        /// </summary>
        protected override async void Process(object? _, ElapsedEventArgs __)
        {
            try
            {
                ExternalRequestSender externalRequestSender = new(apiConnection, globalConfig);
                if(!await externalRequestSender.Run())
                {
                    throw new Exception("External Request failed.");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("External Request", $"Ran into exception: ", exc);
                string titletext = "Error encountered while trying to send External Request";
                Log.WriteAlert($"source: \"{GlobalConst.kExternalRequest}\"",
                    $"userId: \"0\", title: \"{titletext}\", description: \"{exc}\", alertCode: \"{AlertCode.ExternalRequest}\"");
                await AddLogEntry(1, globalConfig.GetText("external_request"), globalConfig.GetText("ran_into_exception") + exc.Message, GlobalConst.kExternalRequest);
                await SetAlert(globalConfig.GetText("external_request"), titletext, GlobalConst.kExternalRequest, AlertCode.ExternalRequest);
            }
        }
    }
}
