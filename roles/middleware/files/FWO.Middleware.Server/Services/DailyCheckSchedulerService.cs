using FWO.Api.Client;
using FWO.Api.Client.ExceptionHandling;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for daily checks (no BackgroundService)
    /// </summary>
    public class DailyCheckSchedulerService : IAsyncDisposable
    {
        private readonly ISchedulerFactory schedulerFactory;
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;
        private readonly TimeProvider timeProvider;
        private GraphQlApiSubscription<List<ConfigItem>>? configSubscription;
        private IScheduler? scheduler;
        private ScheduleFingerprint? lastAppliedSchedule;
        private bool disposed = false;

        private const string JobKeyName = "DailyCheckJob";
        private const string TriggerKeyName = "DailyCheckTrigger";
        private const string SchedulerName = "DailyCheckScheduler";
        private static readonly TimeSpan kDailyInterval = TimeSpan.FromDays(1);

        /// <summary>
        /// Initializes the daily check scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime">Application lifetime for startup hook.</param>
        /// <param name="timeProvider">Clock used for schedule calculations.</param>
        public DailyCheckSchedulerService(ISchedulerFactory schedulerFactory, ApiConnection apiConnection, GlobalConfig globalConfig, IHostApplicationLifetime appLifetime, TimeProvider? timeProvider = null)
        {
            this.schedulerFactory = schedulerFactory;
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            this.timeProvider = timeProvider ?? TimeProvider.System;

            // Attach after application started
            appLifetime.ApplicationStarted.Register(OnStarted);
        }

        private async void OnStarted()
        {
            try
            {
                scheduler = await schedulerFactory.GetScheduler();
                configSubscription = apiConnection.GetSubscription<List<ConfigItem>>(GraphqlExceptionHandler.Handle, OnGlobalConfigChange, ConfigQueries.subscribeDailyCheckConfigChanges);
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
                ScheduleFingerprint scheduleKey = GetScheduleFingerprint();
                if (scheduleKey == lastAppliedSchedule)
                {
                    Log.WriteDebug(SchedulerName, "Config emission with unchanged schedule - skipping reschedule.");
                    return;
                }

                await ScheduleJob();
                lastAppliedSchedule = scheduleKey;
                Log.WriteInfo(SchedulerName, "Job rescheduled due to config change");
            }
            catch (Exception ex)
            {
                Log.WriteError(SchedulerName, "Failed to reschedule job", ex);
            }
        }

        private ScheduleFingerprint GetScheduleFingerprint()
        {
            return new ScheduleFingerprint(globalConfig.DailyCheckStartAt, kDailyInterval);
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

            if (await scheduler.Exists(jobKey))
            {
                await scheduler.DeleteJob(jobKey);
                Log.WriteInfo(SchedulerName, "Removed existing job");
            }

            IJobDetail job = JobBuilder.Create<DailyCheckJob>()
                .WithIdentity(jobKey)
                .Build();

            DateTimeOffset now = timeProvider.GetUtcNow();
            TimeZoneInfo zone = timeProvider.LocalTimeZone;
            ITrigger trigger = WallClockSchedule.CreateTrigger(triggerKey, jobKey, globalConfig.DailyCheckStartAt, kDailyInterval, now, zone);

            await scheduler.ScheduleJob(job, trigger);

            Log.WriteInfo(SchedulerName, $"Job scheduled. Start: {WallClockSchedule.FormatNextFireTime(trigger, now, zone)}, Interval: 1d");
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

        private readonly record struct ScheduleFingerprint(DateTime StartAt, TimeSpan Interval);
    }
}
