namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Serializes asynchronous work per key. Entries are reference counted: an entry exists only while
    /// a caller holds or waits for its key and is removed and disposed as soon as the last one leaves,
    /// so the number of entries is bounded by the number of concurrent callers, not by the number of
    /// distinct keys ever seen. Caps on the total number of keys, the callers per key and the distinct
    /// keys per owner bound this further against a flood of concurrent callers.
    /// </summary>
    /// <typeparam name="TKey">Type of the key the work is serialized on.</typeparam>
    public sealed class KeyedAsyncLock<TKey> where TKey : notnull
    {
        /// <summary>Default maximum number of keys held or waited for at the same time.</summary>
        public const int kDefaultMaxKeys = 1000;
        /// <summary>Default maximum number of callers holding or waiting for one key.</summary>
        public const int kDefaultMaxCallersPerKey = 32;
        /// <summary>Default maximum number of distinct keys one owner holds or waits for at the same time.</summary>
        public const int kDefaultMaxKeysPerOwner = 8;

        private readonly Dictionary<TKey, LockEntry> entries = [];
        private readonly Dictionary<string, int> keysPerOwner = [];
        private readonly Lock entriesLock = new();
        private readonly int maxKeys;
        private readonly int maxCallersPerKey;
        private readonly int maxKeysPerOwner;

        /// <summary>
        /// Creates the lock with the given caps.
        /// </summary>
        /// <param name="maxKeys">Maximum number of keys held or waited for at the same time.</param>
        /// <param name="maxCallersPerKey">Maximum number of callers holding or waiting for one key.</param>
        /// <param name="maxKeysPerOwner">Maximum number of distinct keys one owner holds or waits for at the same time.</param>
        public KeyedAsyncLock(int maxKeys = kDefaultMaxKeys, int maxCallersPerKey = kDefaultMaxCallersPerKey,
            int maxKeysPerOwner = kDefaultMaxKeysPerOwner)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxKeys);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCallersPerKey);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxKeysPerOwner);
            this.maxKeys = maxKeys;
            this.maxCallersPerKey = maxCallersPerKey;
            this.maxKeysPerOwner = maxKeysPerOwner;
        }

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
        /// Number of owners currently holding or waiting for at least one key.
        /// </summary>
        public int OwnerCount
        {
            get
            {
                lock (entriesLock)
                {
                    return keysPerOwner.Count;
                }
            }
        }

        /// <summary>
        /// Waits until the lock for the given key is acquired, unless a cap is exceeded.
        /// </summary>
        /// <param name="key">Key to serialize on.</param>
        /// <param name="owner">Identity of the caller the per-owner cap is counted for.</param>
        /// <param name="cancellationToken">Cancels the wait; the reservation of the key is released then.</param>
        /// <returns>A handle that releases the lock when disposed, or null if a cap is exceeded.</returns>
        public async Task<IDisposable?> TryAcquireAsync(TKey key, string owner, CancellationToken cancellationToken = default)
        {
            LockEntry? entry = TryAddReference(key, owner);
            if (entry == null)
            {
                return null;
            }

            try
            {
                await entry.Semaphore.WaitAsync(cancellationToken);
            }
            catch
            {
                RemoveReference(key, owner, entry);
                throw;
            }
            return new Releaser(this, key, owner, entry);
        }

        private LockEntry? TryAddReference(TKey key, string owner)
        {
            lock (entriesLock)
            {
                bool keyExists = entries.TryGetValue(key, out LockEntry? entry);
                if (!CanAddReference(entry, owner, keyExists))
                {
                    return null;
                }
                if (entry == null)
                {
                    entry = new LockEntry();
                    entries.Add(key, entry);
                }

                int ownerReferences = entry.OwnerReferences.GetValueOrDefault(owner);
                if (ownerReferences == 0)
                {
                    keysPerOwner[owner] = keysPerOwner.GetValueOrDefault(owner) + 1;
                }
                entry.OwnerReferences[owner] = ownerReferences + 1;
                entry.References++;
                return entry;
            }
        }

        private bool CanAddReference(LockEntry? entry, string owner, bool keyExists)
        {
            if (!keyExists)
            {
                return entries.Count < maxKeys && keysPerOwner.GetValueOrDefault(owner) < maxKeysPerOwner;
            }
            return entry!.References < maxCallersPerKey
                && (entry.OwnerReferences.ContainsKey(owner) || keysPerOwner.GetValueOrDefault(owner) < maxKeysPerOwner);
        }

        private void RemoveReference(TKey key, string owner, LockEntry entry)
        {
            lock (entriesLock)
            {
                int ownerReferences = entry.OwnerReferences[owner] - 1;
                if (ownerReferences > 0)
                {
                    entry.OwnerReferences[owner] = ownerReferences;
                }
                else
                {
                    entry.OwnerReferences.Remove(owner);
                    RemoveOwnerKey(owner);
                }

                entry.References--;
                if (entry.References == 0)
                {
                    entries.Remove(key);
                    entry.Semaphore.Dispose();
                }
            }
        }

        private void RemoveOwnerKey(string owner)
        {
            int ownerKeys = keysPerOwner[owner] - 1;
            if (ownerKeys > 0)
            {
                keysPerOwner[owner] = ownerKeys;
            }
            else
            {
                keysPerOwner.Remove(owner);
            }
        }

        private void Release(TKey key, string owner, LockEntry entry)
        {
            entry.Semaphore.Release();
            RemoveReference(key, owner, entry);
        }

        private sealed class LockEntry
        {
            public SemaphoreSlim Semaphore { get; } = new(1, 1);
            public Dictionary<string, int> OwnerReferences { get; } = [];
            public int References { get; set; }
        }

        private sealed class Releaser(KeyedAsyncLock<TKey> keyedLock, TKey key, string owner, LockEntry entry) : IDisposable
        {
            private int released;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref released, 1) == 0)
                {
                    keyedLock.Release(key, owner, entry);
                }
            }
        }
    }
}
