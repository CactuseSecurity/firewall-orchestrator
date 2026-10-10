using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class KeyedAsyncLockTest
    {
        private const int kUniqueKeyCount = 1000;
        private const int kConcurrentCallers = 20;
        private static readonly TimeSpan kWaitTimeout = TimeSpan.FromSeconds(5);

        [Test]
        public async Task ManyUniqueKeysReturnToBaselineAfterRelease()
        {
            KeyedAsyncLock<long> keyedLock = new();

            for (long key = 1; key <= kUniqueKeyCount; key++)
            {
                using IDisposable handle = await keyedLock.AcquireAsync(key);
                Assert.That(keyedLock.Count, Is.EqualTo(1));
            }

            Assert.That(keyedLock.Count, Is.Zero);
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
                callers.Add(Task.Run(async () =>
                {
                    using IDisposable handle = await keyedLock.AcquireAsync(42);
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

            using IDisposable first = await keyedLock.AcquireAsync(1);
            Task<IDisposable> second = keyedLock.AcquireAsync(2);

            Assert.That(await Task.WhenAny(second, Task.Delay(kWaitTimeout)), Is.SameAs(second));
            second.Result.Dispose();
            Assert.That(keyedLock.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task CancelledWaitReleasesReservationWithoutDisposingHeldLock()
        {
            KeyedAsyncLock<long> keyedLock = new();
            IDisposable holder = await keyedLock.AcquireAsync(7);
            using CancellationTokenSource cancellation = new();

            Task<IDisposable> waiter = keyedLock.AcquireAsync(7, cancellation.Token);
            Assert.That(keyedLock.Count, Is.EqualTo(1));
            await cancellation.CancelAsync();

            Assert.That(async () => await waiter, Throws.InstanceOf<OperationCanceledException>());
            Assert.That(keyedLock.Count, Is.EqualTo(1));

            holder.Dispose();
            Assert.That(keyedLock.Count, Is.Zero);

            using IDisposable reacquired = await keyedLock.AcquireAsync(7);
            Assert.That(keyedLock.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task AlreadyCancelledTokenLeavesNoEntry()
        {
            KeyedAsyncLock<long> keyedLock = new();
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();

            Assert.That(async () => await keyedLock.AcquireAsync(3, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
            Assert.That(keyedLock.Count, Is.Zero);
        }

        [Test]
        public async Task ExceptionInsideLockReleasesEntry()
        {
            KeyedAsyncLock<long> keyedLock = new();

            Assert.That(async () =>
            {
                using IDisposable handle = await keyedLock.AcquireAsync(9);
                throw new InvalidOperationException("action failed");
            }, Throws.InvalidOperationException);

            Assert.That(keyedLock.Count, Is.Zero);
            using IDisposable reacquired = await keyedLock.AcquireAsync(9);
            Assert.That(keyedLock.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task WaiterAcquiresAfterHolderReleasesAndEntryIsKeptMeanwhile()
        {
            KeyedAsyncLock<long> keyedLock = new();
            IDisposable holder = await keyedLock.AcquireAsync(11);
            Task<IDisposable> waiter = keyedLock.AcquireAsync(11);

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
            IDisposable holder = await keyedLock.AcquireAsync(13);
            Task<IDisposable> waiter = keyedLock.AcquireAsync(13);

            holder.Dispose();
            using IDisposable second = await waiter.WaitAsync(kWaitTimeout);
            holder.Dispose();

            Task<IDisposable> third = keyedLock.AcquireAsync(13);
            Assert.Multiple(() =>
            {
                Assert.That(third.IsCompleted, Is.False);
                Assert.That(keyedLock.Count, Is.EqualTo(1));
            });
            second.Dispose();
            (await third.WaitAsync(kWaitTimeout)).Dispose();
            Assert.That(keyedLock.Count, Is.Zero);
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
