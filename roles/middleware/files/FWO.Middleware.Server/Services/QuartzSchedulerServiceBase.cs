using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Shared Quartz scheduler service logic for config-driven jobs.
    /// </summary>
    public abstract class QuartzSchedulerServiceBase<TJob> : IAsyncDisposable where TJob : IJob
    {
        private readonly ISchedulerFactory schedulerFactory;
        private readonly ApiConnection apiConnection;
        /// <summary>
        /// Global configuration for scheduler settings.
        /// </summary>
        protected readonly GlobalConfig globalConfig;
        private readonly QuartzSchedulerOptions options;
        private GraphQlApiSubscription<List<ConfigItem>>? configSubscription;
        private IScheduler? scheduler;
        private bool disposed;

        /// <summary>
        /// Initializes the scheduler service base.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime">Application lifetime for startup hook.</param>
        /// <param name="options">Options for scheduler identifiers and subscription.</param>
        protected QuartzSchedulerServiceBase(
            ISchedulerFactory schedulerFactory,
            ApiConnection apiConnection,
            GlobalConfig globalConfig,
            IHostApplicationLifetime appLifetime,
            QuartzSchedulerOptions options)
        {
            this.schedulerFactory = schedulerFactory;
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            this.options = options;

            // Attach after application started
            appLifetime.ApplicationStarted.Register(OnStarted);
        }

        /// <summary>
        /// Configured sleep time for this scheduler.
        /// </summary>
        protected abstract int SleepTime { get; }

        /// <summary>
        /// Configured start time for this scheduler.
        /// </summary>
        protected abstract DateTime StartAt { get; }

        /// <summary>
        /// Interval for this scheduler based on configuration.
        /// </summary>
        protected abstract TimeSpan Interval { get; }

        /// <summary>
        /// Flag to decide whether the scheduler is active.
        /// </summary>
        protected virtual bool IsActive => true;

        /// <summary>
        /// Log suffix for the interval (e.g., "h", "m", "s").
        /// </summary>
        protected virtual string IntervalLogSuffix => "h";

        /// <summary>
        /// Log message when scheduler is disabled.
        /// </summary>
        protected virtual string DisabledLogMessage =>
            "Job disabled (sleep time <= 0) - job kept without trigger for manual runs";

        private void OnStarted()
        {
            FireAndForget(HandleStartedAsync(), "Startup failed");
        }

        private async Task HandleStartedAsync()
        {
            try
            {
                scheduler = await schedulerFactory.GetScheduler();
                configSubscription = apiConnection.GetSubscription<List<ConfigItem>>(
                    ApiExceptionHandler,
                    OnGlobalConfigChange,
                    options.ConfigSubscriptionQuery);
                Log.WriteInfo(options.SchedulerName, "Listener started");
            }
            catch (Exception ex)
            {
                Log.WriteError(options.SchedulerName, "Startup failed", ex);
            }
        }

        private void OnGlobalConfigChange(List<ConfigItem> config)
        {
            FireAndForget(HandleGlobalConfigChangeAsync(config), "Failed to reschedule job");
        }

        private async Task HandleGlobalConfigChangeAsync(List<ConfigItem> config)
        {
            try
            {
                globalConfig.SubscriptionUpdateHandler([.. config]);
                await ScheduleJob();
                Log.WriteInfo(options.SchedulerName, "Job rescheduled due to config change");
            }
            catch (Exception ex)
            {
                Log.WriteError(options.SchedulerName, "Failed to reschedule job", ex);
            }
        }

        private async Task ScheduleJob()
        {
            if (scheduler == null)
            {
                Log.WriteWarning(options.SchedulerName, "Scheduler not initialized");
                return;
            }

            var jobKey = new JobKey(options.JobKeyName);
            var triggerKey = new TriggerKey(options.TriggerKeyName);

            // Ensure durable job exists for manual triggering
            if (!await scheduler.CheckExists(jobKey))
            {
                IJobDetail durableJob = JobBuilder.Create<TJob>()
                    .WithIdentity(jobKey)
                    .StoreDurably()
                    .Build();
                await scheduler.AddJob(durableJob, replace: true);
                Log.WriteInfo(options.SchedulerName, "Added durable job for manual triggering");
            }

            // Remove existing trigger (if any) but keep the job
            bool unscheduled = await scheduler.UnscheduleJob(triggerKey);
            if (unscheduled)
            {
                Log.WriteInfo(options.SchedulerName, "Removed existing trigger");
            }

            if (!IsActive || SleepTime <= 0)
            {
                Log.WriteInfo(options.SchedulerName, DisabledLogMessage);
                return;
            }

            DateTimeOffset startTime = CalculateStartTime(StartAt, Interval);

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .ForJob(jobKey)
                .StartAt(startTime)
                .WithSimpleSchedule(x => x
                    .WithInterval(Interval)
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(trigger);

            Log.WriteInfo(
                options.SchedulerName,
                $"Trigger scheduled. Start: {startTime:yyyy-MM-dd HH:mm:ss}, Interval: {SleepTime}{IntervalLogSuffix}");
        }

        /// <summary>
        /// Calculates the next start time in the future from a configured start time.
        /// </summary>
        /// <param name="configuredStartTime">Configured start time.</param>
        /// <param name="interval">Schedule interval.</param>
        /// <returns>Next start time in the future.</returns>
        protected static DateTimeOffset CalculateStartTime(DateTime configuredStartTime, TimeSpan interval)
        {
            return CalculateStartTime(configuredStartTime, interval, DateTime.Now);
        }

        /// <summary>
        /// Calculates the next start time in the future relative to a fixed point in time.
        /// </summary>
        /// <param name="configuredStartTime">Configured start time.</param>
        /// <param name="interval">Schedule interval.</param>
        /// <param name="now">Reference time.</param>
        /// <returns>Next start time in the future.</returns>
        protected static DateTimeOffset CalculateStartTime(DateTime configuredStartTime, TimeSpan interval, DateTime now)
        {
            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
            }

            DateTime startTime = configuredStartTime;
            while (startTime < now)
            {
                startTime = startTime.Add(interval);
            }
            return new DateTimeOffset(startTime);
        }

        private void ApiExceptionHandler(Exception exception)
        {
            Log.WriteError(options.SchedulerName, "Config subscription lead to exception. Retry subscription.", exception);
        }

        private void FireAndForget(Task task, string failureMessage)
        {
            _ = task.ContinueWith(
                continuation =>
                {
                    if (continuation.Exception != null)
                    {
                        Log.WriteError(options.SchedulerName, failureMessage, continuation.Exception);
                    }
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Releases resources used by the service.
        /// Disposes the scheduler and configuration subscription.
        /// </summary>
        public virtual ValueTask DisposeAsync()
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
                    Log.WriteError(options.SchedulerName, "Error during disposal", ex);
                }
            }
            return ValueTask.CompletedTask;
        }
    }
}
