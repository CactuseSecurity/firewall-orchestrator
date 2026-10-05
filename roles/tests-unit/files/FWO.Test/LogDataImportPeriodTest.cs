using System.Reflection;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Config.File;
using FWO.Data;
using FWO.Middleware.Server;
using FWO.Test.Helpers;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the log time range every log data import file has to name and the import period the
    /// middleware stores once for the whole log data.
    /// </summary>
    [TestFixture]
    internal class LogDataImportPeriodTest
    {
        private const string kSourceName = "log-source";
        private const int kOneHour = 3600;
        private const int kOneDay = 86400;
        private static readonly DateTimeOffset kImportTime = new(2026, 10, 5, 8, 30, 0, TimeSpan.Zero);

        [TestCase(0)]
        [TestCase(-1)]
        public void ResolveLogTimeRange_RejectsANonPositiveRangeOfTheFile(int logTimeRange)
        {
            Assert.Throws<InvalidDataException>(() => LogDataImport.ResolveLogTimeRange(logTimeRange, kOneDay));
        }

        [Test]
        public void ResolveLogTimeRange_PrefersTheRangeOfTheFileOverTheDefault()
        {
            Assert.That(LogDataImport.ResolveLogTimeRange(kOneHour, kOneDay), Is.EqualTo(kOneHour));
        }

        [Test]
        public void ResolveLogTimeRange_UsesTheDefaultForAFileWithoutRange()
        {
            Assert.That(LogDataImport.ResolveLogTimeRange(null, kOneDay), Is.EqualTo(kOneDay));
        }

        [Test]
        public void ResolveLogTimeRange_RaisesANonPositiveDefaultToOneSecond()
        {
            Assert.That(LogDataImport.ResolveLogTimeRange(null, 0), Is.EqualTo(1));
        }

        [Test]
        public void FindEntriesOutsideTimeRange_ReturnsEntriesBeforeTheRangeAndAfterTheImport()
        {
            LogDataImportEntry tooOld = NewEntry(kImportTime.AddSeconds(-kOneHour - 1));
            LogDataImportEntry rangeStart = NewEntry(kImportTime.AddSeconds(-kOneHour));
            LogDataImportEntry atImport = NewEntry(kImportTime);
            LogDataImportEntry afterImport = NewEntry(kImportTime.AddSeconds(1));
            LogDataImportEntry withoutLogTime = NewEntry(null);
            List<LogDataImportEntry> entries = [tooOld, rangeStart, atImport, afterImport, withoutLogTime];

            List<LogDataImportEntry> outsideEntries = LogDataImport.FindEntriesOutsideTimeRange(entries, kImportTime, kOneHour);

            List<LogDataImportEntry> expectedEntries = [tooOld, afterImport];
            Assert.That(outsideEntries, Is.EqualTo(expectedEntries), "the range includes its start and the import time");
        }

        [TestCase(1)]
        [TestCase(60)]
        public void WarnAboutEntriesOutsideTimeRange_ReportsWithoutFailing(int outsideEntryCount)
        {
            List<LogDataImportEntry> entries = [.. Enumerable.Range(0, outsideEntryCount).Select(_ => NewEntry(kImportTime.AddDays(-2)))];
            MethodInfo method = typeof(LogDataImport).GetMethod("WarnAboutEntriesOutsideTimeRange", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(LogDataImport).FullName, "WarnAboutEntriesOutsideTimeRange");
            object[] arguments = [entries, kImportTime, kOneHour, "/usr/local/fworch/scripts/customizing/log_data_import/source"];

            Assert.DoesNotThrow(() => method.Invoke(null, arguments));
        }

        [Test]
        [NonParallelizable]
        public async Task Run_ImportsEntriesLoggedOutsideTheTimeRange()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Import source test requires a Unix-like environment.");
            }
            PeriodTestApiConn apiConnection = new();

            List<string> failedImports = await RunWithTemporarySource(apiConnection, kOneDay, sourcePath => File.WriteAllText(sourcePath + ".json",
                """{"import_time": "2026-10-05T08:30:00+00:00", "log_time_range_in_seconds": 3600, "logs": [{"app_id": "APP-1", "log_count": 1, "source": "192.0.2.1", "destination": "198.51.100.1", "log_time": "2026-10-01T08:30:00+00:00"}]}"""));

            Assert.That(failedImports, Is.Empty, "a wrongly dated entry is reported, not rejected");
        }

        [Test]
        public void ImportFile_ReadsTheTopLevelLogTimeRange()
        {
            LogDataImportFile? importFile = JsonSerializer.Deserialize<LogDataImportFile>(
                """{"log_time_range_in_seconds": 604800, "logs": []}""");

            Assert.That(importFile?.LogTimeRangeInSeconds, Is.EqualTo(GlobalConst.kDefaultLogTimeRangeInSeconds));
        }

        [Test]
        public async Task StoreImportPeriod_StoresThePeriodForTheWholeLogData()
        {
            PeriodTestApiConn apiConnection = new();

            await InvokeStoreImportPeriod(CreateImport(apiConnection), new LogDataImportPeriod { LogTimeRangeInSeconds = kOneHour, ImportTime = kImportTime });

            LogDataImportPeriod? storedPeriod = LogDataImportPeriod.Parse(apiConnection.StoredValue);
            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.StoredKey, Is.EqualTo(LogDataImportPeriod.kConfigKey));
                Assert.That(apiConnection.StoredUser, Is.Zero, "the period belongs to the whole log data, not to a user");
                Assert.That(storedPeriod?.LogTimeRangeInSeconds, Is.EqualTo(kOneHour));
                Assert.That(storedPeriod?.ImportTime, Is.EqualTo(kImportTime));
            });
        }

        [Test]
        public async Task StoreImportPeriod_StoresADifferingRangeAsTheCurrentOne()
        {
            PeriodTestApiConn apiConnection = new()
            {
                StoredValue = JsonSerializer.Serialize(new LogDataImportPeriod { LogTimeRangeInSeconds = kOneDay, ImportTime = kImportTime })
            };

            await InvokeStoreImportPeriod(CreateImport(apiConnection), new LogDataImportPeriod { LogTimeRangeInSeconds = kOneHour, ImportTime = kImportTime });

            Assert.That(LogDataImportPeriod.Parse(apiConnection.StoredValue)?.LogTimeRangeInSeconds, Is.EqualTo(kOneHour),
                "the entries of the source are the current ones, so is their period");
        }

        [Test]
        public void StoreImportPeriod_DoesNotFailTheImportWhenThePeriodCannotBeStored()
        {
            PeriodTestApiConn apiConnection = new() { FailUpsert = true };

            Assert.DoesNotThrowAsync(() => InvokeStoreImportPeriod(CreateImport(apiConnection),
                new LogDataImportPeriod { LogTimeRangeInSeconds = kOneHour, ImportTime = kImportTime }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("""{"log_time_range_in_seconds": 0}""")]
        public void Parse_ReturnsNullForAMissingOrUnusablePeriod(string? configValue)
        {
            Assert.That(LogDataImportPeriod.Parse(configValue), Is.Null);
        }

        [Test]
        [NonParallelizable]
        public async Task Run_StoresTheDefaultLogTimeRangeForAnImportFileWithoutRange()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Import source test requires a Unix-like environment.");
            }
            PeriodTestApiConn apiConnection = new();

            List<string> failedImports = await RunWithTemporarySource(apiConnection, kOneDay,
                sourcePath => File.WriteAllText(sourcePath + ".json", """{"logs": []}"""));

            Assert.Multiple(() =>
            {
                Assert.That(failedImports, Is.Empty);
                Assert.That(LogDataImportPeriod.Parse(apiConnection.StoredValue)?.LogTimeRangeInSeconds, Is.EqualTo(kOneDay));
            });
        }

        [Test]
        [NonParallelizable]
        public async Task Run_RejectsAnImportFileWithANonPositiveRange()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Import source test requires a Unix-like environment.");
            }
            PeriodTestApiConn apiConnection = new();

            List<string> failedImports = await RunWithTemporarySource(apiConnection, kOneDay,
                sourcePath => File.WriteAllText(sourcePath + ".json", """{"log_time_range_in_seconds": 0, "logs": []}"""));

            Assert.Multiple(() =>
            {
                Assert.That(failedImports, Has.Count.EqualTo(1));
                Assert.That(apiConnection.StoredValue, Is.Null);
            });
        }

        [Test]
        [NonParallelizable]
        public async Task Run_StoresTheLogTimeRangeOfTheImportFile()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Import source test requires a Unix-like environment.");
            }
            PeriodTestApiConn apiConnection = new();

            List<string> failedImports = await RunWithTemporarySource(apiConnection, kOneDay, sourcePath => File.WriteAllText(sourcePath + ".json",
                """{"import_time": "2026-10-05T08:30:00+00:00", "log_time_range_in_seconds": 3600, "logs": []}"""));

            LogDataImportPeriod? storedPeriod = LogDataImportPeriod.Parse(apiConnection.StoredValue);
            Assert.Multiple(() =>
            {
                Assert.That(failedImports, Is.Empty);
                Assert.That(storedPeriod?.LogTimeRangeInSeconds, Is.EqualTo(kOneHour), "the range of the file overrides the default");
                Assert.That(storedPeriod?.ImportTime, Is.EqualTo(kImportTime));
            });
        }

        /// <summary>
        /// Runs the import against one source in a temporary customization root.
        /// </summary>
        /// <param name="apiConnection">API the import talks to.</param>
        /// <param name="defaultLogTimeRange">Configured default log time range.</param>
        /// <param name="prepareSource">Writes the source files, given the extensionless source path.</param>
        private static async Task<List<string>> RunWithTemporarySource(PeriodTestApiConn apiConnection, int defaultLogTimeRange,
            Action<string> prepareSource)
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"fwo-log-period-{Guid.NewGuid():N}");
            (object? Data, object? PrivateKey, object? PublicKey)? snapshot = null;
            try
            {
                Directory.CreateDirectory(tempRoot);
                snapshot = SnapshotConfigFileState();
                ConfigureAllowedCustomizationRoots(tempRoot);
                string customizationRoot = Path.Combine(tempRoot, "scripts", "customizing");
                Directory.CreateDirectory(customizationRoot);
                string sourcePath = Path.Combine(customizationRoot, kSourceName);
                prepareSource(sourcePath);
                LogDataImport import = CreateImport(apiConnection, JsonSerializer.Serialize(new List<string> { sourcePath }), defaultLogTimeRange);
                return await import.Run();
            }
            finally
            {
                if (snapshot is not null)
                {
                    RestoreConfigFileState(snapshot.Value.Data, snapshot.Value.PrivateKey, snapshot.Value.PublicKey);
                }
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static LogDataImportEntry NewEntry(DateTimeOffset? logTime)
        {
            return new LogDataImportEntry { AppId = "APP-1", LogCount = 1, Source = "192.0.2.1", Destination = "198.51.100.1", LogTime = logTime };
        }

        private static LogDataImport CreateImport(ApiConnection apiConnection, string importPath = "[]",
            int defaultLogTimeRange = GlobalConst.kDefaultLogTimeRangeInSeconds)
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ImportLogDataPath = importPath,
                DefaultLogTimeRangeInSeconds = defaultLogTimeRange
            };
            return new LogDataImport(apiConnection, globalConfig, _ => Task.FromResult(""));
        }

        private static async Task InvokeStoreImportPeriod(LogDataImport import, LogDataImportPeriod period)
        {
            MethodInfo method = typeof(LogDataImport).GetMethod("StoreImportPeriod", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(LogDataImport).FullName, "StoreImportPeriod");
            object[] arguments = [period, "/usr/local/fworch/scripts/customizing/log_data_import/source"];
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

        private static (object? Data, object? PrivateKey, object? PublicKey) SnapshotConfigFileState()
        {
            Type configFileType = typeof(ConfigFile);
            object? data = configFileType.GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            object? privateKey = configFileType.GetField("jwtPrivateKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            object? publicKey = configFileType.GetField("jwtPublicKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            return (data, privateKey, publicKey);
        }

        private static void RestoreConfigFileState(object? data, object? privateKey, object? publicKey)
        {
            Type configFileType = typeof(ConfigFile);
            configFileType.GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, data);
            configFileType.GetField("jwtPrivateKey", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, privateKey);
            configFileType.GetField("jwtPublicKey", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, publicKey);
        }

        /// <summary>
        /// Answers the queries of an import without log entries and records the stored import period.
        /// </summary>
        private sealed class PeriodTestApiConn : SimulatedApiConnection
        {
            public string? StoredKey { get; private set; }
            public string? StoredValue { get; set; }
            public int? StoredUser { get; private set; }
            public bool FailUpsert { get; init; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ConfigQueries.getConfigItemByKey)
                {
                    List<ConfigItem> items = StoredValue is null ? [] : [new ConfigItem { Key = LogDataImportPeriod.kConfigKey, Value = StoredValue }];
                    return Task.FromResult((QueryResponseType)(object)items);
                }
                if (query == ConfigQueries.upsertConfigItem)
                {
                    if (FailUpsert)
                    {
                        throw new InvalidOperationException("upsert failed");
                    }
                    StoredKey = GetVariable<string>(variables, "config_key");
                    StoredValue = GetVariable<string>(variables, "config_value");
                    StoredUser = GetVariable<int>(variables, "config_user");
                    return Task.FromResult(default(QueryResponseType)!);
                }
                if (query == OwnerQueries.getOwnerId)
                {
                    // no application is known, the entries of a source are therefore not written
                    return Task.FromResult((QueryResponseType)(object)new List<OwnerIdModel>());
                }
                if (query == MonitorQueries.addDataImportLogEntry)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
                }
                return Task.FromResult(default(QueryResponseType)!);
            }

            private static T? GetVariable<T>(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
                return value is null ? default : (T)value;
            }
        }
    }
}
