using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.File;
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
        public async Task Run_KeepsDeletingExpiredEntriesAfterAnInvalidSource()
        {
            LogDataImportTestApiConn apiConnection = new();
            LogDataImport import = CreateImport(apiConnection, importPath: """["/etc/passwd"]""");

            List<string> failedImports = await import.Run();

            Assert.Multiple(() =>
            {
                Assert.That(failedImports, Has.Count.EqualTo(1), "the rejected source is reported");
                Assert.That(apiConnection.DeleteExpiredCalls, Is.EqualTo(1), "retention still runs");
            });
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
        public async Task SaveEntries_UsesTheStableBatchTimeWhenEntryTimeIsMissing()
        {
            DateTimeOffset importTime = new(2026, 8, 13, 8, 15, 0, TimeSpan.Zero);
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries = new()
            {
                NewSourceEntry("APP-1", 5, "192.0.2.1", "198.51.100.1")
            };

            await InvokeSaveEntries(import, sourceEntries, importTime);

            Assert.That(apiConnection.InsertedEntries.Single().LogTime, Is.EqualTo(importTime));
        }

        [Test]
        [NonParallelizable]
        public async Task AcknowledgeImport_ReportsAFailingScriptWithoutFailingTheImport()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Script execution test requires a Unix-like environment.");
            }

            string tempRoot = Path.Combine(Path.GetTempPath(), $"fwo-log-data-{Guid.NewGuid():N}");
            object? originalConfigData = null;
            object? originalJwtPrivateKey = null;
            object? originalJwtPublicKey = null;
            bool configSnapshotTaken = false;
            try
            {
                Directory.CreateDirectory(tempRoot);
                (originalConfigData, originalJwtPrivateKey, originalJwtPublicKey) = SnapshotConfigFileState();
                configSnapshotTaken = true;
                ConfigureAllowedCustomizationRoots(tempRoot);

                string customizationRoot = Path.Combine(tempRoot, "scripts", "customizing");
                Directory.CreateDirectory(customizationRoot);
                string sourcePath = Path.Combine(customizationRoot, "log-source");
                string scriptPath = sourcePath + ".py";
                File.WriteAllText(scriptPath, "#!/bin/sh\nexit 1\n");
#pragma warning disable CA1416 // Test is skipped on Windows.
                File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416

                LogDataImportTestApiConn apiConnection = new();
                LogDataImport import = CreateImport(apiConnection);
                List<string> importFiles = new() { scriptPath };

                await InvokeAcknowledgeImport(import, scriptPath, importFiles, sourcePath);

                Assert.That(apiConnection.LogEntryDescriptions, Has.Some.Contains("Acknowledging the imported data"),
                    "the failed acknowledgement is reported, the imported data stays imported");
            }
            finally
            {
                if (configSnapshotTaken)
                {
                    RestoreConfigFileState(originalConfigData, originalJwtPrivateKey, originalJwtPublicKey);
                }
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public async Task SaveEntries_KeepsApplicationIdsOfDifferentCaseApart()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries =
            [
                NewSourceEntry("APP-1", 30, "192.0.2.1", "198.51.100.1"),
                NewSourceEntry("app-1", 20, "192.0.2.2", "198.51.100.2")
            ];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertedEntries, Has.Count.EqualTo(1), "the unknown spelling is not imported");
                Assert.That(apiConnection.InsertedEntries.Single().LogCount, Is.EqualTo(30));
                Assert.That(apiConnection.OwnerLookups, Is.EqualTo(2), "both spellings are looked up");
            });
        }

        [Test]
        public async Task SaveEntries_ReportsThatNothingCouldBeImported()
        {
            LogDataImportTestApiConn apiConnection = new();
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries = [NewSourceEntry("UNKNOWN", 30, "192.0.2.1", "198.51.100.1")];

            bool sourceConsumed = await InvokeSaveEntries(import, sourceEntries);

            Assert.That(sourceConsumed, Is.False, "the source files are kept for a second attempt");
        }

        [Test]
        public async Task SaveEntries_ConsumesAnEmptySource()
        {
            LogDataImportTestApiConn apiConnection = new();
            LogDataImport import = CreateImport(apiConnection);

            bool sourceConsumed = await InvokeSaveEntries(import, []);

            Assert.That(sourceConsumed, Is.True, "a source without entries has nothing left to import");
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
        public async Task SaveEntries_ImportsTheValidEntriesBesideAnInvalidOne()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection);
            LogDataImportEntry invalidEntry = NewSourceEntry("APP-1", 40, "no address", "198.51.100.2");
            List<LogDataImportEntry> sourceEntries = [NewSourceEntry("APP-1", 30, "192.0.2.1", "198.51.100.1"), invalidEntry];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertedEntries, Has.Count.EqualTo(1));
                Assert.That(apiConnection.InsertedEntries.Single().LogCount, Is.EqualTo(30));
                Assert.That(apiConnection.CompletedImports, Is.EqualTo(new List<bool> { true }), "the source stays importable");
            });
        }

        [Test]
        public async Task SaveEntries_AssignsTheOwnerOfEachEntryAfterSkippingAnInvalidOne()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            apiConnection.OwnerIdsByAppId["APP-2"] = 22;
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries =
            [
                NewSourceEntry("APP-1", 90, "no address", "198.51.100.1"),
                NewSourceEntry("APP-2", 50, "192.0.2.2", "198.51.100.2"),
                NewSourceEntry("APP-1", 10, "192.0.2.3", "198.51.100.3")
            ];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertedEntries.Single(entry => entry.LogCount == 50).OwnerId, Is.EqualTo(22));
                Assert.That(apiConnection.InsertedEntries.Single(entry => entry.LogCount == 10).OwnerId, Is.EqualTo(11));
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

        private static async Task<bool> InvokeSaveEntries(LogDataImport import, List<LogDataImportEntry> sourceEntries,
            DateTimeOffset? importTime = null)
        {
            MethodInfo method = typeof(LogDataImport).GetMethod("SaveEntries", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(LogDataImport).FullName, "SaveEntries");
            object[] arguments =
            [
                sourceEntries,
                "/usr/local/fworch/scripts/customizing/log_data_import/source",
                importTime ?? DateTimeOffset.UtcNow
            ];
            return await (Task<bool>)method.Invoke(import, arguments)!;
        }

        private static async Task InvokeAcknowledgeImport(LogDataImport import, string scriptPath,
            List<string> importFiles, string sourcePath)
        {
            MethodInfo method = typeof(LogDataImport).GetMethod("AcknowledgeImport", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(LogDataImport).FullName, "AcknowledgeImport");
            object[] arguments = [scriptPath, importFiles, sourcePath];
            await (Task)method.Invoke(import, arguments)!;
        }

        private static void ConfigureAllowedCustomizationRoots(string fwoHome)
        {
            string configFilePath = Path.Combine(fwoHome, "config.json");
            string privateKeyPath = Path.Combine(fwoHome, "private.pem");
            string publicKeyPath = Path.Combine(fwoHome, "public.pem");
            File.WriteAllText(configFilePath, $"{{\"fworch_home\":\"{fwoHome.Replace("\\", "\\\\")}\"}}");
            File.WriteAllText(privateKeyPath, "");
            File.WriteAllText(publicKeyPath, "");
            object?[] configArguments = [configFilePath, privateKeyPath, publicKeyPath];
            TestHelper.InvokeMethod<ConfigFile, object?>("Read", configArguments);
        }

        private static (object? Data, object? JwtPrivateKey, object? JwtPublicKey) SnapshotConfigFileState()
        {
            Type configFileType = typeof(ConfigFile);
            object? data = configFileType.GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            object? jwtPrivateKey = configFileType.GetField("jwtPrivateKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            object? jwtPublicKey = configFileType.GetField("jwtPublicKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            return (data, jwtPrivateKey, jwtPublicKey);
        }

        private static void RestoreConfigFileState(object? data, object? jwtPrivateKey, object? jwtPublicKey)
        {
            Type configFileType = typeof(ConfigFile);
            configFileType.GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, data);
            configFileType.GetField("jwtPrivateKey", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, jwtPrivateKey);
            configFileType.GetField("jwtPublicKey", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, jwtPublicKey);
        }

        private sealed class LogDataImportTestApiConn : SimulatedApiConnection
        {
            // getOwnerId matches app_id_external case sensitively
            public Dictionary<string, int> OwnerIdsByAppId { get; } = new(StringComparer.Ordinal);
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
