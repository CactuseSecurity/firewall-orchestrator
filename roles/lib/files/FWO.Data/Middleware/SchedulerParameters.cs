using System;

namespace FWO.Data.Middleware
{
    /// <summary>Represents a Quartz job exposed to the UI.</summary>
    public class SchedulerJobInfo
    {
        public string JobName { get; set; } = "";
        public string Group { get; set; } = "";
        public DateTimeOffset? NextFireTimeUtc { get; set; }
        public DateTimeOffset? LastFireTimeUtc { get; set; }
        public string IntervalDescription { get; set; } = "";
        public string LastExecutionStatus { get; set; } = "";
        public string LastExecutionError { get; set; } = "";
    }

    /// <summary>Request payload to trigger a job manually.</summary>
    public class SchedulerJobTriggerParameters
    {
        public string JobName { get; set; } = "";
    }
}
