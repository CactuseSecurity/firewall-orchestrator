using FWO.Data.Middleware;
using System.Collections.Concurrent;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Tracks manually started compliance check jobs and their current execution state.
    /// </summary>
    public class ComplianceCheckStatusTracker
    {
        private readonly ConcurrentDictionary<string, ComplianceCheckJobStatus> jobStatuses = new();
        private readonly ConcurrentDictionary<string, List<TaskCompletionSource<ComplianceCheckJobStatus>>> terminalWaiters = new();

        /// <summary>
        /// Creates a new queued compliance check status entry.
        /// </summary>
        /// <returns>The newly created job status.</returns>
        public ComplianceCheckJobStatus CreateQueuedJob()
        {
            ComplianceCheckJobStatus jobStatus = new()
            {
                JobId = Guid.NewGuid().ToString(),
                Status = ComplianceCheckExecutionStatus.Queued,
                CreatedAt = DateTimeOffset.Now
            };

            jobStatuses[jobStatus.JobId] = jobStatus;
            return jobStatus;
        }

        /// <summary>
        /// Returns the current status for a job if it exists.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <returns>The current job status or null when the id is unknown.</returns>
        public ComplianceCheckJobStatus? Get(string jobId)
        {
            jobStatuses.TryGetValue(jobId, out ComplianceCheckJobStatus? jobStatus);
            return jobStatus;
        }

        /// <summary>
        /// Returns the first currently active job.
        /// </summary>
        /// <returns>The running or queued job, or null when no active job exists.</returns>
        public ComplianceCheckJobStatus? GetActiveJob()
        {
            return jobStatuses.Values
                .Where(job => job.Status is ComplianceCheckExecutionStatus.Queued or ComplianceCheckExecutionStatus.Running)
                .OrderBy(job => job.CreatedAt)
                .FirstOrDefault();
        }

        /// <summary>
        /// Updates the job state to running.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        public void SetRunning(string jobId)
        {
            Update(jobId, ComplianceCheckExecutionStatus.Running, "");
        }

        /// <summary>
        /// Updates the job state to succeeded.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        public void SetSucceeded(string jobId)
        {
            Update(jobId, ComplianceCheckExecutionStatus.Succeeded, "");
        }

        /// <summary>
        /// Updates the job state to failed and stores the failure message.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <param name="message">The failure message.</param>
        public void SetFailed(string jobId, string message)
        {
            Update(jobId, ComplianceCheckExecutionStatus.Failed, message);
        }

        /// <summary>
        /// Waits until the specified job reaches a terminal state.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <param name="cancellationToken">Cancellation token for the wait operation.</param>
        /// <returns>The terminal job status.</returns>
        public Task<ComplianceCheckJobStatus> WaitForTerminalStatusAsync(string jobId, CancellationToken cancellationToken = default)
        {
            ComplianceCheckJobStatus? currentStatus = Get(jobId);
            if (currentStatus?.Status is ComplianceCheckExecutionStatus.Succeeded or ComplianceCheckExecutionStatus.Failed)
            {
                return Task.FromResult(currentStatus);
            }

            TaskCompletionSource<ComplianceCheckJobStatus> waiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => waiter.TrySetCanceled(cancellationToken));
            }

            List<TaskCompletionSource<ComplianceCheckJobStatus>> waiters = terminalWaiters.GetOrAdd(jobId, _ => []);
            lock (waiters)
            {
                waiters.Add(waiter);
            }

            currentStatus = Get(jobId);
            if (currentStatus?.Status is ComplianceCheckExecutionStatus.Succeeded or ComplianceCheckExecutionStatus.Failed)
            {
                CompleteTerminalWaiters(jobId, currentStatus);
            }

            return waiter.Task;
        }

        private void Update(string jobId, ComplianceCheckExecutionStatus status, string message)
        {
            jobStatuses.AddOrUpdate(
                jobId,
                _ => new ComplianceCheckJobStatus
                {
                    JobId = jobId,
                    Status = status,
                    Message = message,
                    CreatedAt = DateTimeOffset.Now,
                    FinishedAt = status is ComplianceCheckExecutionStatus.Succeeded or ComplianceCheckExecutionStatus.Failed ? DateTimeOffset.Now : null
                },
                (_, existingJobStatus) =>
                {
                    existingJobStatus.Status = status;
                    existingJobStatus.Message = message;
                    existingJobStatus.FinishedAt = status is ComplianceCheckExecutionStatus.Succeeded or ComplianceCheckExecutionStatus.Failed ? DateTimeOffset.Now : null;
                    return existingJobStatus;
                });

            if (status is ComplianceCheckExecutionStatus.Succeeded or ComplianceCheckExecutionStatus.Failed
                && jobStatuses.TryGetValue(jobId, out ComplianceCheckJobStatus? jobStatus))
            {
                CompleteTerminalWaiters(jobId, jobStatus);
            }
        }

        private void CompleteTerminalWaiters(string jobId, ComplianceCheckJobStatus jobStatus)
        {
            if (!terminalWaiters.TryRemove(jobId, out List<TaskCompletionSource<ComplianceCheckJobStatus>>? waiters))
            {
                return;
            }

            lock (waiters)
            {
                foreach (TaskCompletionSource<ComplianceCheckJobStatus> waiter in waiters)
                {
                    waiter.TrySetResult(jobStatus);
                }
            }
        }
    }
}
