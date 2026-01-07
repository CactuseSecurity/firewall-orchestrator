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
    /// Quartz scheduler service for import change notifications
    /// </summary>
    public class ImportChangeNotifySchedulerService : BackgroundService, IAsyncDisposable
    {
        private readonly ISchedulerFactory schedulerFactory;
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;
        private GraphQlApiSubscription<List<ConfigItem>>? configSubscription;
        private IScheduler? scheduler;
        private bool disposed = false;

        private const string JobKeyName = "ImportChangeNotifyJob";
        private const string TriggerKeyName = "ImportChangeNotifyTrigger";
        private const string SchedulerName = "ImportChangeNotifyScheduler";

        /// <summary>
        /// Initializes the import change notify scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        public ImportChangeNotifySchedulerService(ISchedulerFactory schedulerFactory, ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.schedulerFactory = schedulerFactory;
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                scheduler = await schedulerFactory.GetScheduler(stoppingToken);
                await ScheduleJob();

                configSubscription = apiConnection.GetSubscription<List<ConfigItem>>(ApiExceptionHandler, OnGlobalConfigChange, ConfigQueries.subscribeImportNotifyConfigChanges);

                Log.WriteInfo(SchedulerName, "Service started");

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                Log.WriteInfo(SchedulerName, "Service stopping");
            }
            catch (Exception ex)
            {
                Log.WriteError(SchedulerName, "Service failed", ex);
                throw;
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

            if (await scheduler.CheckExists(jobKey))
            {
                await scheduler.DeleteJob(jobKey);
                Log.WriteInfo(SchedulerName, "Removed existing job");
            }

            if (!globalConfig.ImpChangeNotifyActive || globalConfig.ImpChangeNotifySleepTime <= 0)
            {
                Log.WriteInfo(SchedulerName, "Job disabled (inactive or sleep time <= 0)");
                return;
            }

            IJobDetail job = JobBuilder.Create<ImportChangeNotifyJob>()
                .WithIdentity(jobKey)
                .Build();

            DateTimeOffset startTime = CalculateStartTime(globalConfig.ImpChangeNotifyStartAt, TimeSpan.FromSeconds(globalConfig.ImpChangeNotifySleepTime));

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .StartAt(startTime)
                .WithSimpleSchedule(x => x
                    .WithInterval(TimeSpan.FromSeconds(globalConfig.ImpChangeNotifySleepTime))
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(job, trigger);

            Log.WriteInfo(SchedulerName, $"Job scheduled. Start: {startTime:yyyy-MM-dd HH:mm:ss}, Interval: {globalConfig.ImpChangeNotifySleepTime}s");
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
                    
                    if (scheduler != null)
                    {
                        await scheduler.Shutdown(waitForJobsToComplete: true);
                    }
                    
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
