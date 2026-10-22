using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Labelling;
using FWO.Middleware.Server;
using NUnit.Framework;
using System.Collections;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Validation and writing of the labels of imported app servers.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class AppServerLabelImportTest
    {
        private const long kEventId = 55;
        private const long kImportControlId = 123;
        private static readonly List<ModellingImportLabel> kTrimmedDuplicates =
        [
            new() { Key = " Stage ", Value = " Prod " },
            new() { Key = "Stage", Value = "Prod" }
        ];
        private static readonly List<(string, string)> kStageProd = [("Stage", "Prod")];
        private static readonly List<ModellingImportLabel> kInvalidLabels =
        [
            new() { Key = "", Value = "Prod" },
            new() { Key = "Stage", Value = " " },
            new() { Key = new string('k', AppServerLabelImport.kMaxLabelKeyLength + 1), Value = "Prod" },
            new() { Key = "Stage", Value = new string('v', AppServerLabelImport.kMaxLabelValueLength + 1) },
            new() { Key = "approle", Value = "AR1" },
            new() { Key = "AppZone", Value = "AZ1" },
            new() { Key = "OS", Value = "Linux" }
        ];
        private static readonly List<(string, string)> kOsLinux = [("OS", "Linux")];
        private static readonly List<ModellingImportLabel> kSeveralStages =
        [
            new() { Key = "Stage", Value = "Prod" },
            new() { Key = "Stage", Value = "Test" },
            new() { Key = "Tag", Value = "a" },
            new() { Key = "Tag", Value = "b" },
            new() { Key = "OS", Value = "Linux" }
        ];
        private static readonly List<(string, string)> kTagsAndOs = [("Tag", "a"), ("Tag", "b"), ("OS", "Linux")];
        private static readonly List<ModellingImportLabel> kLowerCaseKnownKey = [new() { Key = "tag", Value = "a" }];
        private static readonly List<(string, string)> kKnownKeyCasing = [("Tag", "a")];
        private static readonly List<ModellingImportLabel> kStageTest = [new() { Key = "Stage", Value = "Test" }];
        private static readonly List<long> kNewLabel = [1000];
        private static readonly List<long> kProdLabel = [1];
        private static readonly List<long> kAppServer10 = [10];
        private static readonly List<long> kNoAppServers = [];
        private static readonly List<long> kRunImportControl = [kImportControlId];
        private static readonly List<(long, long, long)> kNewLabelOnBothServers = [(10, 1000, kEventId), (11, 1000, kEventId)];

        [Test]
        public async Task ValidateLabels_TrimsAndDropsDuplicates()
        {
            LabelImportApiConnection apiConnection = new();
            AppServerLabelImport labelImport = BuildLabelImport(apiConnection, out List<string> logEntries);

            List<(string, string)> labels = await labelImport.ValidateLabels("srv", kTrimmedDuplicates);

            Assert.That(labels, Is.EqualTo(kStageProd));
            Assert.That(logEntries, Is.Empty);
        }

        [Test]
        public async Task ValidateLabels_RejectsEmptyTooLongAndGroupKeys()
        {
            LabelImportApiConnection apiConnection = new();
            AppServerLabelImport labelImport = BuildLabelImport(apiConnection, out List<string> logEntries);

            List<(string, string)> labels = await labelImport.ValidateLabels("srv", kInvalidLabels);

            Assert.That(labels, Is.EqualTo(kOsLinux));
            Assert.That(logEntries, Has.Count.EqualTo(6));
            Assert.That(logEntries.Count(entry => entry.Contains("maintained as group")), Is.EqualTo(2));
        }

        [Test]
        public async Task ValidateLabels_SkipsLabelsBeyondTheLimit()
        {
            LabelImportApiConnection apiConnection = new() { AllowMultipleValues = true };
            AppServerLabelImport labelImport = BuildLabelImport(apiConnection, out List<string> logEntries);
            List<ModellingImportLabel> manyLabels = [.. Enumerable.Range(0, AppServerLabelImport.kMaxLabelsPerAppServer + 5)
                .Select(index => new ModellingImportLabel { Key = "Tag", Value = $"v{index}" })];

            List<(string, string)> labels = await labelImport.ValidateLabels("srv", manyLabels);

            Assert.That(labels, Has.Count.EqualTo(AppServerLabelImport.kMaxLabelsPerAppServer));
            Assert.That(logEntries, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ValidateLabels_RejectsSeveralValuesOfSingleValueKeysOnly()
        {
            LabelImportApiConnection apiConnection = new() { AllowMultipleValues = true };
            AppServerLabelImport labelImport = BuildLabelImport(apiConnection, out List<string> logEntries);

            List<(string, string)> labels = await labelImport.ValidateLabels("srv", kSeveralStages);

            Assert.That(labels, Is.EqualTo(kTagsAndOs));
            Assert.That(logEntries.Single(), Does.Contain("Stage allows a single value only"));
        }

        [Test]
        public async Task ValidateLabels_UsesTheSpellingOfKnownKeys()
        {
            LabelImportApiConnection apiConnection = new();
            AppServerLabelImport labelImport = BuildLabelImport(apiConnection, out _);

            List<(string, string)> labels = await labelImport.ValidateLabels("srv", kLowerCaseKnownKey);

            Assert.That(labels, Is.EqualTo(kKnownKeyCasing));
        }

        [Test]
        public async Task SyncLabels_CreatesUnknownLabelsAndOneChangeEventForTheRun()
        {
            LabelImportApiConnection apiConnection = new();
            AppServerLabelImport labelImport = BuildLabelImport(apiConnection, out _);

            await labelImport.SyncLabels(10, "srv-1", kStageTest);
            await labelImport.SyncLabels(11, "srv-2", kStageTest);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertCalls, Is.EqualTo(1));
                Assert.That(apiConnection.ImportControlCalls, Is.EqualTo(1));
                Assert.That(apiConnection.ChangeEventImportControlIds, Is.EqualTo(kRunImportControl));
                Assert.That(apiConnection.Added.Select(added => (added.ownerNetworkId, added.labelId, added.eventId)),
                    Is.EqualTo(kNewLabelOnBothServers));
                Assert.That(labelImport.LabelsChanged, Is.True);
            });
        }

        [Test]
        public async Task SyncLabels_ReplacesChangedLabels()
        {
            LabelImportApiConnection apiConnection = new();
            apiConnection.ActiveLabels.Add(new OwnerNetworkLabel { OwnerNetworkId = 10, LabelId = 1 });
            AppServerLabelImport labelImport = BuildLabelImport(apiConnection, out _);
            await labelImport.LoadActiveLabels(kAppServer10);

            await labelImport.SyncLabels(10, "srv", kStageTest);

            Assert.That(apiConnection.Removed.Single().labelIds, Is.EqualTo(kProdLabel));
            Assert.That(apiConnection.Added.Select(added => added.labelId), Is.EqualTo(kNewLabel));
        }

        [Test]
        public async Task SyncLabels_WithoutChangesWritesNothing()
        {
            LabelImportApiConnection apiConnection = new();
            apiConnection.LabelIds[("Stage", "Test")] = 1000;
            apiConnection.ActiveLabels.Add(new OwnerNetworkLabel { OwnerNetworkId = 10, LabelId = 1000 });
            AppServerLabelImport labelImport = BuildLabelImport(apiConnection, out _);
            await labelImport.LoadActiveLabels(kAppServer10);

            await labelImport.SyncLabels(10, "srv", kStageTest);
            await labelImport.SyncLabels(10, "srv", null);
            await labelImport.CloseLabels(12, "no-labels");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.ImportControlCalls, Is.EqualTo(0));
                Assert.That(apiConnection.Added, Is.Empty);
                Assert.That(apiConnection.Removed, Is.Empty);
                Assert.That(labelImport.LabelsChanged, Is.False);
            });
        }

        [Test]
        public async Task CloseLabels_ClosesAllActiveLabels()
        {
            LabelImportApiConnection apiConnection = new();
            apiConnection.ActiveLabels.Add(new OwnerNetworkLabel { OwnerNetworkId = 10, LabelId = 1 });
            AppServerLabelImport labelImport = BuildLabelImport(apiConnection, out _);
            await labelImport.LoadActiveLabels(kAppServer10);

            await labelImport.CloseLabels(10, "srv");

            Assert.That(apiConnection.Removed.Single(), Is.EqualTo((10L, kProdLabel, kEventId)).Using<(long, List<long>, long)>(SameRemoval));
        }

        [Test]
        public async Task SyncLabels_LogsErrorsInsteadOfThrowing()
        {
            LabelImportApiConnection apiConnection = new() { FailWrites = true };
            AppServerLabelImport labelImport = BuildLabelImport(apiConnection, out List<string> logEntries);

            await labelImport.SyncLabels(10, "srv", kStageTest);
            await labelImport.LoadActiveLabels(kNoAppServers);

            Assert.That(logEntries.Single(), Does.Contain("Labels of app server srv could not be imported"));
            Assert.That(labelImport.LabelsChanged, Is.False);
        }

        private static bool SameRemoval((long, List<long>, long) actual, (long, List<long>, long) expected)
        {
            return actual.Item1 == expected.Item1 && actual.Item2.SequenceEqual(expected.Item2) && actual.Item3 == expected.Item3;
        }

        private static AppServerLabelImport BuildLabelImport(LabelImportApiConnection apiConnection, out List<string> logEntries)
        {
            List<string> entries = [];
            logEntries = entries;
            return new AppServerLabelImport(apiConnection, new OwnerChangeImportTracker(apiConnection), (_, _, description) =>
            {
                entries.Add(description);
                return Task.CompletedTask;
            });
        }

        private sealed class LabelImportApiConnection : SimulatedApiConnection
        {
            public bool AllowMultipleValues { get; init; }
            public bool FailWrites { get; init; }
            public Dictionary<(string key, string value), long> LabelIds { get; } = [];
            public List<OwnerNetworkLabel> ActiveLabels { get; } = [];
            public List<(long ownerNetworkId, long labelId, long eventId)> Added { get; } = [];
            public List<(long ownerNetworkId, List<long> labelIds, long eventId)> Removed { get; } = [];
            public List<long> ChangeEventImportControlIds { get; } = [];
            public int UpsertCalls { get; private set; }
            public int ImportControlCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                return Task.FromResult((QueryResponseType)Answer(query, variables));
            }

            private object Answer(string query, object? variables)
            {
                if (query == LabellingQueries.getLabelKeys)
                {
                    return new List<LabelKey> { new() { Name = "Tag", AllowMultipleValues = AllowMultipleValues } };
                }
                if (query == LabellingQueries.getOwnerNetworkLabels)
                {
                    List<long> ids = [.. (IEnumerable<long>)Value(variables, "ownerNetworkIds")!];
                    return ActiveLabels.Where(label => ids.Contains(label.OwnerNetworkId)).ToList();
                }
                if (query == LabellingQueries.getLabelIds)
                {
                    return LabelIds.Select(label => new LabelValue { Id = label.Value, Value = label.Key.value, Key = new LabelKey { Name = label.Key.key } }).ToList();
                }
                if (query == ImportQueries.addImportForOwner)
                {
                    ++ImportControlCalls;
                    return new InsertImportControl { Returning = [new ImportControl { ControlId = kImportControlId }] };
                }
                if (query == LabellingQueries.addLabelChangeEvent)
                {
                    ChangeEventImportControlIds.Add((long)Value(variables, "importControlId")!);
                    return new ReturnId { Id = kEventId };
                }
                return Write(query, variables);
            }

            private ReturnId Write(string query, object? variables)
            {
                if (FailWrites)
                {
                    throw new InvalidOperationException("write failed");
                }
                if (query == LabellingQueries.upsertImportedLabels)
                {
                    ++UpsertCalls;
                    foreach (object labelKey in (IEnumerable)Value(variables, "labelKeys")!)
                    {
                        string keyName = (string)Value(labelKey, "key_name")!;
                        foreach (object labelValue in (IEnumerable)Value(Value(labelKey, "label_values"), "data")!)
                        {
                            LabelIds.TryAdd((keyName, (string)Value(labelValue, "value")!), 1000 + LabelIds.Count);
                        }
                    }
                }
                else if (query == LabellingQueries.addOwnerNetworkLabels)
                {
                    foreach (object label in (IEnumerable)Value(variables, "labels")!)
                    {
                        Added.Add(((long)Value(label, "owner_network_id")!, (long)Value(label, "label_id")!, (long)Value(label, "created_event_id")!));
                    }
                }
                else if (query == LabellingQueries.removeOwnerNetworkLabels)
                {
                    List<long> labelIds = [.. (IEnumerable<long>)Value(variables, "labelIds")!];
                    Removed.Add(((long)Value(variables, "ownerNetworkId")!, labelIds, (long)Value(variables, "removedEventId")!));
                }
                else
                {
                    throw new NotImplementedException($"Query not implemented in label import test api: {query}");
                }
                return new ReturnId { AffectedRows = 1 };
            }

            private static object? Value(object? variables, string propertyName)
            {
                return variables?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(variables);
            }
        }
    }
}
