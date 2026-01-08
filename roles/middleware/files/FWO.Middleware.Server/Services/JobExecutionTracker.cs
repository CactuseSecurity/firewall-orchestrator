using FWO.Logging;
using Quartz;
using System.Collections.Concurrent;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Tracks last execution result for each job.
    /// </summary>
    public class JobExecutionTracker : IJobListener
    {
        private readonly ConcurrentDictionary<string, JobExecutionResult> executionResults = new();

        public string Name => "JobExecutionTracker";

        public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
        {
            string jobKey = context.JobDetail.Key.Name;
            bool success = jobException == null;
            string? errorMessage = jobException?.Message;

            executionResults[jobKey] = new JobExecutionResult
            {
                Success = success,
                ErrorMessage = errorMessage ?? "",
                ExecutedAt = DateTimeOffset.Now
            };

            if (!success)
            {
                Log.WriteWarning("Job Execution", $"Job {jobKey} failed: {errorMessage}");
            }

            return Task.CompletedTask;
        }

        public JobExecutionResult? GetLastResult(string jobName)
        {
            executionResults.TryGetValue(jobName, out JobExecutionResult? result);
            return result;
        }
    }

    public class JobExecutionResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public DateTimeOffset ExecutedAt { get; set; }
    }
}
