using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Config listener and rescheduler for variance analysis (no BackgroundService)
    /// </summary>
    public class VarianceAnalysisSchedulerService : QuartzSchedulerServiceBase<VarianceAnalysisJob>
    {
        private const string JobKeyName = "VarianceAnalysisJob";
        private const string TriggerKeyName = "VarianceAnalysisTrigger";
        private const string SchedulerName = "VarianceAnalysisScheduler";

        /// <summary>
        /// Initializes the variance analysis scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="appLifetime">Application lifetime for startup hook.</param>
        public VarianceAnalysisSchedulerService(
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
                    ConfigQueries.subscribeVarianceAnalysisConfigChanges))
        { }

        /// <inheritdoc/>
        protected override int SleepTime => globalConfig.VarianceAnalysisSleepTime;

        /// <inheritdoc/>
        protected override DateTime StartAt => globalConfig.VarianceAnalysisStartAt;

        /// <inheritdoc/>
        protected override TimeSpan Interval => TimeSpan.FromMinutes(globalConfig.VarianceAnalysisSleepTime);

        /// <inheritdoc/>
        protected override string IntervalLogSuffix => "m";
    }
}
