using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class RefreshTokenLockRegistryTest
    {
        private const int kTestTimeoutMs = 10_000;

        [Test]
        [CancelAfter(kTestTimeoutMs)]
        public async Task TryAcquireAsync_WhenLockIsFree_ReturnsLeaseAndRemovesEntryOnDispose(CancellationToken cancellationToken)
        {
            string lockKey = NewLockKey();

            IDisposable? lease = await RefreshTokenLockRegistry.TryAcquireAsync(lockKey, TimeSpan.FromSeconds(1), cancellationToken);
            bool trackedWhileHeld = RefreshTokenLockRegistry.IsTracked(lockKey);
            lease?.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(lease, Is.Not.Null);
                Assert.That(trackedWhileHeld, Is.True);
                Assert.That(RefreshTokenLockRegistry.IsTracked(lockKey), Is.False);
            });
        }

        /// <summary>
        /// A waiter that gives up must take back its registration; otherwise the entry would
        /// outlive every request and the key would never be cleaned up.
        /// </summary>
        [Test]
        [CancelAfter(kTestTimeoutMs)]
        public async Task TryAcquireAsync_WhenLockIsHeld_TimesOutAndLeavesNoEntryBehind(CancellationToken cancellationToken)
        {
            string lockKey = NewLockKey();
            IDisposable holder = (await RefreshTokenLockRegistry.TryAcquireAsync(lockKey, TimeSpan.FromSeconds(1), cancellationToken))!;

            IDisposable? timedOut = await RefreshTokenLockRegistry.TryAcquireAsync(lockKey, TimeSpan.FromMilliseconds(50), cancellationToken);
            holder.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(timedOut, Is.Null);
                Assert.That(RefreshTokenLockRegistry.IsTracked(lockKey), Is.False);
            });
        }

        [Test]
        [CancelAfter(kTestTimeoutMs)]
        public async Task TryAcquireAsync_WhenWaitIsCancelled_ThrowsAndLeavesNoEntryBehind(CancellationToken cancellationToken)
        {
            string lockKey = NewLockKey();
            IDisposable holder = (await RefreshTokenLockRegistry.TryAcquireAsync(lockKey, TimeSpan.FromSeconds(1), cancellationToken))!;
            using CancellationTokenSource waitCancellation = new();

            Task<IDisposable?> waiter = RefreshTokenLockRegistry.TryAcquireAsync(lockKey, TimeSpan.FromMinutes(1), waitCancellation.Token);
            await waitCancellation.CancelAsync();

            Assert.CatchAsync<OperationCanceledException>(async () => await waiter);
            holder.Dispose();
            Assert.That(RefreshTokenLockRegistry.IsTracked(lockKey), Is.False);
        }

        [Test]
        [CancelAfter(kTestTimeoutMs)]
        public async Task TryAcquireAsync_AfterHolderReleases_WaiterGetsTheLock(CancellationToken cancellationToken)
        {
            string lockKey = NewLockKey();
            IDisposable holder = (await RefreshTokenLockRegistry.TryAcquireAsync(lockKey, TimeSpan.FromSeconds(1), cancellationToken))!;

            Task<IDisposable?> waiter = RefreshTokenLockRegistry.TryAcquireAsync(lockKey, TimeSpan.FromSeconds(5), cancellationToken);
            bool acquiredWhileHeld = waiter.IsCompleted;
            holder.Dispose();
            IDisposable? waiterLease = await waiter;
            waiterLease?.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(acquiredWhileHeld, Is.False);
                Assert.That(waiterLease, Is.Not.Null);
                Assert.That(RefreshTokenLockRegistry.IsTracked(lockKey), Is.False);
            });
        }

        /// <summary>
        /// A request beyond the cap of the keyed lock must be refused at once, not queued.
        /// </summary>
        [Test]
        [CancelAfter(kTestTimeoutMs)]
        public async Task TryAcquireAsync_WhenCapIsExceeded_ReturnsNullWithoutWaiting(CancellationToken cancellationToken)
        {
            string lockKey = NewLockKey();
            KeyedAsyncLock<string> locks = new(maxCallersPerKey: 1);
            IDisposable holder = (await RefreshTokenLockRegistry.TryAcquireAsync(locks, lockKey, TimeSpan.FromSeconds(1), cancellationToken))!;

            Task<IDisposable?> refused = RefreshTokenLockRegistry.TryAcquireAsync(locks, lockKey, TimeSpan.FromMinutes(1), cancellationToken);
            bool refusedAtOnce = refused.IsCompleted;
            IDisposable? refusedLease = await refused;
            holder.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(refusedAtOnce, Is.True);
                Assert.That(refusedLease, Is.Null);
                Assert.That(locks.Count, Is.Zero);
            });
        }

        private static string NewLockKey()
        {
            // The registry is process-wide, so every test uses its own key.
            return $"{nameof(RefreshTokenLockRegistryTest)}-{Guid.NewGuid()}";
        }
    }
}
