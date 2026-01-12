using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for app data imports (no BackgroundService)
    /// </summary>
    public class ImportAppDataSchedulerService : IAsyncDisposable
    {
        private readonly ISchedulerFactory schedulerFactory;
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;
        private GraphQlApiSubscription<List<ConfigItem>>? configSubscription;
        private IScheduler? scheduler;
        private bool disposed = false;

        private const string JobKeyName = "ImportAppDataJob";
        private const string TriggerKeyName = "ImportAppDataTrigger";
        private const string SchedulerName = "ImportAppDataScheduler";

        /// <summary>
        /// Initializes the import app data scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime">Application lifetime for startup hook.</param>
        public ImportAppDataSchedulerService(ISchedulerFactory schedulerFactory, ApiConnection apiConnection, GlobalConfig globalConfig, IHostApplicationLifetime appLifetime)
        {
            this.schedulerFactory = schedulerFactory;
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;

            // Attach after application started
            appLifetime.ApplicationStarted.Register(OnStarted);
        }

        private async void OnStarted()
        {
            try
            {
                scheduler = await schedulerFactory.GetScheduler();
                configSubscription = apiConnection.GetSubscription<List<ConfigItem>>(ApiExceptionHandler, OnGlobalConfigChange, ConfigQueries.subscribeImportAppDataConfigChanges);
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

            // Ensure durable job exists for manual triggering
            if (!await scheduler.CheckExists(jobKey))
            {
                IJobDetail durableJob = JobBuilder.Create<ImportAppDataJob>()
                    .WithIdentity(jobKey)
                    .StoreDurably()
                    .Build();
                await scheduler.AddJob(durableJob, replace: true);
                Log.WriteInfo(SchedulerName, "Added durable job for manual triggering");
            }

            // Remove existing trigger (if any) but keep the job
            bool unscheduled = await scheduler.UnscheduleJob(triggerKey);
            if (unscheduled)
            {
                Log.WriteInfo(SchedulerName, "Removed existing trigger");
            }

            // Only schedule a trigger if sleep time > 0
            if (globalConfig.ImportAppDataSleepTime <= 0)
            {
                Log.WriteInfo(SchedulerName, "Job disabled (sleep time <= 0) - job kept without trigger for manual runs");
                return;
            }

            DateTimeOffset startTime = CalculateStartTime(globalConfig.ImportAppDataStartAt, TimeSpan.FromHours(globalConfig.ImportAppDataSleepTime));

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .ForJob(jobKey)
                .StartAt(startTime)
                .WithSimpleSchedule(x => x
                    .WithInterval(TimeSpan.FromHours(globalConfig.ImportAppDataSleepTime))
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(trigger);

            Log.WriteInfo(SchedulerName, $"Trigger scheduled. Start: {startTime:yyyy-MM-dd HH:mm:ss}, Interval: {globalConfig.ImportAppDataSleepTime}h");
        }

        private DateTimeOffset CalculateStartTime(DateTime configuredStartTime, TimeSpan interval)
        {
            DateTime startTime = configuredStartTime;
            DateTime now = DateTime.Now;
            while (startTime < now)
            {
                startTime = startTime.Add(interval);
            }
            return new DateTimeOffset(startTime);
        }

        private void ApiExceptionHandler(Exception exception)
        {
            Log.WriteError(SchedulerName, "Config subscription lead to exception. Retry subscription.", exception);
        }

        /// <summary>
        /// Releases resources used by the service.
        /// Disposes the scheduler and configuration subscription.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                try
                {
                    configSubscription?.Dispose();
                    // Scheduler lifecycle is managed by QuartzHostedService
                    disposed = true;
                }
                catch (Exception ex)
                {
                    Log.WriteError(SchedulerName, "Error during disposal", ex);
                }
            }
        }
    }
}
