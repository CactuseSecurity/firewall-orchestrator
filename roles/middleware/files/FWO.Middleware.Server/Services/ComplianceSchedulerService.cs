using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for compliance check (no BackgroundService)
    /// </summary>
    public class ComplianceSchedulerService : QuartzSchedulerServiceBase<ComplianceJob>
    {
        private const string JobKeyName = "ComplianceJob";
        private const string TriggerKeyName = "ComplianceTrigger";
        private const string SchedulerName = "ComplianceScheduler";

        /// <summary>
        /// Initializes the compliance scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime">Application lifetime for startup hook.</param>
        public ComplianceSchedulerService(
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
                    ConfigQueries.subscribeComplianceCheckConfigChanges))
        { }

        /// <inheritdoc/>
        protected override int SleepTime => globalConfig.ComplianceCheckSleepTime;

        /// <inheritdoc/>
        protected override DateTime StartAt => globalConfig.ComplianceCheckStartAt;

        /// <inheritdoc/>
        protected override TimeSpan Interval => TimeSpan.FromMinutes(globalConfig.ComplianceCheckSleepTime);

        /// <inheritdoc/>
        protected override string IntervalLogSuffix => " m";
    }
}
