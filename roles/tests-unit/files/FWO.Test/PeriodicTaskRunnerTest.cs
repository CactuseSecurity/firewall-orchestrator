using FWO.Ui.Services;
using NUnit.Framework;
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
        public void Dispose_CalledTwice_ReturnsWithoutError()
        {
            PeriodicTaskRunner runner = new(() => Task.CompletedTask, TimeSpan.FromMilliseconds(20));
            runner.Start();

            runner.Dispose();

            Assert.DoesNotThrow(runner.Dispose);
        }

        [Test]
        public async Task Start_CalledTwice_RunsSingleExecutionLoop()
        {
            int executionCount = 0;
            TaskCompletionSource<bool> firstTickReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

            using PeriodicTaskRunner runner = new(async () =>
            {
                if (Interlocked.Increment(ref executionCount) == 1)
                {
                    firstTickReached.TrySetResult(true);
                }

                await Task.CompletedTask;
            }, TimeSpan.FromMilliseconds(50));

            runner.Start();
            Assert.DoesNotThrow(runner.Start);

            Task completedTask = await Task.WhenAny(firstTickReached.Task, Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.That(completedTask, Is.EqualTo(firstTickReached.Task));
            Assert.That(executionCount, Is.GreaterThanOrEqualTo(1));
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
    }
}
