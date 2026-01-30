using FWO.Middleware.Server.Services;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Quartz;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class QuartzSchedulerServiceBaseTest
    {
        private sealed class TestJob : IJob
        {
            public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
        }

        private sealed class TestSchedulerService : QuartzSchedulerServiceBase<TestJob>
        {
            private TestSchedulerService()
                : base(
                    null!,
                    null!,
                    null!,
                    null!,
                    new QuartzSchedulerOptions("Test", "Test", "Test", "Test"))
            { }

            protected override int SleepTime => 1;

            protected override DateTime StartAt => DateTime.MinValue;

            protected override TimeSpan Interval => TimeSpan.FromSeconds(1);

            public static DateTimeOffset CalculateStartTimeForTest(DateTime configuredStartTime, TimeSpan interval, DateTime now)
            {
                return CalculateStartTime(configuredStartTime, interval, now);
            }
        }

        [Test]
        public void CalculateStartTime_ReturnsFutureStartTime()
        {
            DateTime now = new(2024, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
            DateTime configuredStartTime = now.AddMinutes(-12);
            TimeSpan interval = TimeSpan.FromMinutes(5);

            DateTimeOffset result = TestSchedulerService.CalculateStartTimeForTest(configuredStartTime, interval, now);

            DateTimeOffset expected = new(now.AddMinutes(3));
            ClassicAssert.AreEqual(expected, result);
        }

        [Test]
        public void CalculateStartTime_PreservesFutureStartTime()
        {
            DateTime now = new(2024, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
            DateTime configuredStartTime = now.AddMinutes(10);
            TimeSpan interval = TimeSpan.FromMinutes(5);

            DateTimeOffset result = TestSchedulerService.CalculateStartTimeForTest(configuredStartTime, interval, now);

            ClassicAssert.AreEqual(new DateTimeOffset(configuredStartTime), result);
        }

        [Test]
        public void CalculateStartTime_ThrowsOnNonPositiveInterval()
        {
            DateTime now = new(2024, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
            DateTime configuredStartTime = now;

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TestSchedulerService.CalculateStartTimeForTest(configuredStartTime, TimeSpan.Zero, now));
        }
    }
}
