using FWO.Logging;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config/state listener for report generation
    /// </summary>
    public class ReportSchedulerService
    {
        private readonly ISchedulerFactory schedulerFactory;
        private IScheduler? scheduler;
        private const string JobKeyName = "ReportJob";
        private const string TriggerKeyName = "ReportTrigger";
        private const string SchedulerName = "ReportScheduler";

        /// <summary>
        /// Initializes the report scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="appLifetime"></param>
        public ReportSchedulerService(ISchedulerFactory schedulerFactory, IHostApplicationLifetime appLifetime)
        {
            this.schedulerFactory = schedulerFactory;

            // Attach after application started
            appLifetime.ApplicationStarted.Register(OnStarted);
        }

        private async void OnStarted()
        {
            try
            {
                scheduler = await schedulerFactory.GetScheduler();
                await ScheduleJob();

                Log.WriteInfo(SchedulerName, "Listener started");
            }
            catch (Exception ex)
            {
                Log.WriteError(SchedulerName, "Startup failed", ex);
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

            // Ensure the job exists as a durable job (so it can be manually triggered)
            if (!await scheduler.CheckExists(jobKey))
            {
                IJobDetail durableJob = JobBuilder.Create<ReportJob>()
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

            int interval = 60;

            // Create trigger with recurring schedule for existing job
            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .ForJob(jobKey)
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(interval)
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(trigger);

            Log.WriteInfo(SchedulerName,
                $"Trigger scheduled, Interval: {interval}s");
        }
    }
}
