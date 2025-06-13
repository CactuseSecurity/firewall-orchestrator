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
                List<string> FailedRequests = await externalRequestSender.Run();
                if (FailedRequests.Count > 0)
                {
                    throw new ProcessingFailedException($"{FailedRequests.Count} External Request(s) failed: {string.Join(". ", FailedRequests)}.");
                }
            }
            catch (Exception exc)
            {
                await LogErrorsWithAlert(1, "External Request", GlobalConst.kExternalRequest, AlertCode.ExternalRequest, exc);
            }
        }
    }
}
