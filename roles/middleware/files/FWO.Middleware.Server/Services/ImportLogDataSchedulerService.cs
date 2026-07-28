using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for log data imports.
    /// </summary>
    public class ImportLogDataSchedulerService : QuartzSchedulerServiceBase<ImportLogDataJob>
    {
        private const string JobKeyName = "ImportLogDataJob";
        private const string TriggerKeyName = "ImportLogDataTrigger";
        private const string SchedulerName = "ImportLogDataScheduler";

        /// <summary>
        /// Initializes the log data import scheduler service.
        /// </summary>
        public ImportLogDataSchedulerService(
            ISchedulerFactory schedulerFactory,
            ApiConnection apiConnection,
            GlobalConfig globalConfig,
            IHostApplicationLifetime appLifetime)
            : base(
                schedulerFactory,
                apiConnection,
                globalConfig,
                appLifetime,
                new QuartzSchedulerOptions(
                    SchedulerName,
                    JobKeyName,
                    TriggerKeyName,
                    ConfigQueries.subscribeImportLogDataConfigChanges))
        { }

        /// <inheritdoc/>
        protected override int SleepTime => globalConfig.ImportLogDataSleepTime;

        /// <inheritdoc/>
        protected override DateTime StartAt => globalConfig.ImportLogDataStartAt;

        /// <inheritdoc/>
        protected override TimeSpan Interval => LogDataImportSchedule.GetInterval(
            globalConfig.ImportLogDataSleepTime,
            globalConfig.ImportLogDataSleepTimeUnit);

        /// <inheritdoc/>
        protected override string IntervalLogSuffix => LogDataImportSchedule.GetIntervalLogSuffix(
            globalConfig.ImportLogDataSleepTimeUnit);
    }
}
