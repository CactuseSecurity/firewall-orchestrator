using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Api.Data;
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
        private System.Timers.Timer ScheduleTimer = new();
        private System.Timers.Timer ExternalRequestTimer = new();

		/// <summary>
		/// Async Constructor needing the connection
		/// </summary>
        public static async Task<ExternalRequestScheduler> CreateAsync(ApiConnection apiConnection)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ExternalRequestScheduler(apiConnection, globalConfig);
        }
    
        private ExternalRequestScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig, ConfigQueries.subscribeExternalRequestConfigChanges)
        {}

		/// <summary>
		/// set scheduling timer from config values
		/// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        {
            ScheduleTimer.Stop();
            globalConfig.SubscriptionUpdateHandler(config.ToArray());
            if(globalConfig.ExternalRequestSleepTime > 0)
            {
                ExternalRequestTimer.Interval = globalConfig.ExternalRequestSleepTime * 1000; // convert seconds to milliseconds
                StartScheduleTimer();
            }
        }

		/// <summary>
		/// start the scheduling timer
		/// </summary>
        protected override void StartScheduleTimer()
        {
            if (globalConfig.ExternalRequestSleepTime > 0)
            {
                DateTime startTime = DateTime.Now;
                try
                {
                    startTime = globalConfig.ExternalRequestStartAt;
                    while (startTime < DateTime.Now)
                    {
                        startTime = startTime.AddSeconds(globalConfig.ExternalRequestSleepTime);
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError("External Request scheduler", "Could not calculate start time.", exception);
                }
                TimeSpan interval = startTime - DateTime.Now;

                ScheduleTimer = new();
                ScheduleTimer.Elapsed += SendExternalRequests;
                ScheduleTimer.Elapsed += StartExternalRequestTimer;
                ScheduleTimer.Interval = interval.TotalMilliseconds;
                ScheduleTimer.AutoReset = false;
                ScheduleTimer.Start();
                Log.WriteDebug("External Request scheduler", "ExternalRequestScheduleTimer started.");
            }
        }

        private void StartExternalRequestTimer(object? _, ElapsedEventArgs __)
        {
            ExternalRequestTimer.Stop();
            ExternalRequestTimer = new();
            ExternalRequestTimer.Elapsed += SendExternalRequests;
            ExternalRequestTimer.Interval = globalConfig.ExternalRequestSleepTime * 1000;  // convert seconds to milliseconds
            ExternalRequestTimer.AutoReset = true;
            ExternalRequestTimer.Start();
            Log.WriteDebug("External Request scheduler", "ExternalRequestTimer started.");
        }

        private async void SendExternalRequests(object? _, ElapsedEventArgs __)
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
