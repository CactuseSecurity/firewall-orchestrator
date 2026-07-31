using FWO.Ui.Services;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;

namespace FWO.Test
{
    [TestFixture]
    public class PeriodicTaskRunnerTest
    {
        [Test]
        public async Task Start_WhenStarted_ShouldExecuteCallbackPeriodically()
        {
            int executionCount = 0;
            TaskCompletionSource<bool> callbackReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

            using PeriodicTaskRunner runner = new(async () =>
            {
                if (Interlocked.Increment(ref executionCount) >= 2)
                {
                    callbackReached.TrySetResult(true);
                }

                await Task.CompletedTask;
            }, TimeSpan.FromMilliseconds(20));

            runner.Start();

            Task completedTask = await Task.WhenAny(callbackReached.Task, Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.That(completedTask, Is.EqualTo(callbackReached.Task));
            Assert.That(executionCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task Dispose_StopsFurtherExecutions()
        {
            int executionCount = 0;
            TaskCompletionSource<bool> firstTickReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

            PeriodicTaskRunner runner = new(async () =>
            {
                if (Interlocked.Increment(ref executionCount) == 1)
                {
                    firstTickReached.TrySetResult(true);
                }

                await Task.CompletedTask;
            }, TimeSpan.FromMilliseconds(20));

            runner.Start();

            Task completedTask = await Task.WhenAny(firstTickReached.Task, Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.That(completedTask, Is.EqualTo(firstTickReached.Task));

            runner.Dispose();
            int executionCountAfterDispose = executionCount;

            await Task.Delay(TimeSpan.FromMilliseconds(100));

            Assert.That(executionCount, Is.EqualTo(executionCountAfterDispose));
        }

        [Test]
        public async Task Start_CalledTwice_SecondCallIsIgnored()
        {
            int executionCount = 0;
            TaskCompletionSource<bool> firstTickReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseCallback = new(TaskCreationOptions.RunContinuationsAsynchronously);

            using PeriodicTaskRunner runner = new(async () =>
            {
                if (Interlocked.Increment(ref executionCount) == 1)
                {
                    firstTickReached.TrySetResult(true);
                }

                await releaseCallback.Task;
            }, TimeSpan.FromMilliseconds(20));

            runner.Start();
            Assert.DoesNotThrow(runner.Start);

            Task completedTask = await Task.WhenAny(firstTickReached.Task, Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.That(completedTask, Is.EqualTo(firstTickReached.Task));
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            int executionCountAfterSecondStart = executionCount;
            releaseCallback.TrySetResult(true);

            Assert.That(executionCountAfterSecondStart, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_CalledTwice_IsIdempotent()
        {
            PeriodicTaskRunner runner = new(() => Task.CompletedTask, TimeSpan.FromMilliseconds(20));

            runner.Start();
            runner.Dispose();

            Assert.DoesNotThrow(runner.Dispose);
        }

        [Test]
        public void Start_AfterDispose_Throws()
        {
            PeriodicTaskRunner runner = new(() => Task.CompletedTask, TimeSpan.FromMilliseconds(20));

            runner.Dispose();

            Assert.Throws<ObjectDisposedException>(runner.Start);
        }

        [Test]
        public async Task Dispose_WhileCallbackRunning_WaitsForLoopToStop()
        {
            int executionCount = 0;
            TaskCompletionSource<bool> callbackEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseCallback = new(TaskCreationOptions.RunContinuationsAsynchronously);

            PeriodicTaskRunner runner = new(async () =>
            {
                Interlocked.Increment(ref executionCount);
                callbackEntered.TrySetResult(true);
                await releaseCallback.Task;
            }, TimeSpan.FromMilliseconds(20));

            runner.Start();

            Task enteredTask = await Task.WhenAny(callbackEntered.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.That(enteredTask, Is.EqualTo(callbackEntered.Task));

            // dispose blocks until the loop task has finished, so run it in the background
            // while the callback is still executing, then let the callback complete
            Task disposeTask = Task.Run(runner.Dispose);
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            releaseCallback.TrySetResult(true);

            Task completedTask = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.That(completedTask, Is.EqualTo(disposeTask));
            Assert.That(executionCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task DisposeAsync_StopsFurtherExecutions()
        {
            int executionCount = 0;
            TaskCompletionSource<bool> firstTickReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

            PeriodicTaskRunner runner = new(() =>
            {
                if (Interlocked.Increment(ref executionCount) == 1)
                {
                    firstTickReached.TrySetResult(true);
                }
                return Task.CompletedTask;
            }, TimeSpan.FromMilliseconds(20));

            runner.Start();
            Task completedTask = await Task.WhenAny(firstTickReached.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.That(completedTask, Is.EqualTo(firstTickReached.Task));

            await runner.DisposeAsync();
            int executionCountAfterDispose = executionCount;
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            Assert.That(executionCount, Is.EqualTo(executionCountAfterDispose));
        }

        [Test]
        public async Task DisposeAsync_CalledTwice_IsIdempotent()
        {
            PeriodicTaskRunner runner = new(() => Task.CompletedTask, TimeSpan.FromMilliseconds(20));
            runner.Start();

            await runner.DisposeAsync();

            Assert.DoesNotThrowAsync(async () => await runner.DisposeAsync());
        }

        [Test]
        public async Task DisposeAsync_AfterDispose_IsIdempotent()
        {
            PeriodicTaskRunner runner = new(() => Task.CompletedTask, TimeSpan.FromMilliseconds(20));
            runner.Start();
            runner.Dispose();

            Assert.DoesNotThrowAsync(async () => await runner.DisposeAsync());
            await Task.CompletedTask;
        }

        [Test]
        public void Start_DoesNotCaptureTheSynchronizationContextOfTheCaller()
        {
            SingleThreadedSynchronizationContext dispatcher = new();
            TaskCompletionSource<bool> tickReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

            // Starting and disposing on a single threaded dispatcher used to deadlock: the loop resumed on
            // the dispatcher thread, while that very thread was blocked inside Dispose waiting for the loop.
            bool completed = dispatcher.Run(() =>
            {
                PeriodicTaskRunner runner = new(() =>
                {
                    tickReached.TrySetResult(true);
                    return Task.CompletedTask;
                }, TimeSpan.FromMilliseconds(20));

                runner.Start();
                tickReached.Task.Wait(TimeSpan.FromSeconds(2));
                runner.Dispose();
            }, TimeSpan.FromSeconds(10));

            Assert.Multiple(() =>
            {
                Assert.That(completed, Is.True, "starting and disposing the runner on a dispatcher thread must not deadlock");
                Assert.That(tickReached.Task.IsCompletedSuccessfully, Is.True, "the loop must run without the dispatcher thread");
                Assert.That(dispatcher.QueuedWorkItems, Is.EqualTo(0), "the loop must not queue any work on the caller's context");
            });
        }

        [Test]
        public void DisposeAsync_OnADispatcherThreadDoesNotDeadlock()
        {
            SingleThreadedSynchronizationContext dispatcher = new();
            TaskCompletionSource<bool> tickReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

            bool completed = dispatcher.Run(() =>
            {
                PeriodicTaskRunner runner = new(() =>
                {
                    tickReached.TrySetResult(true);
                    return Task.CompletedTask;
                }, TimeSpan.FromMilliseconds(20));

                runner.Start();
                tickReached.Task.Wait(TimeSpan.FromSeconds(2));
                runner.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
            }, TimeSpan.FromSeconds(10));

            Assert.That(completed, Is.True);
        }

        [Test]
        public void Dispose_WithCallbackNeedingTheCallerThread_GivesUpInsteadOfDeadlocking()
        {
            SingleThreadedSynchronizationContext dispatcher = new();
            TaskCompletionSource<bool> callbackEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

            // worst case: the callback itself needs the disposing thread, so the loop can only end once that
            // thread is free again. Dispose has to give up after its timeout instead of blocking forever.
            bool completed = dispatcher.Run(() =>
            {
                PeriodicTaskRunner runner = new(async () =>
                {
                    callbackEntered.TrySetResult(true);
                    await dispatcher.PostAsync(() => { });
                }, TimeSpan.FromMilliseconds(20));

                runner.Start();
                callbackEntered.Task.Wait(TimeSpan.FromSeconds(5));
                runner.Dispose();
            }, TimeSpan.FromSeconds(60));

            Assert.Multiple(() =>
            {
                Assert.That(completed, Is.True, "Dispose must not block the caller indefinitely");
                Assert.That(dispatcher.QueuedWorkItems, Is.GreaterThan(0), "the callback should have been waiting for the dispatcher");
            });
        }

        [Test]
        public void Dispose_AfterGivingUp_StillReleasesTheCancellationSourceOnceTheLoopEnds()
        {
            SingleThreadedSynchronizationContext dispatcher = new();
            TaskCompletionSource<bool> callbackEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            PeriodicTaskRunner? abandonedRunner = null;

            bool completed = dispatcher.Run(() =>
            {
                PeriodicTaskRunner runner = new(async () =>
                {
                    callbackEntered.TrySetResult(true);
                    await dispatcher.PostAsync(() => { });
                }, TimeSpan.FromMilliseconds(20));

                abandonedRunner = runner;
                runner.Start();
                callbackEntered.Task.Wait(TimeSpan.FromSeconds(5));
                runner.Dispose();
            }, TimeSpan.FromSeconds(60));

            Assert.That(completed, Is.True);

            // the dispatcher thread is free again, so the abandoned loop can finish - and the runner has to
            // clean up after itself instead of leaking the cancellation source it gave up waiting for
            dispatcher.Drain();

            Assert.That(WaitForCancellationSourceRelease(abandonedRunner!), Is.True,
                "the cancellation source must be released once the abandoned loop ends");
        }

        /// <summary>
        /// Waits until the runner released its cancellation source, which is only observable internally.
        /// </summary>
        /// <param name="runner">Runner that was disposed without waiting for its loop.</param>
        /// <returns>True if the cancellation source was released within the timeout.</returns>
        private static bool WaitForCancellationSourceRelease(PeriodicTaskRunner runner)
        {
            FieldInfo releaseFlag = typeof(PeriodicTaskRunner).GetField("cancellationSourceDisposed",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(PeriodicTaskRunner), "cancellationSourceDisposed");

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if ((bool)releaseFlag.GetValue(runner)!)
                {
                    return true;
                }

                Thread.Sleep(20);
            }

            return false;
        }

        /// <summary>
        /// Minimal single threaded synchronization context imitating the Blazor render dispatcher: posted
        /// work can only run on the one dispatcher thread, so nothing progresses while that thread is busy.
        /// Queued work runs only when a test explicitly calls <see cref="Drain"/>.
        /// </summary>
        private sealed class SingleThreadedSynchronizationContext : SynchronizationContext
        {
            private readonly ConcurrentQueue<Action> queue = new();

            /// <summary>
            /// Number of work items waiting for the dispatcher thread.
            /// </summary>
            public int QueuedWorkItems => queue.Count;

            /// <inheritdoc />
            public override void Post(SendOrPostCallback callback, object? state)
            {
                queue.Enqueue(() => callback(state));
            }

            /// <summary>
            /// Runs everything queued so far, imitating a dispatcher thread that became free again.
            /// </summary>
            public void Drain()
            {
                while (queue.TryDequeue(out Action? work))
                {
                    work();
                }
            }

            /// <summary>
            /// Queues work on the dispatcher and returns a task completing once it ran.
            /// </summary>
            /// <param name="work">Work to run on the dispatcher thread.</param>
            /// <returns>A task representing the queued work.</returns>
            public Task PostAsync(Action work)
            {
                TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Post(_ =>
                {
                    work();
                    completion.TrySetResult();
                }, null);
                return completion.Task;
            }

            /// <summary>
            /// Runs the given body on a dedicated dispatcher thread.
            /// </summary>
            /// <param name="body">Body to execute on the dispatcher thread.</param>
            /// <param name="timeout">Maximum time the body may take.</param>
            /// <returns>True if the body finished within the timeout.</returns>
            public bool Run(Action body, TimeSpan timeout)
            {
                Thread dispatcherThread = new(() =>
                {
                    SetSynchronizationContext(this);
                    body();
                })
                { IsBackground = true };
                dispatcherThread.Start();
                return dispatcherThread.Join(timeout);
            }
        }
    }
}
