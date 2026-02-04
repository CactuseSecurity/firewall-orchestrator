using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for autodiscovery (no BackgroundService)
    /// </summary>
    public class AutoDiscoverSchedulerService : QuartzSchedulerServiceBase<AutoDiscoverJob>
    {
        private const string JobKeyName = "AutoDiscoverJob";
        private const string TriggerKeyName = "AutoDiscoverTrigger";
        private const string SchedulerName = "AutoDiscoverScheduler";

        /// <summary>
        /// Initializes the autodiscovery scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime">Application lifetime for startup hook.</param>
        public AutoDiscoverSchedulerService(
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
                    ConfigQueries.subscribeAutodiscoveryConfigChanges))
        { }

        /// <inheritdoc/>
        protected override int SleepTime => globalConfig.AutoDiscoverSleepTime;

        /// <inheritdoc/>
        protected override DateTime StartAt => globalConfig.AutoDiscoverStartAt;

        /// <inheritdoc/>
        protected override TimeSpan Interval => TimeSpan.FromHours(globalConfig.AutoDiscoverSleepTime);

        /// <inheritdoc/>
        protected override string IntervalLogSuffix => "h";
    }
}
