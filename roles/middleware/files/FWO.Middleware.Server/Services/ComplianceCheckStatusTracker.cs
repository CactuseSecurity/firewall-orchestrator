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
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, TerminalStatusWaiter>> terminalWaiters = new();

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
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<ComplianceCheckJobStatus>(cancellationToken);
            }

            ComplianceCheckJobStatus? currentStatus = Get(jobId);
            if (currentStatus is null)
            {
                return Task.FromException<ComplianceCheckJobStatus>(
                    new KeyNotFoundException($"Compliance check job '{jobId}' was not found."));
            }

            if (currentStatus?.Status is ComplianceCheckExecutionStatus.Succeeded or ComplianceCheckExecutionStatus.Failed)
            {
                return Task.FromResult(currentStatus);
            }

            TerminalStatusWaiter waiter = new();
            ConcurrentDictionary<Guid, TerminalStatusWaiter> jobWaiters = terminalWaiters.GetOrAdd(jobId, _ => new());
            jobWaiters[waiter.Id] = waiter;
            waiter.RegisterCancellation(this, jobId, cancellationToken);

            currentStatus = Get(jobId);
            if (currentStatus?.Status is ComplianceCheckExecutionStatus.Succeeded or ComplianceCheckExecutionStatus.Failed)
            {
                CompleteTerminalWaiters(jobId, currentStatus);
            }

            return waiter.Completion.Task;
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
            if (!terminalWaiters.TryRemove(jobId, out ConcurrentDictionary<Guid, TerminalStatusWaiter>? waiters))
            {
                return;
            }

            foreach (TerminalStatusWaiter waiter in waiters.Values)
            {
                waiter.Completion.TrySetResult(jobStatus);
            }
        }

        private void CancelTerminalWaiter(string jobId, TerminalStatusWaiter waiter, CancellationToken cancellationToken)
        {
            if (terminalWaiters.TryGetValue(jobId, out ConcurrentDictionary<Guid, TerminalStatusWaiter>? waiters)
                && waiters.TryRemove(waiter.Id, out TerminalStatusWaiter? removedWaiter))
            {
                removedWaiter.Completion.TrySetCanceled(cancellationToken);
            }
        }

        private sealed class TerminalStatusWaiter
        {
            private CancellationTokenRegistration cancellationRegistration;

            public Guid Id { get; } = Guid.NewGuid();

            public TaskCompletionSource<ComplianceCheckJobStatus> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public void RegisterCancellation(ComplianceCheckStatusTracker tracker, string jobId, CancellationToken cancellationToken)
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    return;
                }

                cancellationRegistration = cancellationToken.Register(static state =>
                {
                    TerminalStatusWaiterState waiterState = (TerminalStatusWaiterState)state!;
                    waiterState.Tracker.CancelTerminalWaiter(waiterState.JobId, waiterState.Waiter, waiterState.CancellationToken);
                }, new TerminalStatusWaiterState(tracker, jobId, this, cancellationToken));

                _ = Completion.Task.ContinueWith(
                    _ => cancellationRegistration.Dispose(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        private sealed record TerminalStatusWaiterState(
            ComplianceCheckStatusTracker Tracker,
            string JobId,
            TerminalStatusWaiter Waiter,
            CancellationToken CancellationToken);
    }
}
