using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;

namespace FWO.Test
{
    public class UpdateRuleOwnerMappingTests
    {
        private UpdateRuleOwnerMappingCustomField customFieldService = null!;
        private UpdateRuleOwnerMappingIpBased ipBasedService = null!;
        private UpdateRuleOwnerMappingNameField nameFieldService = null!;

        [SetUp]
        public void Setup()
        {
            var globalConfig = new GlobalConfig
            {
                CustomFieldOwnerKey = @"[""owner""]",
                ModModelledMarker = "FWOC"
            };

            customFieldService = new UpdateRuleOwnerMappingCustomField(null!, globalConfig);
            ipBasedService = new UpdateRuleOwnerMappingIpBased(null!, globalConfig);
            nameFieldService = new UpdateRuleOwnerMappingNameField(null!, globalConfig);
        }

        [Test]
        public void BuildNewRuleOwnersCustomField_ShouldCreateMapping_WhenOwnerExists()
        {
            var rule = BuildRule();
            var owners = new List<FwoOwner> { BuildOwner() };

            var result = customFieldService.BuildNewRuleOwnersCustomField(new List<Rule> { rule }, owners);

            Assert.Multiple(() =>
            {
                Assert.That(result.Count, Is.EqualTo(1));
                Assert.That(result[0].OwnerId, Is.EqualTo(10));
                Assert.That(result[0].RuleId, Is.EqualTo(1));
            });
        }

        [TestCase("TeamB", 10, false)]
        [TestCase("TeamA", 10, true)]
        public void BuildNewRuleOwnersCustomField_ShouldReturnExpectedOwnerMatching(string ownerExtAppId, int expectedOwnerId, bool shouldMatch)
        {
            var rule = BuildRule();
            var owners = new List<FwoOwner> { BuildOwner(id: expectedOwnerId, extAppId: ownerExtAppId) };

            var result = customFieldService.BuildNewRuleOwnersCustomField(new List<Rule> { rule }, owners);

            Assert.That(result.Any(), Is.EqualTo(shouldMatch));
            if (shouldMatch)
            {
                Assert.That(result[0].OwnerId, Is.EqualTo(expectedOwnerId));
            }
        }

