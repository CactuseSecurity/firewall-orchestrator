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
    }
}
