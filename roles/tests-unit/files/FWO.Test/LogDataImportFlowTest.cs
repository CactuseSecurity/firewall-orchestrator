using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the import flow of the log data import which talks to the API,
    /// the pure conversion helpers are tested in <see cref="LogDataImportTest"/>.
    /// </summary>
    [TestFixture]
    internal class LogDataImportFlowTest
    {
        [Test]
        public async Task Run_DeletesExpiredEntriesWithoutConfiguredSources()
        {
            LogDataImportTestApiConn apiConnection = new();
            LogDataImport import = CreateImport(apiConnection, retentionDays: 30);

            List<string> failedImports = await import.Run();

            Assert.Multiple(() =>
            {
                Assert.That(failedImports, Is.Empty);
                Assert.That(apiConnection.DeleteExpiredCalls, Is.EqualTo(1));
                Assert.That(apiConnection.LastExpiryTime, Is.Not.Null);
                Assert.That(apiConnection.LastExpiryTime!.Value, Is.EqualTo(DateTimeOffset.UtcNow.AddDays(-30)).Within(TimeSpan.FromMinutes(1)));
            });
        }

        [Test]
        public void Run_ThrowsForUnreadableSourceConfiguration()
        {
            LogDataImportTestApiConn apiConnection = new();
            LogDataImport import = CreateImport(apiConnection, importPath: "not a json list");

            Assert.That(async () => await import.Run(), Throws.Exception);
        }

        [Test]
        public async Task Run_ClampsNegativeRetentionToTheCurrentTime()
        {
            LogDataImportTestApiConn apiConnection = new();
            LogDataImport import = CreateImport(apiConnection, retentionDays: -5);

            await import.Run();

            Assert.That(apiConnection.LastExpiryTime!.Value, Is.EqualTo(DateTimeOffset.UtcNow).Within(TimeSpan.FromMinutes(1)));
        }

        [Test]
        public async Task SaveEntries_ResolvesOwnersAndInsertsTheEntries()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries =
            [
                NewSourceEntry("APP-1", 5, "192.0.2.1", "198.51.100.1"),
                NewSourceEntry("APP-1", 90, "192.0.2.2", "198.51.100.2")
            ];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertedEntries, Has.Count.EqualTo(2));
                Assert.That(apiConnection.InsertedEntries.Select(entry => entry.OwnerId), Is.All.EqualTo(11));
                Assert.That(apiConnection.InsertedEntries.First().LogCount, Is.EqualTo(90), "entries are ordered by log count");
                Assert.That(apiConnection.CompletedImports, Is.EqualTo(new List<bool> { true }));
                Assert.That(apiConnection.CreateImportControlCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task SaveEntries_QueriesEachApplicationIdOnlyOnce()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries =
            [
                NewSourceEntry("APP-1", 30, "192.0.2.1", "198.51.100.1"),
                NewSourceEntry("APP-1", 20, "192.0.2.2", "198.51.100.2"),
                NewSourceEntry("APP-1", 10, "192.0.2.3", "198.51.100.3")
            ];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.That(apiConnection.OwnerLookups, Is.EqualTo(1));
        }

        [Test]
        public async Task SaveEntries_SkipsEntriesOfUnknownApplications()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries =
            [
                NewSourceEntry("APP-1", 30, "192.0.2.1", "198.51.100.1"),
                NewSourceEntry("UNKNOWN", 20, "192.0.2.2", "198.51.100.2")
            ];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertedEntries, Has.Count.EqualTo(1));
                Assert.That(apiConnection.InsertedEntries.Single().OwnerId, Is.EqualTo(11));
            });
        }

        [Test]
        public async Task SaveEntries_WritesNoImportWithoutResolvableEntries()
        {
            LogDataImportTestApiConn apiConnection = new();
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries = [NewSourceEntry("UNKNOWN", 30, "192.0.2.1", "198.51.100.1")];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.CreateImportControlCalls, Is.Zero);
                Assert.That(apiConnection.InsertedEntries, Is.Empty);
                Assert.That(apiConnection.LogEntryDescriptions, Has.Some.Contains("No valid log entries"));
            });
        }

        [Test]
        public async Task SaveEntries_KeepsOnlyTheConfiguredNumberOfEntries()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection, maxEntries: 2);
            List<LogDataImportEntry> sourceEntries =
            [
                NewSourceEntry("APP-1", 10, "192.0.2.1", "198.51.100.1"),
                NewSourceEntry("APP-1", 90, "192.0.2.2", "198.51.100.2"),
                NewSourceEntry("APP-1", 50, "192.0.2.3", "198.51.100.3")
            ];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertedEntries.Select(entry => entry.LogCount), Is.EqualTo(new List<int> { 90, 50 }));
                Assert.That(apiConnection.LogEntryDescriptions, Has.Some.Contains("discarded 1 entries"));
            });
        }

        [Test]
        public async Task SaveEntries_MergesRepeatedFlowsAndReportsThem()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries =
            [
                NewSourceEntry("APP-1", 30, "192.0.2.1", "198.51.100.1"),
                NewSourceEntry("APP-1", 20, "192.0.2.1", "198.51.100.1")
            ];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertedEntries, Has.Count.EqualTo(1));
                Assert.That(apiConnection.InsertedEntries.Single().LogCount, Is.EqualTo(50));
                Assert.That(apiConnection.LogEntryDescriptions, Has.Some.Contains("merged 1 repeated"));
            });
        }

        [Test]
        public async Task SaveEntries_MarksTheImportAsFailedWhenTheInsertFails()
        {
            LogDataImportTestApiConn apiConnection = new() { FailInsert = true };
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries = [NewSourceEntry("APP-1", 30, "192.0.2.1", "198.51.100.1")];

            Assert.That(async () => await InvokeSaveEntries(import, sourceEntries), Throws.Exception);
            Assert.That(apiConnection.CompletedImports, Is.EqualTo(new List<bool> { false }));
            await Task.CompletedTask;
        }

        [Test]
        public void SaveEntries_FailsWhenNoImportControlIsCreated()
        {
            LogDataImportTestApiConn apiConnection = new() { CreateEmptyImportControl = true };
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries = [NewSourceEntry("APP-1", 30, "192.0.2.1", "198.51.100.1")];

            Assert.That(async () => await InvokeSaveEntries(import, sourceEntries), Throws.InstanceOf<InvalidOperationException>());
        }

        private static LogDataImport CreateImport(ApiConnection apiConnection, string importPath = "[]",
            int maxEntries = 1000, int retentionDays = 90)
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ImportLogDataPath = importPath,
                ImportLogDataMaxEntries = maxEntries,
                LogDataRetentionDays = retentionDays
            };
            return new LogDataImport(apiConnection, globalConfig);
        }

        private static LogDataImportEntry NewSourceEntry(string appId, int logCount, string source, string destination)
        {
            return new LogDataImportEntry
            {
                AppId = appId,
                LogCount = logCount,
                Source = source,
                Destination = destination,
                Protocol = 6,
                Port = 443,
                Action = "accept"
            };
        }

        private static async Task InvokeSaveEntries(LogDataImport import, List<LogDataImportEntry> sourceEntries)
        {
            MethodInfo method = typeof(LogDataImport).GetMethod("SaveEntries", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(LogDataImport).FullName, "SaveEntries");
            object[] arguments = [sourceEntries, "/usr/local/fworch/scripts/customizing/log_data_import/source"];
            await (Task)method.Invoke(import, arguments)!;
        }

        private sealed class LogDataImportTestApiConn : SimulatedApiConnection
        {
            public Dictionary<string, int> OwnerIdsByAppId { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<FirewallLogEntryInput> InsertedEntries { get; } = [];
            public List<bool> CompletedImports { get; } = [];
            public List<string> LogEntryDescriptions { get; } = [];
            public int DeleteExpiredCalls { get; private set; }
            public int CreateImportControlCalls { get; private set; }
            public int OwnerLookups { get; private set; }
            public DateTimeOffset? LastExpiryTime { get; private set; }
            public bool FailInsert { get; init; }
            public bool CreateEmptyImportControl { get; init; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == OwnerQueries.getOwnerId)
                {
                    return Task.FromResult((QueryResponseType)(object)LookupOwner(variables));
                }
                if (query == ImportQueries.addImportForLog)
                {
                    CreateImportControlCalls++;
                    return Task.FromResult((QueryResponseType)(object)CreateImportControl());
                }
                if (query == ImportQueries.completeLogImport)
                {
                    CompletedImports.Add(GetVariable<bool>(variables, "successful"));
                    return Task.FromResult(default(QueryResponseType)!);
                }
                if (query == LogDataQueries.insertLogEntries)
                {
                    return Task.FromResult((QueryResponseType)(object)InsertEntries(variables)!);
                }
                if (query == LogDataQueries.deleteExpiredLogEntries)
                {
                    DeleteExpiredCalls++;
                    LastExpiryTime = GetVariable<DateTimeOffset>(variables, "expiryTime");
                    return Task.FromResult(default(QueryResponseType)!);
                }
                if (query == MonitorQueries.addDataImportLogEntry)
                {
                    LogEntryDescriptions.Add(GetVariable<string>(variables, "description") ?? "");
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }
                return Task.FromResult(default(QueryResponseType)!);
            }

            private List<OwnerIdModel> LookupOwner(object? variables)
            {
                OwnerLookups++;
                string appId = GetVariable<string>(variables, "externalAppId") ?? "";
                return OwnerIdsByAppId.TryGetValue(appId, out int ownerId) ? [new OwnerIdModel { Id = ownerId }] : [];
            }

            private InsertImportControl CreateImportControl()
            {
                return CreateEmptyImportControl
                    ? new InsertImportControl()
                    : new InsertImportControl { Returning = [new ImportControl { ControlId = 4711 }] };
            }

            private object InsertEntries(object? variables)
            {
                if (FailInsert)
                {
                    throw new InvalidOperationException("insert failed");
                }
                InsertedEntries.AddRange(GetVariable<List<FirewallLogEntryInput>>(variables, "entries") ?? []);
                return new object();
            }

            private static T? GetVariable<T>(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
                return value is null ? default : (T)value;
            }
        }
    }
}
