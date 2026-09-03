using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Enums;
using FWO.Middleware.Server.Jobs;
using FWO.Ui.Data.Extensions;
using NUnit.Framework;
using Quartz;

namespace FWO.Test
{
    [TestFixture]
    internal class LogDataImportJobTest
    {
        [Test]
        public async Task Execute_RunsTheImportWithoutAlertForAWorkingSource()
        {
            LogDataJobTestApiConn apiConnection = new();
            ImportLogDataJob job = new(apiConnection, new SimulatedGlobalConfig { ImportLogDataPath = "[]" });

            await job.Execute(null!);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.DeleteExpiredCalls, Is.EqualTo(1), "the import ran");
                Assert.That(apiConnection.AlertCalls, Is.Zero);
            });
        }

        [Test]
        public async Task Execute_AlertsWhenTheImportFails()
        {
            LogDataJobTestApiConn apiConnection = new();
            ImportLogDataJob job = new(apiConnection, new SimulatedGlobalConfig { ImportLogDataPath = "no json" });

            await job.Execute(null!);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.DeleteExpiredCalls, Is.Zero);
                Assert.That(apiConnection.AlertCalls, Is.GreaterThan(0), "a failing import is alerted");
            });
        }

        [Test]
        public void LogDataImportIntervalUnit_IsDisplayedWithItsLocalizedName()
        {
            SimulatedUserConfig userConfig = new();

            Assert.Multiple(() =>
            {
                Assert.That(LogDataImportIntervalUnit.Seconds.ToString(userConfig), Is.EqualTo(userConfig.GetText("Seconds")));
                Assert.That(LogDataImportIntervalUnit.Minutes.ToString(userConfig), Is.EqualTo(userConfig.GetText("Minutes2")));
                Assert.That(LogDataImportIntervalUnit.Hours.ToString(userConfig), Is.EqualTo(userConfig.GetText("Hours")));
            });
        }

        [Test]
        public void LogDataImportIntervalUnit_RequiresAUserConfig()
        {
            Assert.That(() => LogDataImportIntervalUnit.Hours.ToString((UserConfig)null!),
                Throws.InstanceOf<ArgumentNullException>());
        }

        private sealed class LogDataJobTestApiConn : SimulatedApiConnection
        {
            public int DeleteExpiredCalls { get; private set; }
            public int AlertCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == LogDataQueries.deleteExpiredLogEntries)
                {
                    DeleteExpiredCalls++;
                }
                if (query == MonitorQueries.addAlert)
                {
                    AlertCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }
                if (query == MonitorQueries.addDataImportLogEntry || query == MonitorQueries.addLogEntry)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }
                return Task.FromResult(default(QueryResponseType)!);
            }
        }
    }
}
