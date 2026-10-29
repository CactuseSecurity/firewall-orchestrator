namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Serializes refresh-token rotation per token inside this process, so concurrent
    /// requests with the same token do not all rebuild the user before only one of them
    /// can consume it.
    /// </summary>
    /// <remarks>
    /// This only saves redundant work. Consuming a token exactly once is still guaranteed
    /// by the conditional revoke in the database, also across middleware instances.
    /// The locking itself, including the removal of unused entries and the caps against a
    /// flood of concurrent callers, is done by <see cref="KeyedAsyncLock{TKey}"/>; this class
    /// only adds the wait timeout.
    /// </remarks>
    internal static class RefreshTokenLockRegistry
    {
        // The token hash is the key and the owner: callers of the anonymous refresh endpoint cannot be
        // told apart reliably, since the UI server forwards the refreshes of all its users. With one owner
        // per key, the per-key cap is the one that applies, so the per-owner-and-key cap must not be lower.
        private static readonly KeyedAsyncLock<string> Locks = new(
            maxCallersPerOwnerAndKey: KeyedAsyncLock<string>.kDefaultMaxCallersPerKey);

        /// <summary>
        /// Waits for the lock of the given key.
        /// </summary>
        /// <param name="lockKey">Key identifying the refresh token, typically its hash.</param>
        /// <param name="timeout">How long to wait for a request that currently holds the lock.</param>
        /// <param name="cancellationToken">Cancels the wait, e.g. when the caller disconnects.</param>
        /// <returns>
        /// A lease that releases the lock when disposed, or null if the lock could not be
        /// taken within <paramref name="timeout"/> or too many requests use the lock already.
        /// </returns>
        /// <exception cref="OperationCanceledException">The wait was cancelled.</exception>
        public static Task<IDisposable?> TryAcquireAsync(string lockKey, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return TryAcquireAsync(Locks, lockKey, timeout, cancellationToken);
        }

        /// <summary>
        /// Waits for the lock of the given key in the given keyed lock.
        /// </summary>
        /// <param name="locks">Keyed lock to take the lock from.</param>
        /// <param name="lockKey">Key identifying the refresh token, typically its hash.</param>
        /// <param name="timeout">How long to wait for a request that currently holds the lock.</param>
        /// <param name="cancellationToken">Cancels the wait, e.g. when the caller disconnects.</param>
        /// <returns>
        /// A lease that releases the lock when disposed, or null if the lock could not be
        /// taken within <paramref name="timeout"/> or a cap of <paramref name="locks"/> is exceeded.
        /// </returns>
        /// <exception cref="OperationCanceledException">The wait was cancelled.</exception>
        internal static async Task<IDisposable?> TryAcquireAsync(KeyedAsyncLock<string> locks, string lockKey, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            try
            {
                return await locks.TryAcquireAsync(lockKey, lockKey, timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Only the timeout fired; the keyed lock has already taken back the registration.
                return null;
            }
        }

        /// <summary>
        /// Whether a request currently holds or waits for the lock of the given key.
        /// </summary>
        /// <param name="lockKey">Key identifying the refresh token.</param>
        /// <returns>True while a lock entry exists for the key.</returns>
        internal static bool IsTracked(string lockKey)
        {
            return Locks.Contains(lockKey);
        }
    }
}
