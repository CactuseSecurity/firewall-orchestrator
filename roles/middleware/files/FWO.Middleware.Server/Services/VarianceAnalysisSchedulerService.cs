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
    /// Quartz scheduler service for variance analysis
    /// </summary>
    public class VarianceAnalysisSchedulerService : BackgroundService
    {
        private readonly ISchedulerFactory schedulerFactory;
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;
        private GraphQlApiSubscription<List<ConfigItem>>? configSubscription;
        private IScheduler? scheduler;

        private const string JobKeyName = "VarianceAnalysisJob";
        private const string TriggerKeyName = "VarianceAnalysisTrigger";
        private const string SchedulerName = "VarianceAnalysisScheduler";

        public VarianceAnalysisSchedulerService(ISchedulerFactory schedulerFactory, ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.schedulerFactory = schedulerFactory;
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                scheduler = await schedulerFactory.GetScheduler(stoppingToken);
                await ScheduleJob();

                configSubscription = apiConnection.GetSubscription<List<ConfigItem>>(ApiExceptionHandler, OnGlobalConfigChange, ConfigQueries.subscribeVarianceAnalysisConfigChanges);

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

            if (globalConfig.VarianceAnalysisSleepTime <= 0)
            {
                Log.WriteInfo(SchedulerName, "Job disabled (sleep time <= 0)");
                return;
            }

            var job = JobBuilder.Create<VarianceAnalysisJob>()
                .WithIdentity(jobKey)
                .Build();

            var startTime = CalculateStartTime(globalConfig.VarianceAnalysisStartAt, TimeSpan.FromMinutes(globalConfig.VarianceAnalysisSleepTime));

            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .StartAt(startTime)
                .WithSimpleSchedule(x => x
                    .WithInterval(TimeSpan.FromMinutes(globalConfig.VarianceAnalysisSleepTime))
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(job, trigger);

            Log.WriteInfo(SchedulerName, $"Job scheduled. Start: {startTime:yyyy-MM-dd HH:mm:ss}, Interval: {globalConfig.VarianceAnalysisSleepTime}m");
        }

        private DateTimeOffset CalculateStartTime(DateTime configuredStartTime, TimeSpan interval)
        {
            var startTime = configuredStartTime;
            var now = DateTime.Now;
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

        public override void Dispose()
        {
            configSubscription?.Dispose();
            base.Dispose();
        }
    }
}
