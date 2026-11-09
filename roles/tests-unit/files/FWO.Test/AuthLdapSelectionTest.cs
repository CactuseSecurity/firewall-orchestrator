using FWO.Middleware.Server;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AuthLdapSelectionTest
    {
        [Test]
        public void GetPreferredLdapIndexReturnsFirstSuccessfulIndex()
        {
            int selected = AuthLdapSelection.GetPreferredLdapIndex([false, true, true]);

            Assert.That(selected, Is.EqualTo(1));
        }

        [Test]
        public void GetPreferredLdapIndexReturnsZeroWhenFirstSucceeds()
        {
            int selected = AuthLdapSelection.GetPreferredLdapIndex([true, false, true]);

            Assert.That(selected, Is.EqualTo(0));
        }

        [Test]
        public void GetPreferredLdapIndexReturnsMinusOneWhenNoneSucceeds()
        {
            int selected = AuthLdapSelection.GetPreferredLdapIndex([false, false, false]);

            Assert.That(selected, Is.EqualTo(-1));
        }

        [Test]
        public void GetPreferredLdapIndexReturnsMinusOneForNullOrEmpty()
        {
            int selectedForNull = AuthLdapSelection.GetPreferredLdapIndex(null);
            int selectedForEmpty = AuthLdapSelection.GetPreferredLdapIndex([]);

            Assert.That(selectedForNull, Is.EqualTo(-1));
            Assert.That(selectedForEmpty, Is.EqualTo(-1));
        }
    }
    [TestFixture]
    [NonParallelizable]
    internal class LdapAuthenticationGateTest
    {
        [Test]
        public void RunAsync_RejectsTooManyDirectoriesBeforeStartingWork()
        {
            int started = 0;
            List<Func<CancellationToken, Task<int>>> attempts = Enumerable.Range(0, LdapAuthenticationGate.MaxDirectories + 1)
                .Select<int, Func<CancellationToken, Task<int>>>(index => token =>
                {
                    Interlocked.Increment(ref started);
                    return Task.FromResult(index);
                }).ToList();

            LoginCapacityException? exception = Assert.ThrowsAsync<LoginCapacityException>(async () =>
                await LdapAuthenticationGate.RunAsync(attempts, CancellationToken.None));
            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
            Assert.That(exception.Message, Does.StartWith("A0007"));
            Assert.That(started, Is.Zero);
        }

        [Test]
        public async Task RunAsync_CapsConcurrentAttemptsAndPreservesOrder()
        {
            int active = 0;
            int peak = 0;
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            List<Func<CancellationToken, Task<int>>> attempts = Enumerable.Range(0, LdapAuthenticationGate.MaxDirectories)
                .Select<int, Func<CancellationToken, Task<int>>>(index => async token =>
                {
                    int count = Interlocked.Increment(ref active);
                    InterlockedMax(ref peak, count);
                    try
                    {
                        await release.Task.WaitAsync(token);
                        return index;
                    }
                    finally
                    {
                        Interlocked.Decrement(ref active);
                    }
                }).ToList();

            Task<int[]> run = LdapAuthenticationGate.RunAsync(attempts, CancellationToken.None);
            await WaitForStartedAttemptsAsync(() => Volatile.Read(ref active) == LdapAuthenticationGate.kMaxConcurrentOperations);
            Assert.That(peak, Is.EqualTo(LdapAuthenticationGate.kMaxConcurrentOperations));
            release.SetResult();

            int[] results = await run;
            Assert.That(results, Is.EqualTo(Enumerable.Range(0, LdapAuthenticationGate.MaxDirectories)));
        }

        [Test]
        public async Task RunAsync_CancellationStopsQueuedAndActiveAttempts()
        {
            int started = 0;
            using CancellationTokenSource cancellation = new();
            List<Func<CancellationToken, Task<int>>> attempts = Enumerable.Range(0, LdapAuthenticationGate.MaxDirectories)
                .Select<int, Func<CancellationToken, Task<int>>>(index => async token =>
                {
                    Interlocked.Increment(ref started);
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return index;
                }).ToList();

            Task<int[]> run = LdapAuthenticationGate.RunAsync(attempts, cancellation.Token);
            await WaitForStartedAttemptsAsync(() => Volatile.Read(ref started) == LdapAuthenticationGate.kMaxConcurrentOperations);
            cancellation.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await run);
            Assert.That(started, Is.EqualTo(LdapAuthenticationGate.kMaxConcurrentOperations));
        }

        [Test]
        public async Task RunAsync_RejectsRequestsBeyondGlobalCapacity()
        {
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            List<Func<CancellationToken, Task<int>>> attempts = new()
            {
                async token =>
                {
                    await release.Task.WaitAsync(token);
                    return 1;
                }
            };
            List<Task<int[]>> running = [];
            for (int index = 0; index < LdapAuthenticationGate.kMaxConcurrentRequests; index++)
            {
                running.Add(LdapAuthenticationGate.RunAsync(attempts, CancellationToken.None));
            }

            LoginCapacityException? exception = Assert.ThrowsAsync<LoginCapacityException>(async () =>
                await LdapAuthenticationGate.RunAsync(attempts, CancellationToken.None));
            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCodes.Status429TooManyRequests));
            Assert.That(exception.Message, Does.StartWith("A0006"));
            release.SetResult();
            await Task.WhenAll(running);
        }

        [Test]
        public async Task RunAsync_DeadlineCancelsSlowAttemptsAndAnswersUnavailable()
        {
            int cancelled = 0;
            List<Func<CancellationToken, Task<int>>> attempts = Enumerable.Range(0, LdapAuthenticationGate.kMaxConcurrentOperations + 1)
                .Select<int, Func<CancellationToken, Task<int>>>(index => async token =>
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Increment(ref cancelled);
                        throw;
                    }
                    return index;
                }).ToList();
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            LoginCapacityException? exception = Assert.ThrowsAsync<LoginCapacityException>(async () =>
                await LdapAuthenticationGate.RunAsync(attempts, TimeSpan.FromMilliseconds(100), CancellationToken.None));

            Assert.That(exception!.StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
            await WaitForStartedAttemptsAsync(() => Volatile.Read(ref cancelled) == LdapAuthenticationGate.kMaxConcurrentOperations);
        }

        [Test]
        public async Task RunAsync_UsesConfiguredDirectoryCap()
        {
            List<Func<CancellationToken, Task<int>>> attempts = Enumerable.Range(0, 3)
                .Select<int, Func<CancellationToken, Task<int>>>(index => token => Task.FromResult(index)).ToList();
            try
            {
                LdapAuthenticationGate.Configure(new LoginThrottleSettings { MaxDirectories = 2 });
                Assert.ThrowsAsync<LoginCapacityException>(async () => await LdapAuthenticationGate.RunAsync(attempts, CancellationToken.None));

                LdapAuthenticationGate.Configure(new LoginThrottleSettings { MaxDirectories = 3 });
                Assert.That(await LdapAuthenticationGate.RunAsync(attempts, CancellationToken.None), Has.Length.EqualTo(3));
            }
            finally
            {
                LdapAuthenticationGate.Configure(new LoginThrottleSettings());
            }
        }

        /// <summary>Raises the peak to the value without losing concurrent updates.</summary>
        private static void InterlockedMax(ref int peak, int value)
        {
            int current = Volatile.Read(ref peak);
            while (value > current)
            {
                int observed = Interlocked.CompareExchange(ref peak, value, current);
                if (observed == current)
                {
                    return;
                }
                current = observed;
            }
        }

        /// <summary>Waits briefly for the asynchronous attempts to start.</summary>
        [Test]
        public async Task RunInSlotAsync_LoginOperationsShareTheSlotsAndOthersRunDirectly()
        {
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int started = 0;
            using CancellationTokenSource loginDeadline = new();
            List<Task<int>> occupying = Enumerable.Range(0, LdapAuthenticationGate.kMaxConcurrentOperations)
                .Select(index => LdapAuthenticationGate.RunInSlotAsync(async token =>
                {
                    Interlocked.Increment(ref started);
                    await release.Task.WaitAsync(token);
                    return index;
                }, loginDeadline.Token)).ToList();
            await WaitForStartedAttemptsAsync(() => Volatile.Read(ref started) == LdapAuthenticationGate.kMaxConcurrentOperations);

            Task<int> queuedLoginOperation = LdapAuthenticationGate.RunInSlotAsync(_ => Task.FromResult(1), loginDeadline.Token);
            int operationWithoutDeadline = await LdapAuthenticationGate.RunInSlotAsync(_ => Task.FromResult(2), CancellationToken.None);

            Assert.That(operationWithoutDeadline, Is.EqualTo(2), "an operation without a deadline must not wait for a slot");
            Assert.That(queuedLoginOperation.IsCompleted, Is.False, "a login operation has to wait for a free slot");
            release.SetResult();
            Assert.That(await queuedLoginOperation, Is.EqualTo(1));
            await Task.WhenAll(occupying);
        }

        private static async Task WaitForStartedAttemptsAsync(Func<bool> condition)
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
            while (!condition())
            {
                await Task.Delay(10, timeout.Token);
            }
        }
    }
}
