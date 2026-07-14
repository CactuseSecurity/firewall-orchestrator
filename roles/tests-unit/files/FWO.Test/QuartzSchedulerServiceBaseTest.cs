using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Middleware.Server.Services;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Quartz;
using System.Globalization;

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

        private sealed class SubscriptionDrivenTestSchedulerService : QuartzSchedulerServiceBase<TestJob>
        {
            public SubscriptionDrivenTestSchedulerService(
                ISchedulerFactory schedulerFactory,
                ApiConnection apiConnection,
                GlobalConfig globalConfig,
                IHostApplicationLifetime appLifetime)
                : base(
                    schedulerFactory,
                    apiConnection,
                    globalConfig,
                    appLifetime,
                    new QuartzSchedulerOptions(
                        "TestScheduler",
                        "TestJob",
                        "TestTrigger",
                        ConfigQueries.subscribeExternalRequestConfigChanges))
            { }

            protected override int SleepTime => globalConfig.ExternalRequestSleepTime;

            protected override DateTime StartAt => globalConfig.ExternalRequestStartAt;

            protected override TimeSpan Interval => TimeSpan.FromSeconds(globalConfig.ExternalRequestSleepTime);
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

        [Test]
        public async Task ConfigEmissionWithUnchangedSchedule_DoesNotRescheduleQuartzJob()
        {
            IScheduler scheduler = Substitute.For<IScheduler>();
            await ConfigureQuartzScheduler(scheduler);
            ISchedulerFactory schedulerFactory = Substitute.For<ISchedulerFactory>();
            schedulerFactory.GetScheduler().Returns(Task.FromResult(scheduler));
            CapturingApiConnection apiConnection = new();
            using TestApplicationLifetime appLifetime = new();

            _ = new SubscriptionDrivenTestSchedulerService(schedulerFactory, apiConnection, new SimulatedGlobalConfig(), appLifetime);
            appLifetime.Start();
            await WaitUntil(() => apiConnection.ConfigUpdateHandler != null);

            DateTime firstStart = new(2026, 1, 1, 12, 0, 0);
            apiConnection.Emit(CreateExternalRequestConfig(60, firstStart));
            await WaitUntil(async () => await ScheduleTriggerCallCount(scheduler) == 1);

            apiConnection.Emit(CreateExternalRequestConfig(60, firstStart));
            await Task.Delay(100);
            Assert.That(await ScheduleTriggerCallCount(scheduler), Is.EqualTo(1));

            apiConnection.Emit(CreateExternalRequestConfig(90, firstStart));
            await WaitUntil(async () => await ScheduleTriggerCallCount(scheduler) == 2);
        }

        [Test]
        public async Task DailyCheckConfigEmissionWithUnchangedSchedule_DoesNotRecreateQuartzJob()
        {
            IScheduler scheduler = Substitute.For<IScheduler>();
            await ConfigureQuartzScheduler(scheduler);
            ISchedulerFactory schedulerFactory = Substitute.For<ISchedulerFactory>();
            schedulerFactory.GetScheduler().Returns(Task.FromResult(scheduler));
            CapturingApiConnection apiConnection = new();
            using TestApplicationLifetime appLifetime = new();

            _ = new DailyCheckSchedulerService(schedulerFactory, apiConnection, new SimulatedGlobalConfig(), appLifetime);
            appLifetime.Start();
            await WaitUntil(() => apiConnection.ConfigUpdateHandler != null);

            DateTime firstStart = new(2026, 1, 1, 3, 0, 0);
            apiConnection.Emit(CreateDailyCheckConfig(firstStart));
            await WaitUntil(async () => await ScheduleJobAndTriggerCallCount(scheduler) == 1);

            apiConnection.Emit(CreateDailyCheckConfig(firstStart));
            await Task.Delay(100);
            Assert.That(await ScheduleJobAndTriggerCallCount(scheduler), Is.EqualTo(1));

            apiConnection.Emit(CreateDailyCheckConfig(firstStart.AddHours(1)));
            await WaitUntil(async () => await ScheduleJobAndTriggerCallCount(scheduler) == 2);
        }

        private static Task ConfigureQuartzScheduler(IScheduler scheduler)
        {
            scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
            scheduler.AddJob(Arg.Any<IJobDetail>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            scheduler.UnscheduleJob(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
            scheduler.DeleteJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            scheduler.ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(DateTimeOffset.Now));
            scheduler.ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(DateTimeOffset.Now));
            return Task.CompletedTask;
        }

        private static async Task<int> ScheduleTriggerCallCount(IScheduler scheduler)
        {
            try
            {
                await scheduler.Received().ScheduleJob(Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
                return scheduler.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IScheduler.ScheduleJob)
                    && call.GetArguments().Length > 0
                    && call.GetArguments()[0] is ITrigger);
            }
            catch
            {
                return 0;
            }
        }

        private static async Task<int> ScheduleJobAndTriggerCallCount(IScheduler scheduler)
        {
            try
            {
                await scheduler.Received().ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
                return scheduler.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IScheduler.ScheduleJob)
                    && call.GetArguments().Length > 1
                    && call.GetArguments()[0] is IJobDetail
                    && call.GetArguments()[1] is ITrigger);
            }
            catch
            {
                return 0;
            }
        }

        private static List<ConfigItem> CreateExternalRequestConfig(int sleepTime, DateTime startAt)
        {
            return
            [
                new() { Key = "externalRequestSleepTime", Value = sleepTime.ToString(CultureInfo.InvariantCulture) },
                new() { Key = "externalRequestStartAt", Value = startAt.ToString("O", CultureInfo.InvariantCulture) }
            ];
        }

        private static List<ConfigItem> CreateDailyCheckConfig(DateTime startAt)
        {
            return
            [
                new() { Key = "dailyCheckStartAt", Value = startAt.ToString("O", CultureInfo.InvariantCulture) }
            ];
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            await WaitUntil(() => Task.FromResult(condition()));
        }

        private static async Task WaitUntil(Func<Task<bool>> condition)
        {
            for (int i = 0; i < 50; i++)
            {
                if (await condition())
                {
                    return;
                }
                await Task.Delay(20);
            }

            Assert.Fail("Condition was not met in time.");
        }

        private sealed class CapturingApiConnection : SimulatedApiConnection
        {
            public GraphQlApiSubscription<List<ConfigItem>>.SubscriptionUpdate? ConfigUpdateHandler { get; private set; }

            public void Emit(List<ConfigItem> configItems)
            {
                ConfigUpdateHandler?.Invoke(configItems);
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(
                Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler,
                string subscription,
                object? variables = null,
                string? operationName = null)
            {
                if (typeof(SubscriptionResponseType) == typeof(List<ConfigItem>))
                {
                    ConfigUpdateHandler = (GraphQlApiSubscription<List<ConfigItem>>.SubscriptionUpdate)(object)subscriptionUpdateHandler;
                }

                return new SimulatedApiSubscription<SubscriptionResponseType>(
                    this,
                    new GraphQLHttpClient(new GraphQLHttpClientOptions(), new SystemTextJsonSerializer(), new()),
                    new GraphQLRequest(subscription, variables, operationName),
                    exceptionHandler,
                    subscriptionUpdateHandler);
            }
        }

        private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
        {
            private readonly CancellationTokenSource started = new();
            private readonly CancellationTokenSource stopping = new();
            private readonly CancellationTokenSource stopped = new();

            public CancellationToken ApplicationStarted => started.Token;

            public CancellationToken ApplicationStopping => stopping.Token;

            public CancellationToken ApplicationStopped => stopped.Token;

            public void Start()
            {
                started.Cancel();
            }

            public void StopApplication()
            {
                stopping.Cancel();
                stopped.Cancel();
            }

            public void Dispose()
            {
                started.Dispose();
                stopping.Dispose();
                stopped.Dispose();
            }
        }
    }
}
