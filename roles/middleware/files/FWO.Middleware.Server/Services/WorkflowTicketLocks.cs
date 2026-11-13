namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Per-ticket locks shared by all parts of the middleware that perform workflow actions for a ticket: the
    /// workflow action endpoint and the job performing the "While set" actions. Registered as singleton, so that
    /// both never work on the same ticket at the same time.
    /// </summary>
    public sealed class WorkflowTicketLocks
    {
        // A ticket promotion can fan out into one action request per request task. Do not cap
        // callers for one ticket: the ticket itself determines the fan-out size. Keep the global
        // key cap and the per-owner distinct-ticket cap as flood protection.
        private readonly KeyedAsyncLock<long> ticketLocks = new(
            maxCallersPerKey: int.MaxValue,
            maxCallersPerOwnerAndKey: int.MaxValue);

        /// <summary>
        /// Number of tickets currently locked or waited for.
        /// </summary>
        public int Count => ticketLocks.Count;

        /// <summary>
        /// Waits until the lock of the ticket is acquired, unless a cap is exceeded.
        /// </summary>
        /// <param name="ticketId">Id of the ticket.</param>
        /// <param name="owner">Identity of the caller the per-owner cap is counted for.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        /// <returns>A handle that releases the lock when disposed, or null if a cap is exceeded.</returns>
        public Task<IDisposable?> TryAcquireAsync(long ticketId, string owner, CancellationToken cancellationToken = default)
        {
            return ticketLocks.TryAcquireAsync(ticketId, owner, cancellationToken);
        }

        /// <summary>
        /// Acquires the lock of the ticket only if it is free right now, without waiting for it.
        /// </summary>
        /// <param name="ticketId">Id of the ticket.</param>
        /// <param name="owner">Identity of the caller the per-owner cap is counted for.</param>
        /// <returns>A handle that releases the lock when disposed, or null if the ticket is locked or a cap is exceeded.</returns>
        public IDisposable? TryAcquireImmediately(long ticketId, string owner)
        {
            return ticketLocks.TryAcquireImmediately(ticketId, owner);
        }
    }
}
