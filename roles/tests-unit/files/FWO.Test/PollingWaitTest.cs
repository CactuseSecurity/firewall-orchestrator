using FWO.Test.Helpers;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class PollingWaitTest
    {
        private static readonly TimeSpan kShortTimeout = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan kExpiredTimeout = TimeSpan.Zero;

        /// <summary>
        /// Verifies that an already satisfied synchronous condition returns immediately.
        /// </summary>
        [Test]
        public async Task UntilAsync_SynchronousConditionAlreadyTrue_ReturnsTrue()
        {
            int invocationCount = 0;

            bool result = await PollingWait.UntilAsync(() =>
            {
                invocationCount++;
                return true;
            }, kShortTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(invocationCount, Is.EqualTo(1));
            });
        }

        /// <summary>
        /// Verifies that polling continues until a synchronous condition becomes true.
        /// </summary>
        [Test]
        public async Task UntilAsync_SynchronousConditionBecomesTrue_ReturnsTrue()
        {
            int invocationCount = 0;

            bool result = await PollingWait.UntilAsync(() => ++invocationCount == 2, kShortTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(invocationCount, Is.EqualTo(2));
            });
        }

        /// <summary>
        /// Verifies that an expired timeout evaluates the condition once before returning false.
        /// </summary>
        [Test]
        public async Task UntilAsync_TimeoutExpired_ReturnsFalseAfterFinalEvaluation()
        {
            int invocationCount = 0;

            bool result = await PollingWait.UntilAsync(() =>
            {
                invocationCount++;
                return false;
            }, kExpiredTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(invocationCount, Is.EqualTo(1));
            });
        }

        /// <summary>
        /// Verifies that the asynchronous overload awaits and returns its condition result.
        /// </summary>
        [Test]
        public async Task UntilAsync_AsynchronousCondition_ReturnsTrue()
        {
            int invocationCount = 0;

            bool result = await PollingWait.UntilAsync(async () =>
            {
                await Task.Yield();
                invocationCount++;
                return true;
            }, kShortTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(invocationCount, Is.EqualTo(1));
            });
        }
    }
}
