using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for app data imports (no BackgroundService)
    /// </summary>
    public class ImportAppDataSchedulerService : QuartzSchedulerServiceBase<ImportAppDataJob>
    {
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
        public ImportAppDataSchedulerService(
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
                    ConfigQueries.subscribeImportAppDataConfigChanges))
        { }

        /// <inheritdoc/>
        protected override int SleepTime => globalConfig.ImportAppDataSleepTime;

        /// <inheritdoc/>
        protected override DateTime StartAt => globalConfig.ImportAppDataStartAt;

        /// <inheritdoc/>
        protected override TimeSpan Interval => TimeSpan.FromHours(globalConfig.ImportAppDataSleepTime);

        /// <inheritdoc/>
        protected override string IntervalLogSuffix => "h";
    }
}
