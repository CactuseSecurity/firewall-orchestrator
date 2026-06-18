using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Services;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for flow sync (no BackgroundService)
    /// </summary>
    public class UpdateFlowsSchedulerService : QuartzSchedulerServiceBase<UpdateFlowsJob>
    {
        private const string JobKeyName = "UpdateFlowsJob";
        private const string TriggerKeyName = "UpdateFlowsTrigger";
        private const string SchedulerName = "UpdateFlowsScheduler";

        /// <summary>
        /// Creates a new flow sync scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime">Application lifetime handle.</param>
        public UpdateFlowsSchedulerService(
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
                    ConfigQueries.subscribeFlowSyncConfigChanges))
        { }

        /// <inheritdoc />
        protected override int SleepTime => globalConfig.FlowSyncSleepTime;

        /// <inheritdoc />
        protected override DateTime StartAt => DateTime.Now.AddSeconds(1);

        /// <inheritdoc />
        protected override DateTime? StartAtScheduleKey => null;

        /// <inheritdoc />
        protected override TimeSpan Interval => TimeSpan.FromSeconds(globalConfig.FlowSyncSleepTime);

        /// <inheritdoc />
        protected override string IntervalLogSuffix => "s";

        /// <inheritdoc />
        protected override string DisabledLogMessage =>
            "Job disabled (sleep time <= 0) - job kept without trigger for manual runs";
    }
}
