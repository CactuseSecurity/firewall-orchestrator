using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for import change notifications (no BackgroundService)
    /// </summary>
    public class ImportChangeNotifySchedulerService : QuartzSchedulerServiceBase<ImportChangeNotifyJob>
    {
        private const string JobKeyName = "ImportChangeNotifyJob";
        private const string TriggerKeyName = "ImportChangeNotifyTrigger";
        private const string SchedulerName = "ImportChangeNotifyScheduler";

        /// <summary>
        /// Initializes the import change notify scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime">Application lifetime for startup hook.</param>
        public ImportChangeNotifySchedulerService(
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
                    ConfigQueries.subscribeImportNotifyConfigChanges))
        { }

        /// <inheritdoc/>
        protected override int SleepTime => globalConfig.ImpChangeNotifySleepTime;

        /// <inheritdoc/>
        protected override DateTime StartAt => globalConfig.ImpChangeNotifyStartAt;

        /// <inheritdoc/>
        protected override TimeSpan Interval => TimeSpan.FromSeconds(globalConfig.ImpChangeNotifySleepTime);

        /// <inheritdoc/>
        protected override bool IsActive => globalConfig.ImpChangeNotifyActive;

        /// <inheritdoc/>
        protected override string IntervalLogSuffix => "s";

        /// <inheritdoc/>
        protected override string DisabledLogMessage =>
            "Job disabled (inactive or sleep time <= 0) - job kept without trigger for manual runs";
    }
}
