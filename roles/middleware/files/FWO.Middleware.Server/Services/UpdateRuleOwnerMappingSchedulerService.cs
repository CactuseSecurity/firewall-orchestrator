using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for rule owner mapping (no BackgroundService)
    /// </summary>
    public class UpdateRuleOwnerMappingSchedulerService : QuartzSchedulerServiceBase<UpdateRuleOwnerMappingJob>
    {
        private const string JobKeyName = "UpdateRuleOwnerMappingJob";
        private const string TriggerKeyName = "UpdateRuleOwnerMappingTrigger";
        private const string SchedulerName = "UpdateRuleOwnerMappingScheduler";

        /// <summary>
        /// Initializes the import change notify scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime">Application lifetime for startup hook.</param>
        public UpdateRuleOwnerMappingSchedulerService(
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
                    ConfigQueries.subscribeUpdateRuleOwnerMappingConfigChanges))
        { }

        /// <inheritdoc/>
        protected override int SleepTime => globalConfig.UpdateRuleOwnerMappingSleepTime;

        /// <inheritdoc/>
        protected override DateTime StartAt => globalConfig.UpdateRuleOwnerMappingStartAt;

        /// <inheritdoc/>
        protected override TimeSpan Interval => TimeSpan.FromSeconds(globalConfig.UpdateRuleOwnerMappingSleepTime);

        /// <inheritdoc/>
        protected override bool IsActive => globalConfig.UpdateRuleOwnerMappingActive;

        /// <inheritdoc/>
        protected override string IntervalLogSuffix => "s";

        /// <inheritdoc/>
        protected override string DisabledLogMessage =>
            "Job disabled (inactive or sleep time <= 0) - job kept without trigger for manual runs";
    }
}