        [Test]
        public void BuildNewRuleOwnersCustomField_ShouldReturnEmpty_WhenKeyMissing()
        {
            var rule = new Rule
            {
                Id = 1,
                CustomFields = "{'someOtherKey':'TeamA'}",
                Metadata = new RuleMetadata { Id = 100 }
            };
            var owners = new List<FwoOwner> { new FwoOwner { Id = 10, ExtAppId = "TeamA" } };

            var result = customFieldService.BuildNewRuleOwnersCustomField(new List<Rule> { rule }, owners);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildNewRuleOwnersCustomField_ShouldReturnEmpty_WhenCustomFieldMissing()
        {
            var rule = BuildRule(customFields: "{}");
            var owners = new List<FwoOwner> { BuildOwner() };

            var result = customFieldService.BuildNewRuleOwnersCustomField(new List<Rule> { rule }, owners);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetIpRangeAndVersion_ShouldReturnValidRange()
        {
            var (range, version) = UpdateRuleOwnerMappingIpBased.GetIpRangeAndVersion(
                "192.168.1.1",
                "192.168.1.10"
            );

            Assert.Multiple(() =>
            {
                Assert.That(range, Is.Not.Null);
                Assert.That(version, Is.EqualTo(AddressFamily.InterNetwork));
            });
        }

        [Test]
        public void GetMatchingOwnerIds_ShouldMatchOwner_WhenIpOverlaps()
        {
            var rule = BuildRule(
                froms: new[] { BuildNetworkLocation("192.168.1.5", "192.168.1.5") }
            );

            var owners = new List<FwoOwner>
            {
                BuildOwner(ownerNetworks: new[] { BuildOwnerNetwork("192.168.1.0", "192.168.1.255") })
            };

            var prepared = ipBasedService.PrepareOwnerNetworks(owners);
            var result = UpdateRuleOwnerMappingIpBased.GetMatchingOwnerIds(rule, prepared);

            Assert.That(result.ContainsKey(10));
        }

        [Test]
        public void GetMatchingOwnerIds_ShouldReturnEmpty_WhenNoFromsTos()
        {
            var rule = new Rule { Id = 1, Froms = Array.Empty<NetworkLocation>(), Tos = Array.Empty<NetworkLocation>() };
            var ownerNetworks = new List<UpdateRuleOwnerMappingIpBased.OwnerNetworkPrepared>();

            var result = UpdateRuleOwnerMappingIpBased.GetMatchingOwnerIds(rule, ownerNetworks);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildNewRuleOwnersIpBased_ShouldMapMultipleOwners_WhenMultipleRangesOverlap()
        {
            var rule = new Rule
            {
                Id = 1,
                Froms = new[]
                {
                    new NetworkLocation(new NetworkUser(), new NetworkObject { IP = "192.168.1.5", IpEnd = "192.168.1.5" })
                }
            };

            var owners = new List<FwoOwner>
            {
                new FwoOwner
                {
                    Id = 10,
                    OwnerNetworks = new[]
                    {
                        new OwnerNetwork { IP = "192.168.1.0", IpEnd = "192.168.1.10" }
                    }
                },
                new FwoOwner
                {
                    Id = 20,
                    OwnerNetworks = new[]
                    {
                        new OwnerNetwork { IP = "192.168.1.0", IpEnd = "192.168.1.255" }
                    }
                }
            };

            var result = ipBasedService.BuildNewRuleOwnersIpBased(new List<Rule> { rule }, owners);

            Assert.That(result.Select(r => r.OwnerId), Is.EquivalentTo(new[] { 10, 20 }));
        }

        [Test]
        public void PrepareOwnerNetworks_ShouldSkipInvalidOwnerNetworks()
        {
            var owners = new List<FwoOwner>
            {
                new FwoOwner
                {
                    Id = 10,
                    OwnerNetworks = new[]
                    {
                        new OwnerNetwork { IP = "invalid", IpEnd = "192.168.1.255" },
                        new OwnerNetwork { IP = "192.168.2.0", IpEnd = "192.168.2.255" }
                    }
                }
            };

            var prepared = ipBasedService.PrepareOwnerNetworks(owners);

            Assert.That(prepared.Count, Is.EqualTo(1));
            Assert.That(prepared[0].Ranges.Count, Is.EqualTo(1));
        }

        [Test]
        public void GetIpRangeAndVersion_ShouldReturnNull_ForInvalidRange()
        {
            var (range, version) = UpdateRuleOwnerMappingIpBased.GetIpRangeAndVersion("invalid", "192.168.1.10");

            Assert.That(range, Is.Null);
            Assert.That(version, Is.Null);

            (range, version) = UpdateRuleOwnerMappingIpBased.GetIpRangeAndVersion("192.168.1.10", "192.168.1.1");

            Assert.That(range, Is.Null);
            Assert.That(version, Is.Null);
        }

        [Test]
        public void GetIpRangeAndVersion_ShouldHandleIPv6()
        {
            var (range, version) = UpdateRuleOwnerMappingIpBased.GetIpRangeAndVersion("2001:db8::1", "2001:db8::10");

            Assert.That(range, Is.Not.Null);
            Assert.That(version, Is.EqualTo(AddressFamily.InterNetworkV6));
        }

        [TestCase("FWOC123", "FWOC", 123)]
        [TestCase("FWOC123 CX123", "FWOC", 123)]
        [TestCase("123FWOC123 CX123", "FWOC", 123)]
        [TestCase("CCFWOC123 CX123", "FWOC", 123)]
        [TestCase("FWOC999_test", "FWOC", 999)]
        [TestCase("FWOC123CX456", "FWOC", 123)]
        public void ExtractNameFieldValue_ShouldExtractNumber_AfterMarker(string ruleName, string marker, int expected)
        {
            var rule = new Rule { Name = ruleName, Id = 1 };

            var result = UpdateRuleOwnerMappingNameField.ExtractNameFieldValue(rule, marker, out var errorMessage);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(errorMessage, Is.Null);
            });
        }

        [TestCase("FWOC", "FWOC")]
        [TestCase("FWOCabc", "FWOC")]
        [TestCase("APP123", "FWOC")]
        public void ExtractNameFieldValue_ShouldReturnNull_WhenMarkerHasNoNumericMatch(string ruleName, string marker)
        {
            var rule = new Rule { Name = ruleName, Id = 1 };

            var result = UpdateRuleOwnerMappingNameField.ExtractNameFieldValue(rule, marker, out var errorMessage);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Null);
                Assert.That(errorMessage, Is.Not.Null);
            });
        }

        [TestCase(null, "FWOC")]
        [TestCase("", "FWOC")]
        [TestCase("FWOC123", null)]
        [TestCase("FWOC123", "")]
        public void ExtractNameFieldValue_ShouldReturnNull_WhenInputsAreMissing(string? ruleName, string? marker)
        {
            var rule = new Rule { Name = ruleName, Id = 1 };

            var result = UpdateRuleOwnerMappingNameField.ExtractNameFieldValue(rule, marker!, out var errorMessage);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Null);
                Assert.That(errorMessage, Is.Not.Null);
            });
        }

        [Test]
        public void BuildNewRuleOwnersNameField_ShouldCreateMapping_WhenConnectionMatchesMarker()
        {
            MethodInfo method = typeof(UpdateRuleOwnerMappingNameField).GetMethod("BuildNewRuleOwnersNameField", BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new InvalidOperationException("BuildNewRuleOwnersNameField helper not found.");
            List<Rule> rules =
            [
                new Rule
                {
                    Id = 101,
                    Name = "FWOC501 allow",
                    Metadata = new RuleMetadata { Id = 9001 }
                }
            ];
            List<ModellingConnection> connections =
            [
                new ModellingConnection
                {
                    Id = 501,
                    AppId = 20,
                    Name = "Conn-501"
                }
            ];

            var result = (List<RuleOwner>)method.Invoke(nameFieldService, [rules, connections])!;

            Assert.Multiple(() =>
            {
                Assert.That(result.Count, Is.EqualTo(1));
                Assert.That(result[0].RuleId, Is.EqualTo(101));
                Assert.That(result[0].OwnerId, Is.EqualTo(20));
                Assert.That(result[0].OwnerMappingSourceId, Is.EqualTo((int)FWO.Basics.OwnerMappingSourceStm.NameField));
            });
        }

        /// <summary>
        /// Uses reflection to access the private <c>HandleOwnerImportNameField</c> and
        /// <c>BuildNewRuleOwnersNameField</c> methods without exposing them in the production API.
        /// <c>BindingFlags.NonPublic | BindingFlags.Instance</c> tells <c>GetMethod</c> to look for
        /// private instance methods. <c>method.Invoke(...)</c> executes the reflected method on the
        /// service instance with the provided arguments, so the test can verify both the owner-import
        /// step and the final name-field mapping flow together.
        /// </summary>
        [Test]
        public async Task HandleOwnerImportNameField_ShouldLoadRulesFilteredConnectionsAndRemovals_ForOwnerChange()
        {
            UpdateRuleOwnerMappingNameFieldApiConnection apiConnection = new()
            {
                ChangedOwners =
                [
                    new OwnerChange
                    {
                        ChangeAction = 'C',
                        OldOwner = new FwoOwner { Id = 20, ExtAppId = "APP-20" },
                        NewOwner = new FwoOwner { Id = 20, ExtAppId = "APP-20" }
                    }
                ],
                RulesToMap =
                [
                    new Rule
                    {
                        Id = 101,
                        Name = "FWOC501 allow",
                        Metadata = new RuleMetadata { Id = 9001 }
                    }
                ],
                ConnectionOwners =
                [
                    new ModellingConnection { Id = 501, AppId = 20, Name = "Conn-501" }
                ],
                RuleOwnersToRemove =
                [
                    new RuleOwner { RuleId = 101, OwnerId = 20, Created = 5 }
                ]
            };
            UpdateRuleOwnerMappingNameField service = new(apiConnection, new GlobalConfig { ModModelledMarker = "FWOC" });
            MethodInfo method = typeof(UpdateRuleOwnerMappingNameField).GetMethod("HandleOwnerImportNameField", BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new InvalidOperationException("HandleOwnerImportNameField helper not found.");

            var task = (Task<(List<Rule> RulesToMap, List<ModellingConnection> connectionOwners, List<RuleOwner> RuleOwnersToRemove)>)method.Invoke(service, [new ImportControl { ControlId = 88 }])!;
            var result = await task;
            MethodInfo buildMethod = typeof(UpdateRuleOwnerMappingNameField).GetMethod("BuildNewRuleOwnersNameField", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("BuildNewRuleOwnersNameField helper not found.");
            var newRuleOwners = (List<RuleOwner>)buildMethod.Invoke(service, [result.RulesToMap, result.connectionOwners])!;

            var rules = result.RulesToMap.ToList();
            var connections = result.connectionOwners.ToList();
            var ruleOwners = newRuleOwners.ToList();

            foreach (var r in rules)
                Console.WriteLine($"{r.Id} - {r.Id.GetType()}");
            var ids = rules.Select(r => r.Id).ToList();
            Console.WriteLine(ids[0].GetType());
            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.NameFieldRuleQueryCalls, Is.EqualTo(1));
                Assert.That(apiConnection.FilteredConnectionQueryCalls, Is.EqualTo(1));
                Assert.That(apiConnection.FilteredOwnerIds, Is.EquivalentTo(new[] { 20 }));
                Assert.That(apiConnection.RemovedOwnerIds, Is.EquivalentTo(new[] { 20 }));
                Assert.That(rules.Select(r => Convert.ToInt32(r.Id)), Is.EquivalentTo(new[] { 101 }));
                Assert.That(connections.Select(c => Convert.ToInt32(c.Id)), Is.EquivalentTo(new[] { 501 }));
                Assert.That(result.RuleOwnersToRemove.Select(ruleOwner => Convert.ToInt32(ruleOwner.OwnerId)), Is.EquivalentTo(new[] { 20 }));
                Assert.That(newRuleOwners.Select(ruleOwner => Convert.ToInt32(ruleOwner.OwnerId)), Is.EquivalentTo(new[] { 20 }));
                Assert.That(newRuleOwners.Select(ruleOwner => Convert.ToInt32(ruleOwner.RuleId)), Is.EquivalentTo(new[] { 101 }));
            });
        }

        [Test]
        public async Task RunAsyncCustomField_ShouldProcessIncrementalImports_WhenPendingImportCountIsWithinThreshold()
        {
            IncrementalCustomFieldApiConnection apiConnection = new(3);
            UpdateRuleOwnerMappingCustomField service = new(apiConnection, new GlobalConfig { CustomFieldOwnerKey = @"[""owner""]" });

            bool result = await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CompletedImports, Is.EquivalentTo(new long[] { 1, 2, 3 }));
                Assert.That(apiConnection.InsertedPairsByImport[1], Is.Empty, "Initial rule import must not map before owners exist.");
                Assert.That(apiConnection.ActivePairsAfterImport[1], Is.Empty);
                Assert.That(apiConnection.ActivePairsAfterImport[2], Has.Count.EqualTo(10));
                Assert.That(apiConnection.InsertedPairsByImport[3], Is.EquivalentTo(new[] { "1->2", "11->3" }));
                Assert.That(apiConnection.ActivePairsAfterImport[3], Does.Contain("1->2"));
                Assert.That(apiConnection.ActivePairsAfterImport[3], Does.Contain("11->3"));
                Assert.That(apiConnection.ActivePairsAfterImport.All(snapshot => snapshot.Value.Count == snapshot.Value.Distinct().Count()), Is.True, "Each active rule-owner snapshot should contain unique pairs only.");
                Assert.That(apiConnection.ActivePairsAfterImport[3], Is.EquivalentTo(new[]
                {
                    "1->2",
                    "3->3",
                    "4->4",
                    "5->5",
                    "6->1",
                    "7->2",
                    "8->3",
                    "9->4",
                    "10->5",
                    "11->3"
                }));
            });
        }

        [Test]
        public async Task RunAsyncCustomField_ShouldFallbackToFullReinitialize_WhenPendingImportCountExceedsThreshold()
        {
            FallbackToFullReinitializeCustomFieldApiConnection apiConnection = new();
            UpdateRuleOwnerMappingCustomField service = new(apiConnection, new GlobalConfig { CustomFieldOwnerKey = @"[""owner""]" });

            bool result = await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.FullReinitializeTriggered, Is.True);
                Assert.That(apiConnection.CompletedImports, Is.EquivalentTo(Enumerable.Range(1, 10).Select(id => (long)id)));
                Assert.That(apiConnection.FullReinitializeImportControlId, Is.EqualTo(100));
                Assert.That(apiConnection.ActivePairsAfterFullReinitialize, Is.EquivalentTo(new[]
                {
                    "1->2",
                    "3->1",
                    "4->5",
                    "5->3",
                    "6->6",
                    "7->4",
                    "8->5",
                    "10->2",
                    "11->3",
                    "12->1",
                    "13->2",
                    "14->1",
                    "15->3",
                    "16->6"
                }));
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task RunAsyncCustomField_ShouldReachSameInitialMapping_RegardlessOfInitialImportOrder(bool ruleImportFirst)
        {
            InitialOrderCustomFieldApiConnection apiConnection = new(ruleImportFirst);
            UpdateRuleOwnerMappingCustomField service = new(apiConnection, new GlobalConfig { CustomFieldOwnerKey = @"[""owner""]" });

            bool result = await service.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(apiConnection.CompletedImports, Is.EquivalentTo(new long[] { 1, 2 }));
                Assert.That(apiConnection.ActivePairsAfterImport[2], Is.EquivalentTo(new[]
                {
                    "1->1",
                    "2->2",
                    "3->3",
                    "4->1",
                    "5->2",
                    "6->3",
                    "7->1",
                    "8->2",
                    "9->3",
                    "10->1"
                }));
            });
        }

        [Test]
        public void RunAsyncCustomField_ShouldNotTryToInsertDuplicateActiveRuleOwnerPair()
        {
            DuplicateInsertGuardCustomFieldApiConnection apiConnection = new();
            UpdateRuleOwnerMappingCustomField service = new(apiConnection, new GlobalConfig { CustomFieldOwnerKey = @"[""owner""]" });

            Assert.DoesNotThrowAsync(async () => await service.RunAsync(), "Incremental mapping should remove the active pair before re-inserting it.");
        }

        [Test]
        public async Task SetAffectedRuleOwnersRemoved_ShouldPassChunkingOptionsToApiConnection()
        {
            RecordingChunkingApiConnection apiConnection = new();
            TestUpdateRuleOwnerMappingCustomField service = new(apiConnection, new GlobalConfig());

            List<RuleOwner> ruleOwners =
            [
                new() { RuleId = 1, OwnerId = 10, Created = 100 },
                new() { RuleId = 2, OwnerId = 20, Created = 100 }
            ];

            await service.CallSetAffectedRuleOwnersRemoved(ruleOwners, 999);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastQuery, Is.EqualTo(FWO.Api.Client.Queries.OwnerQueries.setAffectedRuleOwnersRemoved));
                Assert.That(apiConnection.LastChunkingOptions, Is.Not.Null);
                Assert.That(apiConnection.LastChunkingOptions!.Enabled, Is.True);
                Assert.That(apiConnection.LastChunkingOptions.ChunkVariableName, Is.EqualTo("objects"));
                Assert.That(apiConnection.LastChunkingOptions.ChunkSize, Is.EqualTo(500));
            });
        }

        [Test]
        public async Task InsertNewRuleOwners_ShouldPassChunkingOptionsToApiConnection()
        {
            RecordingChunkingApiConnection apiConnection = new();
            TestUpdateRuleOwnerMappingCustomField service = new(apiConnection, new GlobalConfig());

            List<RuleOwner> ruleOwners =
            [
                new() { RuleId = 1, OwnerId = 10, Created = 100, RuleMetadataId = 1001, OwnerMappingSourceId = 2 },
                new() { RuleId = 2, OwnerId = 20, Created = 100, RuleMetadataId = 1002, OwnerMappingSourceId = 2 }
            ];

            await service.CallInsertNewRuleOwners(ruleOwners);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastQuery, Is.EqualTo(FWO.Api.Client.Queries.OwnerQueries.insertRuleOwners));
                Assert.That(apiConnection.LastChunkingOptions, Is.Not.Null);
                Assert.That(apiConnection.LastChunkingOptions!.Enabled, Is.True);
                Assert.That(apiConnection.LastChunkingOptions.ChunkVariableName, Is.EqualTo("objects"));
                Assert.That(apiConnection.LastChunkingOptions.ChunkSize, Is.EqualTo(500));
            });
        }

        #region Test Data Builders
        private static Rule BuildRule(
            int id = 1,
            string customFields = "{'owner':'TeamA'}",
            long metadataId = 100,
            NetworkLocation[]? froms = null)
        {
            return new Rule
            {
                Id = id,
                CustomFields = customFields,
                Metadata = new RuleMetadata { Id = metadataId },
                Froms = froms ?? Array.Empty<NetworkLocation>()
            };
        }

        private static FwoOwner BuildOwner(
            int id = 10,
            string extAppId = "TeamA",
            OwnerNetwork[]? ownerNetworks = null)
        {
            return new FwoOwner
            {
                Id = id,
                ExtAppId = extAppId,
                OwnerNetworks = ownerNetworks ?? Array.Empty<OwnerNetwork>()
            };
        }

        private static NetworkLocation BuildNetworkLocation(string ipStart, string ipEnd)
        {
            return new NetworkLocation(
                new NetworkUser { },
                new NetworkObject { IP = ipStart, IpEnd = ipEnd }
            );
        }

        private static OwnerNetwork BuildOwnerNetwork(string ipStart, string ipEnd)
        {
            return new OwnerNetwork { IP = ipStart, IpEnd = ipEnd };
        }

        private sealed class TestUpdateRuleOwnerMappingCustomField : UpdateRuleOwnerMappingCustomField
        {
            public TestUpdateRuleOwnerMappingCustomField(ApiConnection apiConnection, GlobalConfig globalConfig)
                : base(apiConnection, globalConfig)
            {
            }

            public Task CallSetAffectedRuleOwnersRemoved(List<RuleOwner> ruleOwnersToSetRemoved, long importControlId)
            {
                return SetAffectedRuleOwnersRemoved(ruleOwnersToSetRemoved, importControlId);
            }

            public Task CallInsertNewRuleOwners(List<RuleOwner> ruleOwners)
            {
                return InsertNewRuleOwners(ruleOwners);
            }
        }

        private sealed class RecordingChunkingApiConnection : SimulatedApiConnection
        {
            public string? LastQuery { get; private set; }
            public FWO.Api.Client.QueryChunkingOptions? LastChunkingOptions { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
                string query,
                object? variables = null,
                string? operationName = null,
                FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                LastChunkingOptions = chunkingOptions;
                return Task.FromResult(default(QueryResponseType)!);
            }
        }

        private sealed class UpdateRuleOwnerMappingNameFieldApiConnection : SimulatedApiConnection
        {
            public List<OwnerChange> ChangedOwners { get; set; } = [];
            public List<Rule> RulesToMap { get; set; } = [];
            public List<ModellingConnection> ConnectionOwners { get; set; } = [];
            public List<RuleOwner> RuleOwnersToRemove { get; set; } = [];
            public int NameFieldRuleQueryCalls { get; private set; }
            public int FilteredConnectionQueryCalls { get; private set; }
            public List<int> FilteredOwnerIds { get; private set; } = [];
            public List<int> RemovedOwnerIds { get; private set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == FWO.Api.Client.Queries.OwnerQueries.getChangedOwnersForRuleOwnerMappingNameField)
                {
                    return Task.FromResult((QueryResponseType)(object)ChangedOwners);
                }

                if (query == FWO.Api.Client.Queries.RuleQueries.getRulesForRuleOwnerNameField)
                {
                    ++NameFieldRuleQueryCalls;
                    return Task.FromResult((QueryResponseType)(object)RulesToMap.ToList());
                }

                if (query == FWO.Api.Client.Queries.ModellingQueries.getOwnersForRuleOwnerNameFieldFilteredByOwner)
                {
                    ++FilteredConnectionQueryCalls;
                    FilteredOwnerIds = ReadOwnerIds(variables);
                    return Task.FromResult((QueryResponseType)(object)ConnectionOwners);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getRuleOwnerToRemoveByOwner)
                {
                    RemovedOwnerIds = ReadOwnerIds(variables);
                    return Task.FromResult((QueryResponseType)(object)RuleOwnersToRemove);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            private static List<int> ReadOwnerIds(object? variables)
            {
                object? value = variables?.GetType().GetProperty("ownerIds")?.GetValue(variables);
                return value as List<int> ?? [];
            }
        }

        private sealed class IncrementalCustomFieldApiConnection : SimulatedApiConnection
        {
            private readonly List<ImportControl> pendingImports;
            private readonly Dictionary<long, List<RuleChange>> ruleChangesByImport;
            private readonly Dictionary<long, List<OwnerChange>> ownerChangesByImport;
            private readonly Dictionary<long, List<Rule>> rulesByImport;
            private readonly Dictionary<long, List<FwoOwner>> ownersByImport;
            private readonly List<RuleOwner> activeRuleOwners = [];
            private long currentImportId;

            public List<long> CompletedImports { get; } = [];
            public Dictionary<long, List<string>> InsertedPairsByImport { get; } = [];
            public Dictionary<long, List<string>> ActivePairsAfterImport { get; } = [];

            public IncrementalCustomFieldApiConnection(int pendingImportCount = 10)
            {
                List<ImportControl> allPendingImports =
                [
                    NewImport(1, FWO.Basics.ImportType.RULE),
                    NewImport(2, FWO.Basics.ImportType.OWNER),
                    NewImport(3, FWO.Basics.ImportType.RULE),
                    NewImport(4, FWO.Basics.ImportType.RULE),
                    NewImport(5, FWO.Basics.ImportType.OWNER),
                    NewImport(6, FWO.Basics.ImportType.RULE),
                    NewImport(7, FWO.Basics.ImportType.OWNER),
                    NewImport(8, FWO.Basics.ImportType.RULE),
                    NewImport(9, FWO.Basics.ImportType.OWNER),
                    NewImport(10, FWO.Basics.ImportType.RULE)
                ];
                pendingImports = [.. allPendingImports.Take(pendingImportCount)];

                Dictionary<int, string> rules1 = new()
                {
                    [1] = "A",
                    [2] = "B",
                    [3] = "C",
                    [4] = "D",
                    [5] = "E",
                    [6] = "A",
                    [7] = "B",
                    [8] = "C",
                    [9] = "D",
                    [10] = "E"
                };
                Dictionary<int, string> rules3 = new(rules1) { [1] = "B", [2] = "Unknown", [11] = "C", [12] = "F" };
                Dictionary<int, string> rules4 = new(rules3) { [3] = "A", [4] = "E", [13] = "B" };
                Dictionary<int, string> rules6 = new(rules4) { [6] = "F", [7] = "D", [14] = "A" };
                Dictionary<int, string> rules8 = new(rules6) { [8] = "E", [9] = "Unknown", [10] = "B", [15] = "C" };
                Dictionary<int, string> rules10 = new(rules8) { [5] = "C", [12] = "A", [16] = "F" };

                Dictionary<int, string> owners2 = new() { [1] = "A", [2] = "B", [3] = "C", [4] = "D", [5] = "E" };
                Dictionary<int, string> owners5 = new(owners2) { [6] = "F" };
                Dictionary<int, string> owners7 = new() { [1] = "A", [2] = "B", [4] = "D", [5] = "E", [6] = "F" };
                Dictionary<int, string> owners9 = new(owners7) { [3] = "C" };

                rulesByImport = new()
                {
                    [1] = BuildRules(rules1),
                    [2] = BuildRules(rules1),
                    [3] = BuildRules(rules3),
                    [4] = BuildRules(rules4),
                    [5] = BuildRules(rules4),
                    [6] = BuildRules(rules6),
                    [7] = BuildRules(rules6),
                    [8] = BuildRules(rules8),
                    [9] = BuildRules(rules8),
                    [10] = BuildRules(rules10)
                };

                ownersByImport = new()
                {
                    [1] = [],
                    [2] = BuildOwners(owners2),
                    [3] = BuildOwners(owners2),
                    [4] = BuildOwners(owners2),
                    [5] = BuildOwners(owners5),
                    [6] = BuildOwners(owners5),
                    [7] = BuildOwners(owners7),
                    [8] = BuildOwners(owners7),
                    [9] = BuildOwners(owners9),
                    [10] = BuildOwners(owners9)
                };

                ruleChangesByImport = new()
                {
                    [1] = rules1.Select(entry => NewRuleInsert(entry.Key, entry.Value)).ToList(),
                    [3] =
                    [
                        NewRuleChange(1, "A", "B"),
                        NewRuleChange(2, "B", "Unknown"),
                        NewRuleInsert(11, "C"),
                        NewRuleInsert(12, "F")
                    ],
                    [4] =
                    [
                        NewRuleChange(3, "C", "A"),
                        NewRuleChange(4, "D", "E"),
                        NewRuleInsert(13, "B")
                    ],
                    [6] =
                    [
                        NewRuleChange(6, "A", "F"),
                        NewRuleChange(7, "B", "D"),
                        NewRuleInsert(14, "A")
                    ],
                    [8] =
                    [
                        NewRuleChange(8, "C", "E"),
                        NewRuleChange(9, "D", "Unknown"),
                        NewRuleChange(10, "E", "B"),
                        NewRuleInsert(15, "C")
                    ],
                    [10] =
                    [
                        NewRuleChange(5, "E", "C"),
                        NewRuleChange(12, "F", "A"),
                        NewRuleInsert(16, "F")
                    ]
                };

                ownerChangesByImport = new()
                {
                    [2] = owners2.Select(entry => NewOwnerInsert(entry.Key, entry.Value)).ToList(),
                    [5] = [NewOwnerInsert(6, "F")],
                    [7] = [NewOwnerDelete(3, "C")],
                    [9] =
                    [
                        NewOwnerChange(2, "B", "B"),
                        NewOwnerReactivate(3, "C")
                    ]
                };
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == FWO.Api.Client.Queries.ImportQueries.getPendingRuleOwnerImports)
                {
                    return Task.FromResult((QueryResponseType)(object)pendingImports.Where(import => !CompletedImports.Contains(import.ControlId)).ToList());
                }

                if (query == FWO.Api.Client.Queries.RuleQueries.getChangedRulesForRuleOwnerMappingCustomField)
                {
                    currentImportId = ReadLong(variables, "controlId");
                    return Task.FromResult((QueryResponseType)(object)(ruleChangesByImport.TryGetValue(currentImportId, out List<RuleChange>? changes) ? changes : new List<RuleChange>()));
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getChangedOwnersForRuleOwnerMappingCustomField)
                {
                    currentImportId = ReadLong(variables, "controlId");
                    return Task.FromResult((QueryResponseType)(object)(ownerChangesByImport.TryGetValue(currentImportId, out List<OwnerChange>? changes) ? changes : new List<OwnerChange>()));
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getOwnersForRuleOwnerCustomField)
                {
                    return Task.FromResult((QueryResponseType)(object)ownersByImport[currentImportId]);
                }

                if (query == FWO.Api.Client.Queries.RuleQueries.getRulesForRuleOwnerCustomField)
                {
                    return Task.FromResult((QueryResponseType)(object)rulesByImport[currentImportId]);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getRuleOwnerToRemoveByRule)
                {
                    List<long> ruleIds = ReadLongList(variables, "ruleIds");
                    return Task.FromResult((QueryResponseType)(object)activeRuleOwners.Where(ruleOwner => ruleIds.Contains(ruleOwner.RuleId)).Select(CloneRuleOwner).ToList());
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getRuleOwnerToRemoveByOwner)
                {
                    List<int> ownerIds = ReadIntList(variables, "ownerIds");
                    return Task.FromResult((QueryResponseType)(object)activeRuleOwners.Where(ruleOwner => ownerIds.Contains(ruleOwner.OwnerId)).Select(CloneRuleOwner).ToList());
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.setAffectedRuleOwnersRemoved)
                {
                    RemoveAffectedRuleOwners(variables);
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.insertRuleOwners)
                {
                    InsertRuleOwners(variables);
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == FWO.Api.Client.Queries.ImportQueries.updateImportControlForRuleOwnerInc)
                {
                    long controlId = ReadLong(variables, "controlId");
                    InsertedPairsByImport.TryAdd(controlId, []);
                    CompletedImports.Add(controlId);
                    ActivePairsAfterImport[controlId] = activeRuleOwners.Select(ruleOwner => $"{ruleOwner.RuleId}->{ruleOwner.OwnerId}").OrderBy(pair => pair).ToList();
                    return Task.FromResult(default(QueryResponseType)!);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            private void InsertRuleOwners(object? variables)
            {
                List<RuleOwner> objects = ReadRuleOwners(variables, "objects");
                InsertedPairsByImport[currentImportId] = objects.Select(ruleOwner => $"{ruleOwner.RuleId}->{ruleOwner.OwnerId}").OrderBy(pair => pair).ToList();
                foreach (RuleOwner ruleOwner in objects)
                {
                    activeRuleOwners.Add(CloneRuleOwner(ruleOwner));
                }
            }

            private void RemoveAffectedRuleOwners(object? variables)
            {
                object? objects = variables?.GetType().GetProperty("objects")?.GetValue(variables);
                if (objects is not System.Collections.IEnumerable enumerable)
                {
                    return;
                }

                foreach (object entry in enumerable)
                {
                    long ruleId = ReadNestedLong(entry, "rule_id");
                    int ownerId = (int)ReadNestedLong(entry, "owner_id");
                    long created = ReadNestedLong(entry, "created");
                    activeRuleOwners.RemoveAll(ruleOwner => ruleOwner.RuleId == ruleId && ruleOwner.OwnerId == ownerId && ruleOwner.Created == created);
                }
            }

            private static ImportControl NewImport(long id, int importTypeId)
            {
                return new ImportControl { ControlId = id, ImportTypeId = importTypeId };
            }

            private static List<Rule> BuildRules(Dictionary<int, string> mapping)
            {
                return mapping.Select(entry => new Rule
                {
                    Id = entry.Key,
                    CustomFields = "{'owner':'" + entry.Value + "'}",
                    Metadata = new RuleMetadata { Id = 1000 + entry.Key }
                }).ToList();
            }

            private static List<FwoOwner> BuildOwners(Dictionary<int, string> mapping)
            {
                return mapping.Select(entry => new FwoOwner { Id = entry.Key, ExtAppId = entry.Value }).ToList();
            }

            private static RuleChange NewRuleInsert(int ruleId, string ownerKey)
            {
                return new RuleChange
                {
                    ChangeAction = 'I',
                    NewRule = new Rule
                    {
                        Id = ruleId,
                        CustomFields = "{'owner':'" + ownerKey + "'}",
                        Metadata = new RuleMetadata { Id = 1000 + ruleId }
                    }
                };
            }

            private static RuleChange NewRuleChange(int ruleId, string oldOwnerKey, string newOwnerKey)
            {
                return new RuleChange
                {
                    ChangeAction = 'C',
                    OldRule = new Rule
                    {
                        Id = ruleId,
                        CustomFields = "{'owner':'" + oldOwnerKey + "'}",
                        Metadata = new RuleMetadata { Id = 1000 + ruleId }
                    },
                    NewRule = new Rule
                    {
                        Id = ruleId,
                        CustomFields = "{'owner':'" + newOwnerKey + "'}",
                        Metadata = new RuleMetadata { Id = 1000 + ruleId }
                    }
                };
            }

            private static OwnerChange NewOwnerInsert(int ownerId, string extAppId)
            {
                return new OwnerChange
                {
                    ChangeAction = 'I',
                    NewOwner = new FwoOwner { Id = ownerId, ExtAppId = extAppId }
                };
            }

            private static OwnerChange NewOwnerDelete(int ownerId, string extAppId)
            {
                return new OwnerChange
                {
                    ChangeAction = 'D',
                    OldOwner = new FwoOwner { Id = ownerId, ExtAppId = extAppId }
                };
            }

            private static OwnerChange NewOwnerReactivate(int ownerId, string extAppId)
            {
                return new OwnerChange
                {
                    ChangeAction = 'R',
                    NewOwner = new FwoOwner { Id = ownerId, ExtAppId = extAppId }
                };
            }

            private static OwnerChange NewOwnerChange(int ownerId, string oldExtAppId, string newExtAppId)
            {
                return new OwnerChange
                {
                    ChangeAction = 'C',
                    OldOwner = new FwoOwner { Id = ownerId, ExtAppId = oldExtAppId },
                    NewOwner = new FwoOwner { Id = ownerId, ExtAppId = newExtAppId }
                };
            }

            private static RuleOwner CloneRuleOwner(RuleOwner source)
            {
                return new RuleOwner
                {
                    RuleId = source.RuleId,
                    OwnerId = source.OwnerId,
                    Created = source.Created,
                    RuleMetadataId = source.RuleMetadataId,
                    OwnerMappingSourceId = source.OwnerMappingSourceId
                };
            }

            private static long ReadLong(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    _ => throw new InvalidOperationException($"Missing long property '{propertyName}'.")
                };
            }

            private static List<long> ReadLongList(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value as List<long> ?? [];
            }

            private static List<int> ReadIntList(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value as List<int> ?? [];
            }

            private static List<RuleOwner> ReadRuleOwners(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value as List<RuleOwner> ?? [];
            }

            private static long ReadNestedLong(object source, string propertyName)
            {
                object? wrapper = source.GetType().GetProperty(propertyName)?.GetValue(source);
                object? eqValue = wrapper?.GetType().GetProperty("_eq")?.GetValue(wrapper);
                return eqValue switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    _ => throw new InvalidOperationException($"Missing nested _eq value for '{propertyName}'.")
                };
            }
        }

        private sealed class FallbackToFullReinitializeCustomFieldApiConnection : SimulatedApiConnection
        {
            private readonly List<ImportControl> pendingImports =
            [
                CreateImport(1, FWO.Basics.ImportType.RULE),
                CreateImport(2, FWO.Basics.ImportType.OWNER),
                CreateImport(3, FWO.Basics.ImportType.RULE),
                CreateImport(4, FWO.Basics.ImportType.RULE),
                CreateImport(5, FWO.Basics.ImportType.OWNER),
                CreateImport(6, FWO.Basics.ImportType.RULE),
                CreateImport(7, FWO.Basics.ImportType.OWNER),
                CreateImport(8, FWO.Basics.ImportType.RULE),
                CreateImport(9, FWO.Basics.ImportType.OWNER),
                CreateImport(10, FWO.Basics.ImportType.RULE)
            ];
            private readonly List<RuleOwner> activeRuleOwners = [];
            private readonly List<Rule> rules;
            private readonly List<FwoOwner> owners;

            public List<long> CompletedImports { get; } = [];
            public bool FullReinitializeTriggered { get; private set; }
            public long FullReinitializeImportControlId { get; private set; }
            public List<string> ActivePairsAfterFullReinitialize { get; private set; } = [];

            public FallbackToFullReinitializeCustomFieldApiConnection()
            {
                Dictionary<int, string> rules10 = new()
                {
                    [1] = "B",
                    [2] = "Unknown",
                    [3] = "A",
                    [4] = "E",
                    [5] = "C",
                    [6] = "F",
                    [7] = "D",
                    [8] = "E",
                    [9] = "Unknown",
                    [10] = "B",
                    [11] = "C",
                    [12] = "A",
                    [13] = "B",
                    [14] = "A",
                    [15] = "C",
                    [16] = "F"
                };
                Dictionary<int, string> owners9 = new()
                {
                    [1] = "A",
                    [2] = "B",
                    [3] = "C",
                    [4] = "D",
                    [5] = "E",
                    [6] = "F"
                };

                rules = CreateRules(rules10);
                owners = CreateOwners(owners9);
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == FWO.Api.Client.Queries.ImportQueries.getPendingRuleOwnerImports)
                {
                    return Task.FromResult((QueryResponseType)(object)pendingImports.Where(import => !CompletedImports.Contains(import.ControlId)).ToList());
                }

                if (query == FWO.Api.Client.Queries.ImportQueries.addImportForRuleOwner)
                {
                    FullReinitializeTriggered = true;
                    return Task.FromResult((QueryResponseType)(object)new InsertImportControl
                    {
                        Returning = [new ImportControl { ControlId = 100 }]
                    });
                }

                if (query == FWO.Api.Client.Queries.RuleQueries.getRulesForOwnerMappingCustomField)
                {
                    return Task.FromResult((QueryResponseType)(object)rules);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getOwnersForRuleOwnerCustomField)
                {
                    return Task.FromResult((QueryResponseType)(object)owners);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.setAllActiveRuleOwnersRemoved)
                {
                    activeRuleOwners.Clear();
                    FullReinitializeImportControlId = ReadLong(variables, "controlId");
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.insertRuleOwners)
                {
                    foreach (RuleOwner ruleOwner in ReadObjects(variables, "objects"))
                    {
                        activeRuleOwners.Add(Clone(ruleOwner));
                    }
                    ActivePairsAfterFullReinitialize = activeRuleOwners.Select(ruleOwner => $"{ruleOwner.RuleId}->{ruleOwner.OwnerId}").OrderBy(pair => pair).ToList();
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == FWO.Api.Client.Queries.ImportQueries.updateImportControlForRuleOwnerFull)
                {
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == FWO.Api.Client.Queries.ImportQueries.updateImportControlForRuleOwnerInc)
                {
                    CompletedImports.Add(ReadLong(variables, "controlId"));
                    return Task.FromResult(default(QueryResponseType)!);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            private static ImportControl CreateImport(long id, int importTypeId)
            {
                return new ImportControl { ControlId = id, ImportTypeId = importTypeId };
            }

            private static List<Rule> CreateRules(Dictionary<int, string> mapping)
            {
                return mapping.Select(entry => new Rule
                {
                    Id = entry.Key,
                    CustomFields = "{'owner':'" + entry.Value + "'}",
                    Metadata = new RuleMetadata { Id = 1000 + entry.Key }
                }).ToList();
            }

            private static List<FwoOwner> CreateOwners(Dictionary<int, string> mapping)
            {
                return mapping.Select(entry => new FwoOwner { Id = entry.Key, ExtAppId = entry.Value }).ToList();
            }

            private static RuleOwner Clone(RuleOwner source)
            {
                return new RuleOwner
                {
                    RuleId = source.RuleId,
                    OwnerId = source.OwnerId,
                    Created = source.Created,
                    RuleMetadataId = source.RuleMetadataId,
                    OwnerMappingSourceId = source.OwnerMappingSourceId
                };
            }

            private static long ReadLong(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    _ => throw new InvalidOperationException($"Missing long property '{propertyName}'.")
                };
            }

            private static List<RuleOwner> ReadObjects(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value as List<RuleOwner> ?? [];
            }
        }

        private sealed class InitialOrderCustomFieldApiConnection : SimulatedApiConnection
        {
            private readonly List<ImportControl> pendingImports;
            private readonly Dictionary<long, List<RuleChange>> ruleChangesByImport;
            private readonly Dictionary<long, List<OwnerChange>> ownerChangesByImport;
            private readonly Dictionary<long, List<Rule>> rulesByImport;
            private readonly Dictionary<long, List<FwoOwner>> ownersByImport;
            private readonly List<RuleOwner> activeRuleOwners = [];
            private long currentImportId;

            public List<long> CompletedImports { get; } = [];
            public Dictionary<long, List<string>> ActivePairsAfterImport { get; } = [];

            public InitialOrderCustomFieldApiConnection(bool ruleImportFirst)
            {
                Dictionary<int, string> rules = new()
                {
                    [1] = "A",
                    [2] = "B",
                    [3] = "C",
                    [4] = "A",
                    [5] = "B",
                    [6] = "C",
                    [7] = "A",
                    [8] = "B",
                    [9] = "C",
                    [10] = "A"
                };
                Dictionary<int, string> owners = new()
                {
                    [1] = "A",
                    [2] = "B",
                    [3] = "C"
                };

                pendingImports = ruleImportFirst
                    ?
                    [
                        NewImport(1, FWO.Basics.ImportType.RULE),
                        NewImport(2, FWO.Basics.ImportType.OWNER)
                    ]
                    :
                    [
                        NewImport(1, FWO.Basics.ImportType.OWNER),
                        NewImport(2, FWO.Basics.ImportType.RULE)
                    ];

                ruleChangesByImport = ruleImportFirst
                    ? new()
                    {
                        [1] = rules.Select(entry => NewRuleInsert(entry.Key, entry.Value)).ToList(),
                        [2] = []
                    }
                    : new()
                    {
                        [1] = [],
                        [2] = rules.Select(entry => NewRuleInsert(entry.Key, entry.Value)).ToList()
                    };

                ownerChangesByImport = ruleImportFirst
                    ? new()
                    {
                        [1] = [],
                        [2] = owners.Select(entry => NewOwnerInsert(entry.Key, entry.Value)).ToList()
                    }
                    : new()
                    {
                        [1] = owners.Select(entry => NewOwnerInsert(entry.Key, entry.Value)).ToList(),
                        [2] = []
                    };

                rulesByImport = ruleImportFirst
                    ? new()
                    {
                        [1] = BuildRules(rules),
                        [2] = BuildRules(rules)
                    }
                    : new()
                    {
                        [1] = [],
                        [2] = BuildRules(rules)
                    };

                ownersByImport = ruleImportFirst
                    ? new()
                    {
                        [1] = [],
                        [2] = BuildOwners(owners)
                    }
                    : new()
                    {
                        [1] = BuildOwners(owners),
                        [2] = BuildOwners(owners)
                    };
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == FWO.Api.Client.Queries.ImportQueries.getPendingRuleOwnerImports)
                {
                    return Task.FromResult((QueryResponseType)(object)pendingImports.Where(import => !CompletedImports.Contains(import.ControlId)).ToList());
                }

                if (query == FWO.Api.Client.Queries.RuleQueries.getChangedRulesForRuleOwnerMappingCustomField)
                {
                    currentImportId = ReadLong(variables, "controlId");
                    return Task.FromResult((QueryResponseType)(object)ruleChangesByImport[currentImportId]);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getChangedOwnersForRuleOwnerMappingCustomField)
                {
                    currentImportId = ReadLong(variables, "controlId");
                    return Task.FromResult((QueryResponseType)(object)ownerChangesByImport[currentImportId]);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getOwnersForRuleOwnerCustomField)
                {
                    return Task.FromResult((QueryResponseType)(object)ownersByImport[currentImportId]);
                }

                if (query == FWO.Api.Client.Queries.RuleQueries.getRulesForRuleOwnerCustomField)
                {
                    return Task.FromResult((QueryResponseType)(object)rulesByImport[currentImportId]);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getRuleOwnerToRemoveByRule)
                {
                    List<long> ruleIds = ReadLongList(variables, "ruleIds");
                    return Task.FromResult((QueryResponseType)(object)activeRuleOwners.Where(ruleOwner => ruleIds.Contains(ruleOwner.RuleId)).Select(CloneRuleOwner).ToList());
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getRuleOwnerToRemoveByOwner)
                {
                    List<int> ownerIds = ReadIntList(variables, "ownerIds");
                    return Task.FromResult((QueryResponseType)(object)activeRuleOwners.Where(ruleOwner => ownerIds.Contains(ruleOwner.OwnerId)).Select(CloneRuleOwner).ToList());
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.setAffectedRuleOwnersRemoved)
                {
                    RemoveAffectedRuleOwners(variables);
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.insertRuleOwners)
                {
                    foreach (RuleOwner ruleOwner in ReadRuleOwners(variables, "objects"))
                    {
                        activeRuleOwners.Add(CloneRuleOwner(ruleOwner));
                    }
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == FWO.Api.Client.Queries.ImportQueries.updateImportControlForRuleOwnerInc)
                {
                    long controlId = ReadLong(variables, "controlId");
                    CompletedImports.Add(controlId);
                    ActivePairsAfterImport[controlId] = activeRuleOwners.Select(ruleOwner => $"{ruleOwner.RuleId}->{ruleOwner.OwnerId}").OrderBy(pair => pair).ToList();
                    return Task.FromResult(default(QueryResponseType)!);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            private void RemoveAffectedRuleOwners(object? variables)
            {
                object? objects = variables?.GetType().GetProperty("objects")?.GetValue(variables);
                if (objects is not System.Collections.IEnumerable enumerable)
                {
                    return;
                }

                foreach (object entry in enumerable)
                {
                    long ruleId = ReadNestedLong(entry, "rule_id");
                    int ownerId = (int)ReadNestedLong(entry, "owner_id");
                    long created = ReadNestedLong(entry, "created");
                    activeRuleOwners.RemoveAll(ruleOwner => ruleOwner.RuleId == ruleId && ruleOwner.OwnerId == ownerId && ruleOwner.Created == created);
                }
            }

            private static ImportControl NewImport(long id, int importTypeId)
            {
                return new ImportControl { ControlId = id, ImportTypeId = importTypeId };
            }

            private static List<Rule> BuildRules(Dictionary<int, string> mapping)
            {
                return mapping.Select(entry => new Rule
                {
                    Id = entry.Key,
                    CustomFields = "{'owner':'" + entry.Value + "'}",
                    Metadata = new RuleMetadata { Id = 1000 + entry.Key }
                }).ToList();
            }

            private static List<FwoOwner> BuildOwners(Dictionary<int, string> mapping)
            {
                return mapping.Select(entry => new FwoOwner { Id = entry.Key, ExtAppId = entry.Value }).ToList();
            }

            private static RuleChange NewRuleInsert(int ruleId, string ownerKey)
            {
                return new RuleChange
                {
                    ChangeAction = 'I',
                    NewRule = new Rule
                    {
                        Id = ruleId,
                        CustomFields = "{'owner':'" + ownerKey + "'}",
                        Metadata = new RuleMetadata { Id = 1000 + ruleId }
                    }
                };
            }

            private static OwnerChange NewOwnerInsert(int ownerId, string extAppId)
            {
                return new OwnerChange
                {
                    ChangeAction = 'I',
                    NewOwner = new FwoOwner { Id = ownerId, ExtAppId = extAppId }
                };
            }

            private static RuleOwner CloneRuleOwner(RuleOwner source)
            {
                return new RuleOwner
                {
                    RuleId = source.RuleId,
                    OwnerId = source.OwnerId,
                    Created = source.Created,
                    RuleMetadataId = source.RuleMetadataId,
                    OwnerMappingSourceId = source.OwnerMappingSourceId
                };
            }

            private static long ReadLong(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    _ => throw new InvalidOperationException($"Missing long property '{propertyName}'.")
                };
            }

            private static List<long> ReadLongList(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value as List<long> ?? [];
            }

            private static List<int> ReadIntList(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value as List<int> ?? [];
            }

            private static List<RuleOwner> ReadRuleOwners(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value as List<RuleOwner> ?? [];
            }

            private static long ReadNestedLong(object source, string propertyName)
            {
                object? wrapper = source.GetType().GetProperty(propertyName)?.GetValue(source);
                object? eqValue = wrapper?.GetType().GetProperty("_eq")?.GetValue(wrapper);
                return eqValue switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    _ => throw new InvalidOperationException($"Missing nested _eq value for '{propertyName}'.")
                };
            }
        }

        private sealed class DuplicateInsertGuardCustomFieldApiConnection : SimulatedApiConnection
        {
            private readonly List<ImportControl> pendingImports =
            [
                new() { ControlId = 1, ImportTypeId = FWO.Basics.ImportType.RULE },
                new() { ControlId = 2, ImportTypeId = FWO.Basics.ImportType.OWNER },
                new() { ControlId = 3, ImportTypeId = FWO.Basics.ImportType.OWNER }
            ];
            private readonly List<RuleOwner> activeRuleOwners = [];
            private long currentImportId;
            private readonly Rule trackedRule = new()
            {
                Id = 101,
                CustomFields = "{'owner':'A'}",
                Metadata = new RuleMetadata { Id = 1101 }
            };
            private readonly FwoOwner trackedOwner = new() { Id = 1, ExtAppId = "A" };
            private readonly List<long> completedImports = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == FWO.Api.Client.Queries.ImportQueries.getPendingRuleOwnerImports)
                {
                    return Task.FromResult((QueryResponseType)(object)pendingImports.Where(import => !completedImports.Contains(import.ControlId)).ToList());
                }

                if (query == FWO.Api.Client.Queries.RuleQueries.getChangedRulesForRuleOwnerMappingCustomField)
                {
                    currentImportId = ReadLong(variables, "controlId");
                    List<RuleChange> changes = currentImportId == 1
                        ? [new RuleChange { ChangeAction = 'I', NewRule = trackedRule }]
                        : [];
                    return Task.FromResult((QueryResponseType)(object)changes);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getChangedOwnersForRuleOwnerMappingCustomField)
                {
                    currentImportId = ReadLong(variables, "controlId");
                    List<OwnerChange> changes = currentImportId switch
                    {
                        2 => [new OwnerChange { ChangeAction = 'I', NewOwner = trackedOwner }],
                        3 => [new OwnerChange { ChangeAction = 'C', OldOwner = trackedOwner, NewOwner = trackedOwner }],
                        _ => []
                    };
                    return Task.FromResult((QueryResponseType)(object)changes);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getOwnersForRuleOwnerCustomField)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoOwner> { trackedOwner });
                }

                if (query == FWO.Api.Client.Queries.RuleQueries.getRulesForRuleOwnerCustomField)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Rule> { trackedRule });
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getRuleOwnerToRemoveByRule)
                {
                    List<long> ruleIds = ReadLongList(variables, "ruleIds");
                    return Task.FromResult((QueryResponseType)(object)activeRuleOwners.Where(ruleOwner => ruleIds.Contains(ruleOwner.RuleId)).Select(CloneRuleOwner).ToList());
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.getRuleOwnerToRemoveByOwner)
                {
                    List<int> ownerIds = ReadIntList(variables, "ownerIds");
                    return Task.FromResult((QueryResponseType)(object)activeRuleOwners.Where(ruleOwner => ownerIds.Contains(ruleOwner.OwnerId)).Select(CloneRuleOwner).ToList());
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.setAffectedRuleOwnersRemoved)
                {
                    RemoveAffectedRuleOwners(variables);
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == FWO.Api.Client.Queries.OwnerQueries.insertRuleOwners)
                {
                    foreach (RuleOwner ruleOwner in ReadRuleOwners(variables, "objects"))
                    {
                        if (activeRuleOwners.Any(existing => existing.RuleId == ruleOwner.RuleId && existing.OwnerId == ruleOwner.OwnerId))
                        {
                            throw new InvalidOperationException($"Duplicate active rule_owner pair detected for rule {ruleOwner.RuleId} and owner {ruleOwner.OwnerId}.");
                        }
                        activeRuleOwners.Add(CloneRuleOwner(ruleOwner));
                    }
                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == FWO.Api.Client.Queries.ImportQueries.updateImportControlForRuleOwnerInc)
                {
                    completedImports.Add(ReadLong(variables, "controlId"));
                    return Task.FromResult(default(QueryResponseType)!);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            private void RemoveAffectedRuleOwners(object? variables)
            {
                object? objects = variables?.GetType().GetProperty("objects")?.GetValue(variables);
                if (objects is not System.Collections.IEnumerable enumerable)
                {
                    return;
                }

                foreach (object entry in enumerable)
                {
                    long ruleId = ReadNestedLong(entry, "rule_id");
                    int ownerId = (int)ReadNestedLong(entry, "owner_id");
                    long created = ReadNestedLong(entry, "created");
                    activeRuleOwners.RemoveAll(ruleOwner => ruleOwner.RuleId == ruleId && ruleOwner.OwnerId == ownerId && ruleOwner.Created == created);
                }
            }

            private static RuleOwner CloneRuleOwner(RuleOwner source)
            {
                return new RuleOwner
                {
                    RuleId = source.RuleId,
                    OwnerId = source.OwnerId,
                    Created = source.Created,
                    RuleMetadataId = source.RuleMetadataId,
                    OwnerMappingSourceId = source.OwnerMappingSourceId
                };
            }

            private static long ReadLong(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    _ => throw new InvalidOperationException($"Missing long property '{propertyName}'.")
                };
            }

            private static List<long> ReadLongList(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value as List<long> ?? [];
            }

            private static List<int> ReadIntList(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value as List<int> ?? [];
            }

            private static List<RuleOwner> ReadRuleOwners(object? variables, string propertyName)
            {
                object? value = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
                return value as List<RuleOwner> ?? [];
            }

            private static long ReadNestedLong(object source, string propertyName)
            {
                object? wrapper = source.GetType().GetProperty(propertyName)?.GetValue(source);
                object? eqValue = wrapper?.GetType().GetProperty("_eq")?.GetValue(wrapper);
                return eqValue switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    _ => throw new InvalidOperationException($"Missing nested _eq value for '{propertyName}'.")
                };
            }
        }

        #endregion
    }
}
