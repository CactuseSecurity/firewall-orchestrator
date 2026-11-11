using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Server;
using NUnit.Framework;
using FWO.Test.Helpers;

namespace FWO.Test
{
    /// <summary>
    /// Covers the import flow of the log data import which talks to the API,
    /// the pure conversion helpers are tested in <see cref="LogDataImportTest"/>.
    /// </summary>
    [TestFixture]
    internal class LogDataImportFlowTest
    {
        private const int kAddressPairsOfUnreachableDnsTest = 32;

        [Test]
        public void ReplaceLogEntriesMutation_DeletesAndInsertsAtomically()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LogDataQueries.replaceLogEntries, Does.Contain("delete_logging_log_entry"));
                Assert.That(LogDataQueries.replaceLogEntries, Does.Contain("insert_logging_log_entry"));
                Assert.That(LogDataQueries.replaceLogEntries, Does.Contain("owner_id: {_in: $ownerIds}"));
            });
        }

        [Test]
        public void DeleteLogEntriesOfOwnersMutation_OnlyRemovesTheRowsOfTheGivenOwners()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LogDataQueries.deleteLogEntriesOfOwners, Does.Contain("delete_logging_log_entry"));
                Assert.That(LogDataQueries.deleteLogEntriesOfOwners, Does.Contain("owner_id: {_in: $ownerIds}"));
                Assert.That(LogDataQueries.deleteLogEntriesOfOwners, Does.Not.Contain("insert_logging_log_entry"));
            });
        }

        [Test]
        public void DeleteOrphanedIpMetadataMutation_OnlyRemovesRowsWithoutAnyLogEntry()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LogDataQueries.deleteOrphanedIpMetadata, Does.Contain("delete_logging_ip_metadata"));
                Assert.That(LogDataQueries.deleteOrphanedIpMetadata, Does.Contain("_not: {source_log_entries: {}}"));
                Assert.That(LogDataQueries.deleteOrphanedIpMetadata, Does.Contain("_not: {destination_log_entries: {}}"),
                    "an address used only as destination still needs its metadata");
            });
        }

        [Test]
        public void GetIpMetadataSourcesQuery_ReadsAllRangesAtOnce()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LogDataQueries.getIpMetadataSources, Does.Not.Contain("$"),
                    "the ranges are matched in memory, a query per logged address would not scale");
                Assert.That(LogDataQueries.getIpMetadataSources, Does.Contain("is_deleted: {_eq: false}"));
                Assert.That(LogDataQueries.getIpMetadataSources, Does.Contain($"group_type: {{_eq: {(int)ModellingTypes.ModObjectType.NetworkArea}}}"));
            });
        }

        [Test]
        public void PendingImportBacklogs_IgnoreTheImportControlRowsOfALogImport()
        {
            // a log data import writes an import control row without a management which nothing
            // ever maps or synchronizes: picked up by these backlogs it would stay in them forever,
            // fail the incremental rule_owner mapping on an unsupported import type, force a full
            // reinitialize in every run and disable the NameField rule_owner prefilter
            Assert.Multiple(() =>
            {
                Assert.That(ImportQueries.getPendingRuleOwnerImports, Does.Contain($"import_type_id: {{_in: [{ImportType.RULE}, {ImportType.OWNER}]}}"));
                Assert.That(FlowQueries.getPendingFlowSyncImports, Does.Contain($"import_type_id: {{ _eq: {ImportType.RULE} }}"));
                Assert.That(ImportQueries.addImportForLog, Does.Contain("import_type_id: $importTypeId"),
                    "the log import row is typed, so the backlogs can tell it apart");
            });
        }

        [Test]
        public async Task SaveEntries_RemovesTheStoredRowsWhenNothingOfTheSourceCanBeImported()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection, replaceExisting: true);
            List<LogDataImportEntry> sourceEntries = [NewSourceEntry("APP-1", 30, "not an address", "198.51.100.1")];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.RemovedOwnerIds, Is.EqualTo(new List<int> { 11 }),
                    "the source is the current truth about its applications, also without importable entries");
                Assert.That(apiConnection.InsertedEntries, Is.Empty);
                Assert.That(apiConnection.CompletedImports, Is.EqualTo(new List<bool> { true }));
                Assert.That(apiConnection.LogEntryDescriptions, Has.Some.Contains("was removed"));
            });
        }

        [Test]
        public async Task SaveEntries_RemovesNothingWithoutReplacement()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection, replaceExisting: false);
            List<LogDataImportEntry> sourceEntries = [NewSourceEntry("APP-1", 30, "not an address", "198.51.100.1")];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.RemovedOwnerIds, Is.Empty, "added log data is only removed by the retention");
                Assert.That(apiConnection.CreateImportControlCalls, Is.Zero);
            });
        }

        [Test]
        public async Task SaveEntries_RemovesNothingForAnUnknownApplication()
        {
            LogDataImportTestApiConn apiConnection = new();
            LogDataImport import = CreateImport(apiConnection, replaceExisting: true);
            List<LogDataImportEntry> sourceEntries = [NewSourceEntry("UNKNOWN", 30, "192.0.2.1", "198.51.100.1")];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.RemovedOwnerIds, Is.Empty);
                Assert.That(apiConnection.CreateImportControlCalls, Is.Zero, "nothing was changed");
            });
        }

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
                Assert.That(apiConnection.OrphanedMetadataPurges, Is.EqualTo(1),
                    "metadata of addresses without a log entry does not outlive the retention");
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
                Assert.That(apiConnection.ReplacementOwnerIds, Is.EqualTo(new List<int> { 11 }),
                    "replacement is enabled by default so absent flows do not remain current");
            });
        }

        [Test]
        public async Task SaveEntries_StoresDistinctAddressMetadataWithTheLogEntries()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            apiConnection.MetadataSources.AddRange(
            [
                // the ranges overlap and repeat the same values, so the merge of several ranges
                // covering one address is exercised as well
                NewMetadataSource("192.0.2.0", "192.0.2.255", "APP-B", "AREA-2"),
                NewMetadataSource("192.0.2.1", "192.0.2.1", "APP-A", "AREA-1"),
                NewMetadataSource("192.0.2.1", "192.0.2.1", "APP-A", "AREA-1"),
                NewMetadataSource("203.0.113.0", "203.0.113.255", "APP-C", "AREA-3")
            ]);
            LogDataImport import = CreateImport(apiConnection, reverseDnsLookup: (address, _) =>
                Task.FromResult<string?>(address.ToString() == "192.0.2.1" ? "source.example.test" : ""));
            List<LogDataImportEntry> sourceEntries =
            [
                NewSourceEntry("APP-1", 5, "192.0.2.1", "198.51.100.1"),
                NewSourceEntry("APP-1", 4, "192.0.2.1", "198.51.100.2")
            ];

            await InvokeSaveEntries(import, sourceEntries);

            IpMetadata sourceMetadata = apiConnection.InsertedMetadata.Single(metadata => metadata.IpAddress == "192.0.2.1/32");
            IpMetadata destinationMetadata = apiConnection.InsertedMetadata.Single(metadata => metadata.IpAddress == "198.51.100.1/32");
            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertedMetadata, Has.Count.EqualTo(3), "repeated addresses are enriched once");
                Assert.That(sourceMetadata.AppIds, Is.EqualTo(new List<string> { "APP-A", "APP-B" }));
                Assert.That(sourceMetadata.AreaIds, Is.EqualTo(new List<string> { "AREA-1", "AREA-2" }));
                Assert.That(sourceMetadata.Dns, Is.EqualTo("source.example.test"));
                Assert.That(destinationMetadata.AppIds, Is.Empty, "an address outside every range keeps empty metadata");
                Assert.That(destinationMetadata.AreaIds, Is.Empty);
                Assert.That(destinationMetadata.Dns, Is.Empty);
                Assert.That(apiConnection.MetadataLookups, Is.EqualTo(1), "the ranges are read once per batch");
            });
        }

        [Test]
        public async Task SaveEntries_SkipsMetadataSourcesWithAnUnusableRange()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            apiConnection.MetadataSources.AddRange(
            [
                NewMetadataSource("not-an-ip", "192.0.2.255", "APP-BROKEN", "AREA-BROKEN"),
                NewMetadataSource("192.0.2.1", "2001:db8::1", "APP-MIXED", "AREA-MIXED"),
                NewMetadataSource("192.0.2.1", "192.0.2.1", "APP-A", "AREA-1")
            ]);
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries = [NewSourceEntry("APP-1", 5, "192.0.2.1", "198.51.100.1")];

            await InvokeSaveEntries(import, sourceEntries);

            IpMetadata sourceMetadata = apiConnection.InsertedMetadata.Single(metadata => metadata.IpAddress == "192.0.2.1/32");
            Assert.Multiple(() =>
            {
                Assert.That(sourceMetadata.AppIds, Is.EqualTo(new List<string> { "APP-A" }));
                Assert.That(sourceMetadata.AreaIds, Is.EqualTo(new List<string> { "AREA-1" }));
            });
        }

        [Test]
        public async Task SaveEntries_KeepsCurrentFlowsForEveryOwnerWhenTheSourceIsLimited()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            apiConnection.OwnerIdsByAppId["APP-2"] = 22;
            LogDataImport import = CreateImport(apiConnection, maxEntries: 1);
            List<LogDataImportEntry> sourceEntries = new()
            {
                NewSourceEntry("APP-1", 1000, "192.0.2.1", "198.51.100.1"),
                NewSourceEntry("APP-1", 900, "192.0.2.2", "198.51.100.2"),
                NewSourceEntry("APP-2", 10, "192.0.2.3", "198.51.100.3")
            };

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.ReplacementOwnerIds, Is.EquivalentTo(new List<int> { 11, 22 }));
                Assert.That(apiConnection.InsertedEntries, Has.Count.EqualTo(2));
                Assert.That(apiConnection.InsertedEntries.Select(entry => entry.OwnerId), Is.EquivalentTo(new List<int> { 11, 22 }),
                    "a noisy owner must not displace every current flow of a quieter owner");
            });
        }

        [Test]
        public async Task SaveEntries_ReplacesRowsForEveryKnownApplicationIdInTheSource()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            apiConnection.OwnerIdsByAppId["APP-2"] = 22;
            LogDataImport import = CreateImport(apiConnection, replaceExisting: true);
            LogDataImportEntry invalidEntry = NewSourceEntry("APP-2", 40, "not an address", "198.51.100.2");
            List<LogDataImportEntry> sourceEntries =
            [
                NewSourceEntry("APP-1", 30, "192.0.2.1", "198.51.100.1"),
                invalidEntry,
                NewSourceEntry("UNKNOWN", 20, "192.0.2.3", "198.51.100.3")
            ];

            await InvokeSaveEntries(import, sourceEntries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.ReplacementOwnerIds, Is.EquivalentTo(new List<int> { 11, 22 }));
                Assert.That(apiConnection.InsertedEntries, Has.Count.EqualTo(1));
                Assert.That(apiConnection.CompletedImports, Is.EqualTo(new List<bool> { true }));
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
        public async Task SaveEntries_WritesTheEntriesOfAnUnimportableSourceToTheLog()
        {
            LogDataImportTestApiConn apiConnection = new();
            LogDataImport import = CreateImport(apiConnection);
            List<LogDataImportEntry> sourceEntries = [NewSourceEntry("UNKNOWN", 30, "192.0.2.1", "198.51.100.1")];
            string writtenLog = await ConsoleOutput.CaptureAsync(() => InvokeSaveEntries(import, sourceEntries));

            Assert.Multiple(() =>
            {
                Assert.That(writtenLog, Does.Contain("None of the 1 entries"),
                    "the source is removed, so its entries have to be readable in the log afterwards");
                Assert.That(writtenLog, Does.Contain("\"app_id\":\"UNKNOWN\""));
                Assert.That(writtenLog, Does.Contain("\"source\":\"192.0.2.1\""));
                Assert.That(apiConnection.LogEntryDescriptions, Has.Some.Contains("are written to the log"));
            });
        }

        [Test]
        public async Task SaveEntries_LogsNoMoreUnimportedEntriesThanAnImportWouldHaveKept()
        {
            LogDataImportTestApiConn apiConnection = new();
            LogDataImport import = CreateImport(apiConnection, maxEntries: 2);
            List<LogDataImportEntry> sourceEntries = [.. Enumerable.Range(1, 120)
                .Select(entryNumber => NewSourceEntry("UNKNOWN", entryNumber, $"192.0.2.{entryNumber % 250}", "198.51.100.1"))];
            string writtenLog = await ConsoleOutput.CaptureAsync(() => InvokeSaveEntries(import, sourceEntries));

            Assert.Multiple(() =>
            {
                Assert.That(writtenLog, Does.Contain("None of the 120 entries"), "how much was lost is still reported");
                Assert.That(writtenLog, Does.Contain("its first 2 entries"));
                Assert.That(writtenLog.Split("\"app_id\"").Length - 1, Is.EqualTo(2),
                    "a huge export of an unknown application must not flood the log in every run");
            });
        }

        [Test]
        public async Task SaveEntries_LogsNoEntriesOfAnEmptySource()
        {
            LogDataImportTestApiConn apiConnection = new();
            LogDataImport import = CreateImport(apiConnection);
            string logOutput = await ConsoleOutput.CaptureAsync(() => InvokeSaveEntries(import, []));

            Assert.Multiple(() =>
            {
                Assert.That(logOutput, Does.Not.Contain("could be imported"));
                Assert.That(apiConnection.LogEntryDescriptions, Has.Some.Contains("No log entries found"));
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

        /// <summary>
        /// A stalled resolver must stop before any database change when the job is cancelled.
        /// </summary>
        [Test]
        public async Task SaveEntries_CancelsPendingDnsWithoutWriting()
        {
            using CancellationTokenSource cancellation = new();
            TaskCompletionSource lookupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection, reverseDnsLookup: async (_, token) =>
            {
                lookupStarted.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                return "";
            });
            List<LogDataImportEntry> entries = [NewSourceEntry("APP-1", 1, "192.0.2.1", "198.51.100.1")];
            Task save = InvokeSaveEntries(import, entries, cancellationToken: cancellation.Token);
            await lookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            Assert.That(async () => await save.WaitAsync(TimeSpan.FromSeconds(5)), Throws.InstanceOf<OperationCanceledException>());
            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.CreateImportControlCalls, Is.Zero);
                Assert.That(apiConnection.InsertedEntries, Is.Empty);
                Assert.That(apiConnection.RemovedOwnerIds, Is.Empty);
            });
        }

        /// <summary>
        /// Cancellation reaches metadata preparation through Run and leaves the source unacknowledged.
        /// </summary>
        [Test]
        [NonParallelizable]
        public void Run_CancelledDnsKeepsTheSourceForRetry()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"fwo-log-cancel-{Guid.NewGuid():N}");
            var originalState = SnapshotConfigFileState();
            using CancellationTokenSource cancellation = new();
            try
            {
                Directory.CreateDirectory(tempRoot);
                ConfigureAllowedCustomizationRoots(tempRoot);
                string customizationRoot = Path.Combine(tempRoot, "scripts", "customizing");
                Directory.CreateDirectory(customizationRoot);
                string sourcePath = Path.Combine(customizationRoot, "log-source");
                LogDataImportFile file = new()
                {
                    Logs = [NewSourceEntry("APP-1", 1, "192.0.2.1", "198.51.100.1")]
                };
                File.WriteAllText(sourcePath + ".json", System.Text.Json.JsonSerializer.Serialize(file));
                LogDataImportTestApiConn apiConnection = new();
                apiConnection.OwnerIdsByAppId["APP-1"] = 11;
                List<string> paths = [sourcePath];
                LogDataImport import = CreateImport(apiConnection,
                    importPath: System.Text.Json.JsonSerializer.Serialize(paths),
                    reverseDnsLookup: async (_, token) =>
                    {
                        cancellation.Cancel();
                        await Task.Delay(Timeout.Infinite, token);
                        return "";
                    });

                Assert.That(async () => await import.Run(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
                Assert.Multiple(() =>
                {
                    Assert.That(File.Exists(sourcePath + ".json"), Is.True);
                    Assert.That(apiConnection.InsertedEntries, Is.Empty);
                    Assert.That(apiConnection.CreateImportControlCalls, Is.Zero);
                });
            }
            finally
            {
                RestoreConfigFileState(originalState.Data, originalState.JwtPrivateKey, originalState.JwtPublicKey);
                Directory.Delete(tempRoot, true);
            }
        }

        /// <summary>
        /// Stored results, including negative results, avoid repeated DNS queries.
        /// </summary>
        [TestCase("cached.example.test")]
        [TestCase("")]
        public async Task SaveEntries_ReusesStoredDnsResults(string storedName)
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            apiConnection.StoredMetadata.Add(new IpMetadata { IpAddress = "192.0.2.1/32", Dns = storedName, DnsLookupCompleted = true });
            apiConnection.StoredMetadata.Add(new IpMetadata { IpAddress = "198.51.100.1/32", DnsLookupCompleted = true });
            int lookupCalls = 0;
            LogDataImport import = CreateImport(apiConnection, reverseDnsLookup: (_, _) =>
            {
                Interlocked.Increment(ref lookupCalls);
                return Task.FromResult<string?>("");
            });
            List<LogDataImportEntry> entries = [NewSourceEntry("APP-1", 1, "192.0.2.1", "198.51.100.1")];

            await InvokeSaveEntries(import, entries);

            Assert.That(apiConnection.InsertedMetadata.Single(item => item.IpAddress == "192.0.2.1/32").Dns, Is.EqualTo(storedName));
            Assert.That(lookupCalls, Is.Zero);
        }

        /// <summary>
        /// Disabled lookups retain cached names and leave new addresses eligible for a later lookup.
        /// </summary>
        [Test]
        public async Task SaveEntries_DisabledDnsRetainsNamesWithoutResolvingNewAddresses()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            apiConnection.StoredMetadata.Add(new IpMetadata { IpAddress = "192.0.2.1/32", Dns = "cached.example.test", DnsLookupCompleted = true });
            int lookupCalls = 0;
            LogDataImport import = CreateImport(apiConnection, resolveDns: false, reverseDnsLookup: (_, _) =>
            {
                Interlocked.Increment(ref lookupCalls);
                return Task.FromResult<string?>("");
            });
            List<LogDataImportEntry> entries = [NewSourceEntry("APP-1", 1, "192.0.2.1", "198.51.100.1")];

            await InvokeSaveEntries(import, entries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertedMetadata.Single(item => item.IpAddress == "192.0.2.1/32").Dns, Is.EqualTo("cached.example.test"));
                Assert.That(apiConnection.InsertedMetadata.Single(item => item.IpAddress == "198.51.100.1/32").DnsLookupCompleted, Is.False);
                Assert.That(lookupCalls, Is.Zero);
            });
            apiConnection.StoredMetadata.Clear();
            apiConnection.StoredMetadata.AddRange(apiConnection.InsertedMetadata);
            apiConnection.InsertedMetadata.Clear();
            LogDataImport enabledImport = CreateImport(apiConnection, reverseDnsLookup: (_, _) =>
            {
                Interlocked.Increment(ref lookupCalls);
                return Task.FromResult<string?>("new.example.test");
            });
            await InvokeSaveEntries(enabledImport, entries);
            Assert.That(lookupCalls, Is.EqualTo(1), "re-enabling resolves only the address not previously looked up");
            Assert.That(apiConnection.InsertedMetadata.Single(item => item.IpAddress == "198.51.100.1/32").Dns, Is.EqualTo("new.example.test"));

        }

        /// <summary>
        /// DNS outages must not reject otherwise valid log data.
        /// </summary>
        [Test]
        public async Task SaveEntries_ImportsWhenDnsFails()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection,
                reverseDnsLookup: (_, _) => throw new System.Net.Sockets.SocketException());
            List<LogDataImportEntry> entries = [NewSourceEntry("APP-1", 1, "192.0.2.1", "198.51.100.1")];

            await InvokeSaveEntries(import, entries);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertedEntries, Has.Count.EqualTo(1));
                Assert.That(apiConnection.InsertedMetadata.Select(item => item.Dns), Is.All.Empty);
                Assert.That(apiConnection.InsertedMetadata.Select(item => item.DnsLookupCompleted), Is.All.False,
                    "a failed lookup must not be stored as an address without name");
            });
        }

        /// <summary>
        /// Once no DNS server answers, the remaining addresses of the batch are not looked up: each of them
        /// would only wait for the same timeout. They stay eligible for the next import.
        /// </summary>
        [Test]
        public async Task SaveEntries_SkipsTheRemainingLookupsWhenNoDnsServerAnswers()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            int lookupCalls = 0;
            LogDataImport import = CreateImport(apiConnection, reverseDnsLookup: (_, _) =>
            {
                Interlocked.Increment(ref lookupCalls);
                throw new System.Net.Sockets.SocketException();
            });
            List<LogDataImportEntry> entries = [.. Enumerable.Range(1, kAddressPairsOfUnreachableDnsTest)
                .Select(index => NewSourceEntry("APP-1", 1, $"192.0.2.{index}", $"198.51.100.{index}"))];

            await InvokeSaveEntries(import, entries);

            Assert.Multiple(() =>
            {
                // every parallel worker stops after its own failed lookup at the latest
                Assert.That(lookupCalls, Is.LessThanOrEqualTo(LogDataImport.ReverseLookupParallelism));
                Assert.That(apiConnection.InsertedEntries, Has.Count.EqualTo(kAddressPairsOfUnreachableDnsTest));
                Assert.That(apiConnection.InsertedMetadata, Has.Count.EqualTo(2 * kAddressPairsOfUnreachableDnsTest));
                Assert.That(apiConnection.InsertedMetadata.Select(item => item.DnsLookupCompleted), Is.All.False);
            });
        }

        /// <summary>
        /// A lookup without definitive answer keeps a stored name and is repeated by the next import.
        /// A server answering without result does not stop the other lookups of the batch.
        /// </summary>
        [Test]
        public async Task SaveEntries_RetriesFailedDnsLookupsWithTheNextImport()
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            apiConnection.StoredMetadata.Add(new IpMetadata { IpAddress = "192.0.2.1/32", Dns = "old.example.test" });
            int lookupCalls = 0;
            LogDataImport failingImport = CreateImport(apiConnection, reverseDnsLookup: (_, _) =>
            {
                Interlocked.Increment(ref lookupCalls);
                return Task.FromResult<string?>(null);
            });
            List<LogDataImportEntry> entries = [NewSourceEntry("APP-1", 1, "192.0.2.1", "198.51.100.1")];

            await InvokeSaveEntries(failingImport, entries);

            Assert.Multiple(() =>
            {
                Assert.That(lookupCalls, Is.EqualTo(2));
                Assert.That(apiConnection.InsertedMetadata.Select(item => item.DnsLookupCompleted), Is.All.False);
                Assert.That(apiConnection.InsertedMetadata.Single(item => item.IpAddress == "192.0.2.1/32").Dns, Is.EqualTo("old.example.test"));
            });
            apiConnection.StoredMetadata.Clear();
            apiConnection.StoredMetadata.AddRange(apiConnection.InsertedMetadata);
            apiConnection.InsertedMetadata.Clear();
            LogDataImport workingImport = CreateImport(apiConnection, reverseDnsLookup: (address, _) =>
            {
                Interlocked.Increment(ref lookupCalls);
                return Task.FromResult<string?>(address.ToString() == "192.0.2.1" ? "new.example.test" : "");
            });

            await InvokeSaveEntries(workingImport, entries);

            Assert.Multiple(() =>
            {
                Assert.That(lookupCalls, Is.EqualTo(4), "both failed addresses are looked up again");
                Assert.That(apiConnection.InsertedMetadata.Select(item => item.DnsLookupCompleted), Is.All.True);
                Assert.That(apiConnection.InsertedMetadata.Single(item => item.IpAddress == "192.0.2.1/32").Dns, Is.EqualTo("new.example.test"));
            });
        }

        /// <summary>
        /// Timing belongs to the same rows and transaction as counts in both import modes.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task SaveEntries_StoresTimingWithCounts(bool replaceExisting)
        {
            LogDataImportTestApiConn apiConnection = new();
            apiConnection.OwnerIdsByAppId["APP-1"] = 11;
            LogDataImport import = CreateImport(apiConnection, replaceExisting: replaceExisting);
            DateTimeOffset importTime = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
            List<LogDataImportEntry> entries = [NewSourceEntry("APP-1", 1, "192.0.2.1", "198.51.100.1")];

            await InvokeSaveEntries(import, entries, importTime);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.InsertedEntries.Single().ImportTime, Is.EqualTo(importTime));
                Assert.That(apiConnection.InsertedEntries.Single().LogTimeRangeInSeconds, Is.EqualTo(604800));
            });
        }

        private static LogDataImport CreateImport(ApiConnection apiConnection, string importPath = "[]",
            int maxEntries = 1000, int retentionDays = 90, bool replaceExisting = true,
            Func<System.Net.IPAddress, CancellationToken, Task<string?>>? reverseDnsLookup = null, bool resolveDns = true)
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ImportLogDataPath = importPath,
                ImportLogDataMaxEntries = maxEntries,
                LogDataRetentionDays = retentionDays,
                ReplaceExistingLogData = replaceExisting,
                ResolveLogDataDns = resolveDns
            };
            return new LogDataImport(apiConnection, globalConfig, reverseDnsLookup ?? ((_, _) => Task.FromResult<string?>("")));
        }

        private static IpMetadataSource NewMetadataSource(string ip, string ipEnd, string appId, string areaId)
        {
            return new IpMetadataSource
            {
                Ip = ip,
                IpEnd = ipEnd,
                Owner = new FwoOwnerBase { ExtAppId = appId },
                AreaMemberships =
                [
                    new IpMetadataAreaMembership { Area = new IpMetadataArea { IdString = areaId } }
                ]
            };
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

        private static async Task InvokeSaveEntries(LogDataImport import, List<LogDataImportEntry> sourceEntries,
            DateTimeOffset? importTime = null, CancellationToken cancellationToken = default)
        {
            MethodInfo method = typeof(LogDataImport).GetMethod("SaveEntries", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(LogDataImport).FullName, "SaveEntries");
            object[] arguments =
            [
                sourceEntries,
                "/usr/local/fworch/scripts/customizing/log_data_import/source",
                importTime ?? DateTimeOffset.UtcNow,
                604800,
                cancellationToken
            ];
            await (Task)method.Invoke(import, arguments)!;
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
            public List<IpMetadata> InsertedMetadata { get; } = [];
            public List<IpMetadataSource> MetadataSources { get; } = [];
            public List<IpMetadata> StoredMetadata { get; } = [];
            public List<int> ReplacementOwnerIds { get; } = [];
            public List<int> RemovedOwnerIds { get; } = [];
            public List<bool> CompletedImports { get; } = [];
            public List<string> LogEntryDescriptions { get; } = [];
            public int DeleteExpiredCalls { get; private set; }
            public int OrphanedMetadataPurges { get; private set; }
            public int CreateImportControlCalls { get; private set; }
            public int OwnerLookups { get; private set; }
            public int MetadataLookups { get; private set; }
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
                if (query == LogDataQueries.getIpMetadata)
                {
                    return Task.FromResult((QueryResponseType)(object)StoredMetadata.ToList());
                }
                if (query == LogDataQueries.getIpMetadataSources)
                {
                    MetadataLookups++;
                    return Task.FromResult((QueryResponseType)(object)MetadataSources.ToList());
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
                if (query == LogDataQueries.replaceLogEntries)
                {
                    ReplacementOwnerIds.AddRange(GetVariable<List<int>>(variables, "ownerIds") ?? []);
                    return Task.FromResult((QueryResponseType)(object)InsertEntries(variables)!);
                }
                if (query == LogDataQueries.deleteLogEntriesOfOwners)
                {
                    RemovedOwnerIds.AddRange(GetVariable<List<int>>(variables, "ownerIds") ?? []);
                    return Task.FromResult(default(QueryResponseType)!);
                }
                if (query == LogDataQueries.deleteExpiredLogEntries)
                {
                    DeleteExpiredCalls++;
                    LastExpiryTime = GetVariable<DateTimeOffset>(variables, "expiryTime");
                    return Task.FromResult(default(QueryResponseType)!);
                }
                if (query == LogDataQueries.deleteOrphanedIpMetadata)
                {
                    OrphanedMetadataPurges++;
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
                InsertedMetadata.AddRange(GetVariable<List<IpMetadata>>(variables, "metadata") ?? []);
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
