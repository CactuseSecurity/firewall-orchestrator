using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Middleware.Server.Jobs;
using Quartz;
using FwoSchedulerOptions = FWO.Middleware.Server.Services.QuartzSchedulerOptions;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for rule hit import requests.
    /// </summary>
    public class ImportRuleHitsSchedulerService : QuartzSchedulerServiceBase<ImportRuleHitsJob>
    {
        private const string JobKeyName = "ImportRuleHitsJob";
        private const string TriggerKeyName = "ImportRuleHitsTrigger";
        private const string SchedulerName = "ImportRuleHitsScheduler";

        /// <summary>
        /// Initializes the rule hit import scheduler service.
        /// </summary>
        public ImportRuleHitsSchedulerService(
            ISchedulerFactory schedulerFactory,
            ApiConnection apiConnection,
            GlobalConfig globalConfig,
            IHostApplicationLifetime appLifetime)
            : base(
                schedulerFactory,
                apiConnection,
                globalConfig,
                appLifetime,
                new FwoSchedulerOptions(
                    SchedulerName,
                    JobKeyName,
                    TriggerKeyName,
                    ConfigQueries.subscribeImportRuleHitsConfigChanges))
        { }

        /// <inheritdoc/>
        protected override int SleepTime => globalConfig.ImportRuleHitsSleepTime;

        /// <inheritdoc/>
        protected override DateTime StartAt => globalConfig.ImportRuleHitsStartAt;

        /// <inheritdoc/>
        protected override TimeSpan Interval => RuleHitImportSchedule.GetInterval(
            globalConfig.ImportRuleHitsSleepTime,
            globalConfig.ImportRuleHitsSleepTimeUnit);

        /// <inheritdoc/>
        protected override string IntervalLogSuffix => RuleHitImportSchedule.GetIntervalLogSuffix(
            globalConfig.ImportRuleHitsSleepTimeUnit);
    }
}
