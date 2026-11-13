using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for the state actions with event "While set" (no BackgroundService).
    /// The job runs in the configured interval of minutes; there is no configurable start time.
    /// </summary>
    public class WhileSetActionSchedulerService : QuartzSchedulerServiceBase<Jobs.WhileSetActionJob>
    {
        private const string JobKeyName = "WhileSetActionJob";
        private const string TriggerKeyName = "WhileSetActionTrigger";
        private const string SchedulerName = "WhileSetActionScheduler";

        /// <summary>
        /// Fixed wall-clock anchor the interval is counted from, as there is no configurable start time.
        /// </summary>
        private static readonly DateTime kIntervalAnchor = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        /// <summary>
        /// Initializes the scheduler service of the "While set" actions.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime">Application lifetime for startup hook.</param>
        public WhileSetActionSchedulerService(
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
                    ConfigQueries.subscribeWhileSetActionConfigChanges))
        { }

        /// <inheritdoc/>
        protected override int SleepTime => globalConfig.WhileSetActionSleepTime;

        /// <inheritdoc/>
        protected override DateTime StartAt => kIntervalAnchor;

        /// <inheritdoc/>
        protected override TimeSpan Interval => TimeSpan.FromMinutes(globalConfig.WhileSetActionSleepTime);

        /// <inheritdoc/>
        protected override string IntervalLogSuffix => "m";
    }
}
