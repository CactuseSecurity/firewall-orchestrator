namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Serializes asynchronous work per key. Entries are reference counted: an entry exists only while
    /// a caller holds or waits for its key and is removed and disposed as soon as the last one leaves,
    /// so the number of entries is bounded by the number of concurrent callers, not by the number of
    /// distinct keys ever seen.
    /// </summary>
    /// <typeparam name="TKey">Type of the key the work is serialized on.</typeparam>
    public sealed class KeyedAsyncLock<TKey> where TKey : notnull
    {
        private readonly Dictionary<TKey, LockEntry> entries = [];
        private readonly Lock entriesLock = new();

        /// <summary>
        /// Number of keys currently held or waited for.
        /// </summary>
        public int Count
        {
            get
            {
                lock (entriesLock)
                {
                    return entries.Count;
                }
            }
        }

        /// <summary>
        /// Waits until the lock for the given key is acquired.
        /// </summary>
        /// <param name="key">Key to serialize on.</param>
        /// <param name="cancellationToken">Cancels the wait; the reservation of the key is released then.</param>
        /// <returns>A handle that releases the lock when disposed.</returns>
        public async Task<IDisposable> AcquireAsync(TKey key, CancellationToken cancellationToken = default)
        {
            LockEntry entry = AddReference(key);
            try
            {
                await entry.Semaphore.WaitAsync(cancellationToken);
            }
            catch
            {
                RemoveReference(key, entry);
                throw;
            }
            return new Releaser(this, key, entry);
        }

        private LockEntry AddReference(TKey key)
        {
            lock (entriesLock)
            {
                if (!entries.TryGetValue(key, out LockEntry? entry))
                {
                    entry = new LockEntry();
                    entries.Add(key, entry);
                }
                entry.References++;
                return entry;
            }
        }

        private void RemoveReference(TKey key, LockEntry entry)
        {
            lock (entriesLock)
            {
                entry.References--;
                if (entry.References == 0)
                {
                    entries.Remove(key);
                    entry.Semaphore.Dispose();
                }
            }
        }

        private void Release(TKey key, LockEntry entry)
        {
            entry.Semaphore.Release();
            RemoveReference(key, entry);
        }

        private sealed class LockEntry
        {
            public SemaphoreSlim Semaphore { get; } = new(1, 1);
            public int References { get; set; }
        }

        private sealed class Releaser(KeyedAsyncLock<TKey> owner, TKey key, LockEntry entry) : IDisposable
        {
            private int released;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref released, 1) == 0)
                {
                    owner.Release(key, entry);
                }
            }
        }
    }
}
