using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for External Requests (no BackgroundService)
    /// </summary>
    public class ExternalRequestSchedulerService : IAsyncDisposable
    {
        private readonly ISchedulerFactory schedulerFactory;
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;
        private readonly IHostApplicationLifetime appLifetime;
        private GraphQlApiSubscription<List<ConfigItem>>? configSubscription;
        private IScheduler? scheduler;
        
        private const string JobKeyName = "ExternalRequestJob";
        private const string TriggerKeyName = "ExternalRequestTrigger";
        private const string SchedulerName = "ExternalRequestScheduler";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <summary>
        /// Initializes the external request scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime"></param>
        public ExternalRequestSchedulerService(
            ISchedulerFactory schedulerFactory,
            ApiConnection apiConnection,
            GlobalConfig globalConfig,
            IHostApplicationLifetime appLifetime)
        {
            this.schedulerFactory = schedulerFactory;
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            this.appLifetime = appLifetime;

            // Attach after application started
            appLifetime.ApplicationStarted.Register(OnStarted);
        }

        private async void OnStarted()
        {
            try
            {
                scheduler = await schedulerFactory.GetScheduler();
                // Config change subscription
                configSubscription = apiConnection.GetSubscription<List<ConfigItem>>(
                    ApiExceptionHandler,
                    OnGlobalConfigChange,
                    ConfigQueries.subscribeExternalRequestConfigChanges);
                Log.WriteInfo(SchedulerName, "Listener started");
            }
            catch (Exception ex)
            {
                Log.WriteError(SchedulerName, "Startup failed", ex);
            }
        }

        private async void OnGlobalConfigChange(List<ConfigItem> config)
        {
            try
            {
                globalConfig.SubscriptionUpdateHandler([.. config]);
                await ScheduleJob();
                Log.WriteInfo(SchedulerName, "Job rescheduled due to config change");
            }
            catch (Exception ex)
            {
                Log.WriteError(SchedulerName, "Failed to reschedule job", ex);
            }
        }

        private async Task ScheduleJob()
        {
            if (scheduler == null)
            {
                Log.WriteWarning(SchedulerName, "Scheduler not initialized");
                return;
            }

            var jobKey = new JobKey(JobKeyName);
            var triggerKey = new TriggerKey(TriggerKeyName);

            // Remove existing job if present
            if (await scheduler.CheckExists(jobKey))
            {
                await scheduler.DeleteJob(jobKey);
                Log.WriteInfo(SchedulerName, "Removed existing job");
            }

            // Only schedule if sleep time > 0
            if (globalConfig.ExternalRequestSleepTime <= 0)
            {
                Log.WriteInfo(SchedulerName, "Job disabled (sleep time <= 0)");
                return;
            }

            // Create job
            IJobDetail job = JobBuilder.Create<Jobs.ExternalRequestJob>()
                .WithIdentity(jobKey)
                .Build();

            // Calculate start time
            DateTimeOffset startTime = CalculateStartTime(
                globalConfig.ExternalRequestStartAt,
                globalConfig.ExternalRequestSleepTime);

            // Create trigger with recurring schedule
            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .StartAt(startTime)
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(globalConfig.ExternalRequestSleepTime)
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(job, trigger);
            
            Log.WriteInfo(SchedulerName, 
                $"Job scheduled. Start: {startTime:yyyy-MM-dd HH:mm:ss}, Interval: {globalConfig.ExternalRequestSleepTime}s");
        }

        private static DateTimeOffset CalculateStartTime(DateTime configuredStartTime, int intervalSeconds)
        {
            DateTime startTime = configuredStartTime;
            DateTime now = DateTime.Now;

            // Move start time forward until it's in the future
            while (startTime < now)
            {
                startTime = startTime.AddSeconds(intervalSeconds);
            }

            return new DateTimeOffset(startTime);
        }

        private void ApiExceptionHandler(Exception exception)
        {
            Log.WriteError(SchedulerName, 
                "Config subscription lead to exception. Retry subscription.", exception);
        }

        /// <summary>
        /// Releases resources used by the listener.
        /// Disposes the configuration subscription.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            configSubscription?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
