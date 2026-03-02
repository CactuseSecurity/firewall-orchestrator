using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for External Requests (no BackgroundService)
    /// </summary>
    public class ExternalRequestSchedulerService : QuartzSchedulerServiceBase<Jobs.ExternalRequestJob>
    {
        private const string JobKeyName = "ExternalRequestJob";
        private const string TriggerKeyName = "ExternalRequestTrigger";
        private const string SchedulerName = "ExternalRequestScheduler";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <summary>
        /// Initializes the external request scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime"></param>
        public ExternalRequestSchedulerService(
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
                    ConfigQueries.subscribeExternalRequestConfigChanges))
        { }

        /// <inheritdoc/>
        protected override int SleepTime => globalConfig.ExternalRequestSleepTime;

        /// <inheritdoc/>
        protected override DateTime StartAt => globalConfig.ExternalRequestStartAt;

        /// <inheritdoc/>
        protected override TimeSpan Interval => TimeSpan.FromSeconds(globalConfig.ExternalRequestSleepTime);

        /// <inheritdoc/>
        protected override string IntervalLogSuffix => "s";
    }
}
