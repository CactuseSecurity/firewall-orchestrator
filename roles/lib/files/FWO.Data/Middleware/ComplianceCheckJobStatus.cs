namespace FWO.Data.Middleware
{
    /// <summary>
    /// Represents the lifecycle state of an asynchronously executed compliance check.
    /// </summary>
    public enum ComplianceCheckExecutionStatus
    {
        Queued = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3
    }

    /// <summary>
    /// Represents the current status of an asynchronously executed compliance check.
    /// </summary>
    public class ComplianceCheckJobStatus
    {
        public string JobId { get; set; } = "";
        public ComplianceCheckExecutionStatus Status { get; set; }
        public string Message { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
    }

    /// <summary>
    /// Contains the identifier of a newly started compliance check job.
    /// </summary>
    public class ComplianceCheckStartResult
    {
        public string JobId { get; set; } = "";
    }
}
