using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for area IP data imports (no BackgroundService)
    /// </summary>
    public class ImportIpDataSchedulerService : QuartzSchedulerServiceBase<ImportIpDataJob>
    {
        private const string JobKeyName = "ImportIpDataJob";
        private const string TriggerKeyName = "ImportIpDataTrigger";
        private const string SchedulerName = "ImportIpDataScheduler";

        /// <summary>
        /// Initializes the import IP data scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime">Application lifetime for startup hook.</param>
        public ImportIpDataSchedulerService(
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
                    ConfigQueries.subscribeImportIpDataConfigChanges))
        { }

        /// <inheritdoc/>
        protected override int SleepTime => globalConfig.ImportSubnetDataSleepTime;

        /// <inheritdoc/>
        protected override DateTime StartAt => globalConfig.ImportSubnetDataStartAt;

        /// <inheritdoc/>
        protected override TimeSpan Interval => TimeSpan.FromHours(globalConfig.ImportSubnetDataSleepTime);

        /// <inheritdoc/>
        protected override string IntervalLogSuffix => "h";
    }
}
