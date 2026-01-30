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

        /// <summary>
        /// Gets the name of the job listener.
        /// </summary>
        public string Name => "JobExecutionTracker";

        /// <summary>
        /// Called by the scheduler when a job is about to be executed.
        /// </summary>
        /// <param name="context">The job execution context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed task.</returns>
        public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called by the scheduler when a job execution was vetoed by a trigger listener.
        /// </summary>
        /// <param name="context">The job execution context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed task.</returns>
        public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called by the scheduler after a job has been executed.
        /// </summary>
        /// <param name="context">The job execution context.</param>
        /// <param name="jobException">The exception thrown by the job, if any.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed task.</returns>
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

        /// <summary>
        /// Gets the last execution result for a given job.
        /// </summary>
        /// <param name="jobName">The name of the job.</param>
        /// <returns>The last execution result, or null if the job hasn't been executed yet.</returns>
        public JobExecutionResult? GetLastResult(string jobName)
        {
            executionResults.TryGetValue(jobName, out JobExecutionResult? result);
            return result;
        }
    }

    /// <summary>
    /// Represents the result of a job execution.
    /// </summary>
    public class JobExecutionResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the job execution was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the job execution failed.
        /// </summary>
        public string ErrorMessage { get; set; } = "";

        /// <summary>
        /// Gets or sets the timestamp when the job was executed.
        /// </summary>
        public DateTimeOffset ExecutedAt { get; set; }
    }
}
