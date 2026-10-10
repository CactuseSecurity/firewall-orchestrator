using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class KeyedAsyncLockTest
    {
        private const int kUniqueKeyCount = 1000;
        private const int kConcurrentCallers = 20;
        private const string kOwner = "uid=user,ou=users,dc=test";
        private const string kOtherOwner = "uid=other,ou=users,dc=test";
        private static readonly TimeSpan kWaitTimeout = TimeSpan.FromSeconds(5);

        [Test]
        public async Task ManyUniqueKeysReturnToBaselineAfterRelease()
        {
            KeyedAsyncLock<long> keyedLock = new();

            for (long key = 1; key <= kUniqueKeyCount; key++)
            {
                using IDisposable handle = await Acquire(keyedLock, key);
                Assert.That(keyedLock.Count, Is.EqualTo(1));
            }

            Assert.Multiple(() =>
            {
                Assert.That(keyedLock.Count, Is.Zero);
                Assert.That(keyedLock.OwnerCount, Is.Zero);
            });
        }

        [Test]
        public async Task SameKeyIsSerialized()
        {
            KeyedAsyncLock<long> keyedLock = new();
            int activeHolders = 0;
            int maxActiveHolders = 0;

            List<Task> callers = [];
            for (int i = 0; i < kConcurrentCallers; i++)
            {
                string owner = $"uid=caller{i},ou=users,dc=test";
                callers.Add(Task.Run(async () =>
                {
                    using IDisposable handle = await Acquire(keyedLock, 42, owner);
                    int active = Interlocked.Increment(ref activeHolders);
                    InterlockedMax(ref maxActiveHolders, active);
                    await Task.Delay(1);
                    Interlocked.Decrement(ref activeHolders);
                }));
            }
            await Task.WhenAll(callers);

            Assert.Multiple(() =>
            {
                Assert.That(maxActiveHolders, Is.EqualTo(1));
                Assert.That(keyedLock.Count, Is.Zero);
            });
        }

        [Test]
        public async Task DifferentKeysDoNotBlockEachOther()
        {
            KeyedAsyncLock<long> keyedLock = new();

            using IDisposable first = await Acquire(keyedLock, 1);
            Task<IDisposable> second = Acquire(keyedLock, 2);

            Assert.That(await Task.WhenAny(second, Task.Delay(kWaitTimeout)), Is.SameAs(second));
            second.Result.Dispose();
            Assert.That(keyedLock.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task CancelledWaitReleasesReservationWithoutDisposingHeldLock()
        {
            KeyedAsyncLock<long> keyedLock = new();
            IDisposable holder = await Acquire(keyedLock, 7);
            using CancellationTokenSource cancellation = new();

            Task<IDisposable?> waiter = keyedLock.TryAcquireAsync(7, kOtherOwner, cancellation.Token);
            Assert.That(keyedLock.OwnerCount, Is.EqualTo(2));
            await cancellation.CancelAsync();

            await Assert.ThatAsync(async () => await waiter, Throws.InstanceOf<OperationCanceledException>());
            Assert.Multiple(() =>
            {
                Assert.That(keyedLock.Count, Is.EqualTo(1));
                Assert.That(keyedLock.OwnerCount, Is.EqualTo(1));
            });

            holder.Dispose();
            Assert.That(keyedLock.Count, Is.Zero);

            using IDisposable reacquired = await Acquire(keyedLock, 7);
            Assert.That(keyedLock.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task AlreadyCancelledTokenLeavesNoEntry()
        {
            KeyedAsyncLock<long> keyedLock = new();
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();

            await Assert.ThatAsync(async () => await keyedLock.TryAcquireAsync(3, kOwner, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
            Assert.Multiple(() =>
            {
                Assert.That(keyedLock.Count, Is.Zero);
                Assert.That(keyedLock.OwnerCount, Is.Zero);
            });
        }

        [Test]
        public async Task ExceptionInsideLockReleasesEntry()
        {
            KeyedAsyncLock<long> keyedLock = new();

            await Assert.ThatAsync(async () =>
            {
                using IDisposable handle = await Acquire(keyedLock, 9);
                throw new InvalidOperationException("action failed");
            }, Throws.InvalidOperationException);

            Assert.That(keyedLock.Count, Is.Zero);
            using IDisposable reacquired = await Acquire(keyedLock, 9);
            Assert.That(keyedLock.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task WaiterAcquiresAfterHolderReleasesAndEntryIsKeptMeanwhile()
        {
            KeyedAsyncLock<long> keyedLock = new();
            IDisposable holder = await Acquire(keyedLock, 11);
            Task<IDisposable> waiter = Acquire(keyedLock, 11);

            Assert.That(waiter.IsCompleted, Is.False);
            holder.Dispose();
            IDisposable second = await waiter.WaitAsync(kWaitTimeout);

            Assert.That(keyedLock.Count, Is.EqualTo(1));
            second.Dispose();
            Assert.That(keyedLock.Count, Is.Zero);
        }

        [Test]
        public async Task DisposingHandleTwiceReleasesOnlyOnce()
        {
            KeyedAsyncLock<long> keyedLock = new();
            IDisposable holder = await Acquire(keyedLock, 13);
            Task<IDisposable> waiter = Acquire(keyedLock, 13);

            holder.Dispose();
            IDisposable second = await waiter.WaitAsync(kWaitTimeout);
            holder.Dispose();

            Task<IDisposable> third = Acquire(keyedLock, 13);
            Assert.Multiple(() =>
            {
                Assert.That(third.IsCompleted, Is.False);
                Assert.That(keyedLock.Count, Is.EqualTo(1));
            });
            second.Dispose();
            (await third.WaitAsync(kWaitTimeout)).Dispose();
            Assert.That(keyedLock.Count, Is.Zero);
        }

        [Test]
        public async Task GlobalKeyCapRejectsNewKeysButNotExistingOnes()
        {
            KeyedAsyncLock<long> keyedLock = new(maxKeys: 2, maxKeysPerOwner: 10);
            IDisposable first = await Acquire(keyedLock, 1);
            using IDisposable second = await Acquire(keyedLock, 2, kOtherOwner);

            IDisposable? rejected = await keyedLock.TryAcquireAsync(3, kOwner);
            Task<IDisposable> waiterOnExistingKey = Acquire(keyedLock, 1, kOtherOwner);

            Assert.Multiple(() =>
            {
                Assert.That(rejected, Is.Null);
                Assert.That(waiterOnExistingKey.IsCompleted, Is.False);
                Assert.That(keyedLock.Count, Is.EqualTo(2));
            });
            first.Dispose();
            (await waiterOnExistingKey.WaitAsync(kWaitTimeout)).Dispose();
        }

        [Test]
        public async Task CallersPerKeyCapRejectsFurtherWaiters()
        {
            KeyedAsyncLock<long> keyedLock = new(maxCallersPerKey: 2);
            IDisposable holder = await Acquire(keyedLock, 5);
            Task<IDisposable> waiter = Acquire(keyedLock, 5, kOtherOwner);

            IDisposable? rejected = await keyedLock.TryAcquireAsync(5, kOwner);

            Assert.That(rejected, Is.Null);
            holder.Dispose();
            (await waiter.WaitAsync(kWaitTimeout)).Dispose();
            Assert.Multiple(() =>
            {
                Assert.That(keyedLock.Count, Is.Zero);
                Assert.That(keyedLock.OwnerCount, Is.Zero);
            });
        }

        [Test]
        public async Task KeysPerOwnerCapRejectsOnlyThatOwner()
        {
            KeyedAsyncLock<long> keyedLock = new(maxKeysPerOwner: 2);
            IDisposable first = await Acquire(keyedLock, 1);
            IDisposable second = await Acquire(keyedLock, 2);

            IDisposable? rejectedNewKey = await keyedLock.TryAcquireAsync(3, kOwner);
            IDisposable? rejectedForeignKey = await keyedLock.TryAcquireAsync(4, kOwner);
            using IDisposable otherOwner = await Acquire(keyedLock, 3, kOtherOwner);
            Task<IDisposable> sameOwnerSameKey = Acquire(keyedLock, 1);

            Assert.Multiple(() =>
            {
                Assert.That(rejectedNewKey, Is.Null);
                Assert.That(rejectedForeignKey, Is.Null);
                Assert.That(sameOwnerSameKey.IsCompleted, Is.False);
            });

            second.Dispose();
            using IDisposable afterRelease = await Acquire(keyedLock, 4);
            first.Dispose();
            (await sameOwnerSameKey.WaitAsync(kWaitTimeout)).Dispose();
        }

        [Test]
        public async Task OwnerMayJoinExistingKeyOfOtherOwnerOnlyWithinOwnCap()
        {
            KeyedAsyncLock<long> keyedLock = new(maxKeysPerOwner: 1);
            using IDisposable foreign = await Acquire(keyedLock, 1, kOtherOwner);
            using IDisposable own = await Acquire(keyedLock, 2);

            IDisposable? rejected = await keyedLock.TryAcquireAsync(1, kOwner);

            Assert.That(rejected, Is.Null);
        }

        /// <summary>
        /// One owner must not be able to occupy all caller slots of a key: further callers of other owners are
        /// still admitted (review finding F1 of SEC-24).
        /// </summary>
        [Test]
        public async Task CallersPerOwnerAndKeyCapLeavesSlotsForOtherOwners()
        {
            KeyedAsyncLock<long> keyedLock = new(maxCallersPerKey: 4, maxCallersPerOwnerAndKey: 2);
            IDisposable holder = await Acquire(keyedLock, 5);
            Task<IDisposable> sameOwnerWaiter = Acquire(keyedLock, 5);

            IDisposable? rejectedThirdCall = await keyedLock.TryAcquireAsync(5, kOwner);
            Task<IDisposable> otherOwnerWaiter = Acquire(keyedLock, 5, kOtherOwner);

            Assert.Multiple(() =>
            {
                Assert.That(rejectedThirdCall, Is.Null);
                Assert.That(otherOwnerWaiter.IsCompleted, Is.False);
            });
            holder.Dispose();
            (await sameOwnerWaiter.WaitAsync(kWaitTimeout)).Dispose();
            (await otherOwnerWaiter.WaitAsync(kWaitTimeout)).Dispose();
            Assert.That(keyedLock.Count, Is.Zero);
        }

        [Test]
        public void ConstructorRejectsNonPositiveCaps()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => new KeyedAsyncLock<long>(maxKeys: 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
                Assert.That(() => new KeyedAsyncLock<long>(maxCallersPerKey: 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
                Assert.That(() => new KeyedAsyncLock<long>(maxKeysPerOwner: -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
                Assert.That(() => new KeyedAsyncLock<long>(maxCallersPerOwnerAndKey: 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            });
        }

        private static async Task<IDisposable> Acquire(KeyedAsyncLock<long> keyedLock, long key, string owner = kOwner)
        {
            IDisposable? handle = await keyedLock.TryAcquireAsync(key, owner);
            Assert.That(handle, Is.Not.Null, $"Lock for key {key} and owner {owner} was rejected.");
            return handle!;
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current = Volatile.Read(ref target);
            while (value > current)
            {
                int previous = Interlocked.CompareExchange(ref target, value, current);
                if (previous == current)
                {
                    return;
                }
                current = previous;
            }
        }
    }
}
