using Prometheus;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Central Prometheus metrics for the middleware server.
    /// </summary>
    public static class MiddlewarePrometheusMetrics
    {
        private static readonly Gauge kKnownJobCountGauge = Metrics.CreateGauge("fwo_middleware_scheduler_job_count",
            "Number of known Quartz jobs in the middleware server.");
        private static readonly Gauge kLastSuccessGauge = Metrics.CreateGauge("fwo_middleware_scheduler_job_last_success",
            "Whether the last execution of a scheduler job succeeded (1) or not (0).",
            new GaugeConfiguration { LabelNames = ["job"] });
        private static readonly Gauge kLastFailureGauge = Metrics.CreateGauge("fwo_middleware_scheduler_job_last_failure",
            "Whether the last execution of a scheduler job failed (1) or not (0).",
            new GaugeConfiguration { LabelNames = ["job"] });
        private static readonly Gauge kLastExecutionUnixTimeGauge = Metrics.CreateGauge("fwo_middleware_scheduler_job_last_execution_unixtime",
            "Unix timestamp of the last execution of a scheduler job.",
            new GaugeConfiguration { LabelNames = ["job"] });
        private static readonly Counter kExecutionsCounter = Metrics.CreateCounter("fwo_middleware_scheduler_job_executions_total",
            "Total number of scheduler job executions observed by the middleware server.",
            new CounterConfiguration { LabelNames = ["job", "result"] });

        /// <summary>
        /// Updates the known number of scheduler jobs.
        /// </summary>
        /// <param name="jobCount">The number of known jobs.</param>
        public static void UpdateKnownJobCount(int jobCount)
        {
            kKnownJobCountGauge.Set(Math.Max(0, jobCount));
        }

        /// <summary>
        /// Records one scheduler job execution.
        /// </summary>
        /// <param name="jobName">The executed job name.</param>
        /// <param name="success">True if the execution succeeded.</param>
        /// <param name="executedAt">The execution timestamp.</param>
        public static void RecordExecution(string jobName, bool success, DateTimeOffset executedAt)
        {
            kExecutionsCounter.WithLabels(jobName, success ? "success" : "failure").Inc();
            kLastSuccessGauge.WithLabels(jobName).Set(success ? 1 : 0);
            kLastFailureGauge.WithLabels(jobName).Set(success ? 0 : 1);
            kLastExecutionUnixTimeGauge.WithLabels(jobName).Set(executedAt.ToUnixTimeSeconds());
        }
    }
}
