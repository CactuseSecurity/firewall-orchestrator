using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Middleware.Server;
using System.Timers;
using FWO.Services;
using FWO.Test.Helpers;


namespace FWO.Test
{
    [TestFixture]
    internal class SchedulerTest
    {
        public class TestScheduler : SchedulerBase
        {
            /// <summary>
            /// Async Constructor needing the connection
            /// </summary>
            public static async Task<TestScheduler> CreateAsync(ApiConnection apiConnection)
            {
                await DefaultInit.DoNothing();
                SimulatedGlobalConfig globalConfig = new();
                return new TestScheduler(apiConnection, globalConfig);
            }

            private readonly ApiConnection testApiConnection;

            private TestScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
                : base(apiConnection, globalConfig, ConfigQueries.subscribeExternalRequestConfigChanges, SchedulerInterval.Seconds, "Test")
            {
                testApiConnection = apiConnection;
                StartScheduleTimer(1, DateTime.Now);
            }

            private readonly int Counter = 1;

            /// <summary>
            /// set scheduling timer from config values
            /// </summary>
            protected override void OnGlobalConfigChange(List<ConfigItem> config)
            { }

            /// <summary>
            /// define the processing to be done
            /// </summary>
            protected override async void Process(object? _, ElapsedEventArgs __)
            {
                await AlertHelper.AddLogEntry(testApiConnection, 1, "cause", $"logDesc {Counter}", "source");
                await AlertHelper.SetAlert(testApiConnection, "title", $"alertDesc {Counter}", "source", AlertCode.UiError, new AlertHelper.AdditionalAlertData());
            }
        }

        static readonly SchedulerTestApiConn apiConnection = new();

        /// <summary>
        /// The schedule timer is set to one second, so this is long enough for exactly one
        /// recurring tick to have happened.
        /// </summary>
        private static readonly TimeSpan kFirstTickWait = TimeSpan.FromMilliseconds(1500);

        /// <summary>
        /// Time given to a tick that was already running when the scheduler was disposed.
        /// </summary>
        private static readonly TimeSpan kSettleWait = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Spans more than two recurring intervals, so a timer that kept running would be seen.
        /// </summary>
        private static readonly TimeSpan kSilenceAfterDisposeWait = TimeSpan.FromMilliseconds(2500);


        [SetUp]
        public void Initialize()
        {
            //
        }

        [Test]
        [NonParallelizable] // redirects the process wide Console.Out
        public async Task TestTestScheduler()
        {
            TestScheduler? scheduler = null;
            string startupLog = string.Empty;
            int logEntriesAtStart = -1;
            int alertsAtStart = -1;

            try
            {
                // nothing is asserted inside the capture: an assertion failing there would take
                // the captured log - the very thing that explains the failure - down with it
                string output = await ConsoleOutput.CaptureAsync(async capture =>
                {
                    scheduler = await TestScheduler.CreateAsync(apiConnection);
                    logEntriesAtStart = apiConnection.LogEntries.Count;
                    alertsAtStart = apiConnection.Alerts.Count;
                    // snapshot before the first tick, so the start up lines are proven to come
                    // from the constructor rather than from the recurring timer starting later
                    startupLog = capture.Snapshot();

                    await Task.Delay(kFirstTickWait);
                });

                ClassicAssert.AreEqual(0, logEntriesAtStart);
                ClassicAssert.AreEqual(0, alertsAtStart);
                ClassicAssert.IsTrue(startupLog.Contains("Scheduler-Test"));
                ClassicAssert.IsTrue(startupLog.Contains("ScheduleTimer started."));
                ClassicAssert.IsFalse(startupLog.Contains("RecurringTimer started."));

                ClassicAssert.AreEqual(1, apiConnection.LogEntries.Count);
                ClassicAssert.IsTrue(apiConnection.LogEntries[0].Contains("logDesc 1"));
                ClassicAssert.AreEqual(1, apiConnection.Alerts.Count);
                ClassicAssert.IsTrue(apiConnection.Alerts[0].Contains("alertDesc 1"));
                ClassicAssert.AreEqual(1, apiConnection.AcknowledgedAlerts.Count);
                ClassicAssert.AreEqual(7, apiConnection.AcknowledgedAlerts[0]);
                ClassicAssert.IsTrue(output.Contains("RecurringTimer started."));
            }
            finally
            {
                // an undisposed scheduler keeps its recurring timer - and its console logging -
                // running for the rest of the test run and leaks into other tests' captures
                scheduler?.Dispose();
            }
        }

        [Test]
        public async Task DisposeStopsTheRecurringTimer()
        {
            SchedulerTestApiConn disposeApiConnection = new();
            TestScheduler scheduler = await TestScheduler.CreateAsync(disposeApiConnection);

            await Task.Delay(kFirstTickWait);
            ClassicAssert.AreEqual(1, disposeApiConnection.LogEntries.Count, "the recurring timer has to have ticked before disposal is meaningful");

            scheduler.Dispose();
            await Task.Delay(kSettleWait);
            int logEntriesAfterDispose = disposeApiConnection.LogEntries.Count;

            await Task.Delay(kSilenceAfterDisposeWait);

            ClassicAssert.AreEqual(logEntriesAfterDispose, disposeApiConnection.LogEntries.Count);
            ClassicAssert.DoesNotThrow(scheduler.Dispose, "disposing twice has to stay harmless");
        }
    }
}
