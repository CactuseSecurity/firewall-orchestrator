using System.Collections.Concurrent;

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
    /// A lock entry exists only while a request holds or waits for it.
    /// </remarks>
    internal static class RefreshTokenLockRegistry
    {
        private static readonly ConcurrentDictionary<string, LockState> Locks = new();

        /// <summary>
        /// Waits for the lock of the given key.
        /// </summary>
        /// <param name="lockKey">Key identifying the refresh token, typically its hash.</param>
        /// <param name="timeout">How long to wait for a request that currently holds the lock.</param>
        /// <param name="cancellationToken">Cancels the wait, e.g. when the caller disconnects.</param>
        /// <returns>
        /// A lease that releases the lock when disposed, or null if the lock could not be
        /// taken within <paramref name="timeout"/>.
        /// </returns>
        /// <exception cref="OperationCanceledException">The wait was cancelled.</exception>
        public static async Task<IDisposable?> TryAcquireAsync(string lockKey, TimeSpan timeout, CancellationToken cancellationToken)
        {
            LockState lockState = Register(lockKey);
            bool acquired = false;
            try
            {
                acquired = await lockState.Semaphore.WaitAsync(timeout, cancellationToken);
            }
            finally
            {
                // A timed-out or cancelled waiter never holds the lock, so it only has to
                // take back its registration; otherwise the entry would never be removed.
                if (!acquired)
                {
                    Unregister(lockKey, lockState);
                }
            }

            return acquired ? new Lease(lockKey, lockState) : null;
        }

        /// <summary>
        /// Whether a request currently holds or waits for the lock of the given key.
        /// </summary>
        /// <param name="lockKey">Key identifying the refresh token.</param>
        /// <returns>True while a lock entry exists for the key.</returns>
        internal static bool IsTracked(string lockKey)
        {
            return Locks.ContainsKey(lockKey);
        }

        /// <summary>
        /// Adds the caller to the lock entry of the key, creating the entry if needed.
        /// </summary>
        /// <param name="lockKey">Key identifying the refresh token.</param>
        /// <returns>The entry the caller is now registered with.</returns>
        private static LockState Register(string lockKey)
        {
            while (true)
            {
                LockState lockState = Locks.GetOrAdd(lockKey, _ => new LockState());
                lock (lockState.SyncRoot)
                {
                    // An entry that is being removed must not be reused; retry with a new one.
                    if (!lockState.Removed)
                    {
                        lockState.ReferenceCount++;
                        return lockState;
                    }
                }
            }
        }

        /// <summary>
        /// Removes the caller from the lock entry and deletes the entry once nobody uses it.
        /// </summary>
        /// <param name="lockKey">Key identifying the refresh token.</param>
        /// <param name="lockState">Entry the caller was registered with.</param>
        private static void Unregister(string lockKey, LockState lockState)
        {
            bool removeLock = false;
            lock (lockState.SyncRoot)
            {
                lockState.ReferenceCount--;
                if (lockState.ReferenceCount == 0)
                {
                    lockState.Removed = true;
                    removeLock = true;
                }
            }

            if (removeLock)
            {
                Locks.TryRemove(new KeyValuePair<string, LockState>(lockKey, lockState));
            }
        }

        /// <summary>
        /// Lock entry of one refresh token: the mutex plus the number of requests holding or
        /// waiting for it.
        /// </summary>
        private sealed class LockState
        {
            /// <summary>Guards <see cref="ReferenceCount"/> and <see cref="Removed"/>.</summary>
            public object SyncRoot { get; } = new();

            /// <summary>Mutex that serializes the requests for this token.</summary>
            public SemaphoreSlim Semaphore { get; } = new(1, 1);

            /// <summary>Number of requests holding or waiting for the lock.</summary>
            public int ReferenceCount { get; set; }

            /// <summary>True once the entry is being removed and must not be reused.</summary>
            public bool Removed { get; set; }
        }

        /// <summary>
        /// Held lock of one request; disposing it releases the lock exactly once.
        /// </summary>
        private sealed class Lease : IDisposable
        {
            private readonly string lockKey;
            private readonly LockState lockState;
            private bool disposed;

            /// <summary>
            /// Creates the lease for a lock the caller has just acquired.
            /// </summary>
            /// <param name="lockKey">Key identifying the refresh token.</param>
            /// <param name="lockState">Entry whose mutex the caller holds.</param>
            public Lease(string lockKey, LockState lockState)
            {
                this.lockKey = lockKey;
                this.lockState = lockState;
            }

            /// <summary>
            /// Releases the lock and removes the entry if no other request uses it.
            /// </summary>
            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                lockState.Semaphore.Release();
                Unregister(lockKey, lockState);
            }
        }
    }
}
