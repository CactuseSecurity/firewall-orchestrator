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
        
            private TestScheduler(ApiConnection apiConnection, GlobalConfig globalConfig)
                : base(apiConnection, globalConfig, ConfigQueries.subscribeExternalRequestConfigChanges, SchedulerInterval.Seconds, "Test")
            {
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
                await AddLogEntry(1, "cause", $"logDesc {Counter}", "source");
                await SetAlert("title", $"alertDesc {Counter}", "source", AlertCode.UiError);
            }
        }

        static readonly SchedulerTestApiConn apiConnection = new();


        [SetUp]
        public void Initialize()
        {}

        [Test]
        public async Task TestTestScheduler()
        {
            List<string> ConsoleLogs = [];
            using StringWriter logOutput = new();
            TextWriter originalConsoleOut = Console.Out;
            Console.SetOut(logOutput);

            await TestScheduler.CreateAsync(apiConnection);

            ClassicAssert.AreEqual(0, apiConnection.LogEntries.Count);
            ClassicAssert.AreEqual(0, apiConnection.Alerts.Count);
            ConsoleLogs.Add(logOutput.ToString());
            ClassicAssert.AreEqual(true, ConsoleLogs[0].Contains("Scheduler-Test"));
            ClassicAssert.AreEqual(true, ConsoleLogs[0].Contains("ScheduleTimer started."));

            Thread.Sleep(1500);
            ClassicAssert.AreEqual(1, apiConnection.LogEntries.Count);
            ClassicAssert.AreEqual(true, apiConnection.LogEntries[0].Contains("logDesc 1"));
            ClassicAssert.AreEqual(1, apiConnection.Alerts.Count);
            ClassicAssert.AreEqual(true, apiConnection.Alerts[0].Contains("alertDesc 1"));
            ConsoleLogs.Add(logOutput.ToString());
            ClassicAssert.AreEqual(true, ConsoleLogs[1].Contains("RecurringTimer started."));
            Console.SetOut(originalConsoleOut);
        }
    }
}
