using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Flow;
using FWO.Services;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowHashRecalculatorTest
    {
        private const string kSourceIp = "10.0.0.1";
        private const string kDestinationIp = "10.0.1.1";
        private const string kUnchangedIp = "10.0.2.1";
        private const string kStaleHashOne = "1111111111111111111111111111111111111111111111111111111111111111";
        private const string kStaleHashTwo = "2222222222222222222222222222222222222222222222222222222222222222";
        private const string kStaleHashThree = "3333333333333333333333333333333333333333333333333333333333333333";
        private const string kRandomHash = "8bf9f27b21be47a2b1f6a6cf9e3ac0f1";
        private const int kTcpProtoId = 6;
        private const int kIcmpProtoId = 1;
        private const int kHttpPort = 80;
        private const int kHttpsPort = 443;
        private const int kCentralEuropeanOffsetHours = 1;
        private static readonly List<long> kBothNwObjectIds = [1, 2];

        private sealed class FlowHashRecalculatorTestApiConn : SimulatedApiConnection
        {
            public List<object?> HashUpdateVariables { get; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == FlowQueries.updateFlowHashes)
                {
                    HashUpdateVariables.Add(variables);
                    return Task.FromResult((T)(object)new List<MutationResult> { new() { AffectedRows = 1 } });
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        [Test]
        public async Task RecalculateFlowHashesAsync_RecalculatesTechnicalHashesAndKeepsRandomHashes()
        {
            FlowNwObject staleObject = CreateFlowNwObject(1, kSourceIp, kStaleHashOne);
            FlowNwObject dynamicObject = new() { Id = 2, Hash = kRandomHash };
            FlowNwObject unchangedObject = CreateFlowNwObject(3, kUnchangedIp, FlowHashGenerator.GenerateNwObjectHash(kUnchangedIp, kUnchangedIp));
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [staleObject, dynamicObject, unchangedObject]);
            FlowHashRecalculatorTestApiConn apiConn = new();
            FlowHashRecalculator recalculator = new(apiConn);

            FlowHashRecalculationOutcome outcome = await recalculator.RecalculateFlowHashesAsync(flowData);

            object? variables = apiConn.HashUpdateVariables.Single();
            List<(long Id, string Hash)> updates = ReadUpdates(variables, "nwObjectHashes", "nwobj_id", "nwobj_hash");
            Assert.Multiple(() =>
            {
                Assert.That(outcome, Is.EqualTo(FlowHashRecalculationOutcome.Updated));
                Assert.That(updates, Has.Count.EqualTo(1));
                Assert.That(updates[0].Id, Is.EqualTo(1));
                Assert.That(updates[0].Hash, Is.EqualTo(FlowHashGenerator.GenerateNwObjectHash(kSourceIp, kSourceIp)));
            });
        }

        [Test]
        public async Task RecalculateFlowHashesAsync_SkipsTemporaryHashesWhenNoHashIsHandedOver()
        {
            FlowNwObject staleObject = CreateFlowNwObject(1, kSourceIp, kStaleHashOne);
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [staleObject]);
            FlowHashRecalculatorTestApiConn apiConn = new();
            FlowHashRecalculator recalculator = new(apiConn);

            await recalculator.RecalculateFlowHashesAsync(flowData);

            object? variables = apiConn.HashUpdateVariables.Single();
            Assert.Multiple(() =>
            {
                Assert.That(ReadUpdates(variables, "nwObjectTempHashes", "nwobj_id", "nwobj_hash"), Is.Empty);
                Assert.That(ReadUpdates(variables, "nwObjectHashes", "nwobj_id", "nwobj_hash"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task RecalculateFlowHashesAsync_MovesEntryHandingOverItsHashThroughATemporaryHash()
        {
            string sourceHash = FlowHashGenerator.GenerateNwObjectHash(kSourceIp, kSourceIp);
            FlowNwObject claimingObject = CreateFlowNwObject(1, kSourceIp, kStaleHashOne);
            FlowNwObject handingOverObject = CreateFlowNwObject(2, kDestinationIp, sourceHash);
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [claimingObject, handingOverObject]);
            FlowHashRecalculatorTestApiConn apiConn = new();
            FlowHashRecalculator recalculator = new(apiConn);

            await recalculator.RecalculateFlowHashesAsync(flowData);

            object? variables = apiConn.HashUpdateVariables.Single();
            List<(long Id, string Hash)> temporaryUpdates = ReadUpdates(variables, "nwObjectTempHashes", "nwobj_id", "nwobj_hash");
            List<(long Id, string Hash)> updates = ReadUpdates(variables, "nwObjectHashes", "nwobj_id", "nwobj_hash");
            Assert.Multiple(() =>
            {
                Assert.That(temporaryUpdates, Has.Count.EqualTo(1));
                Assert.That(temporaryUpdates[0].Id, Is.EqualTo(2));
                Assert.That(temporaryUpdates[0].Hash, Is.Not.EqualTo(sourceHash));
                Assert.That(updates.Select(update => update.Id), Is.EquivalentTo(kBothNwObjectIds));
                Assert.That(updates.Single(update => update.Id == 1).Hash, Is.EqualTo(sourceHash));
                Assert.That(updates.Single(update => update.Id == 2).Hash, Is.EqualTo(FlowHashGenerator.GenerateNwObjectHash(kDestinationIp, kDestinationIp)));
            });
        }

        [Test]
        public async Task RecalculateFlowHashesAsync_MovesBothEntriesOfAnExchangedHashPairThroughTemporaryHashes()
        {
            string sourceHash = FlowHashGenerator.GenerateNwObjectHash(kSourceIp, kSourceIp);
            string destinationHash = FlowHashGenerator.GenerateNwObjectHash(kDestinationIp, kDestinationIp);
            FlowNwObject sourceObject = CreateFlowNwObject(1, kSourceIp, destinationHash);
            FlowNwObject destinationObject = CreateFlowNwObject(2, kDestinationIp, sourceHash);
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [sourceObject, destinationObject]);
            FlowHashRecalculatorTestApiConn apiConn = new();
            FlowHashRecalculator recalculator = new(apiConn);

            await recalculator.RecalculateFlowHashesAsync(flowData);

            object? variables = apiConn.HashUpdateVariables.Single();
            List<(long Id, string Hash)> temporaryUpdates = ReadUpdates(variables, "nwObjectTempHashes", "nwobj_id", "nwobj_hash");
            Assert.Multiple(() =>
            {
                Assert.That(temporaryUpdates.Select(update => update.Id), Is.EquivalentTo(kBothNwObjectIds));
                Assert.That(temporaryUpdates.Select(update => update.Hash), Has.None.EqualTo(sourceHash));
                Assert.That(temporaryUpdates.Select(update => update.Hash), Has.None.EqualTo(destinationHash));
                Assert.That(ReadUpdates(variables, "nwObjectHashes", "nwobj_id", "nwobj_hash"), Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task RecalculateFlowHashesAsync_RecalculatesServiceObjectsAndKeepsProtocolOnlyServices()
        {
            FlowSvcObject staleService = new() { Id = 1, ProtoId = kTcpProtoId, PortStart = kHttpPort, PortEnd = kHttpPort, Hash = kStaleHashOne };
            FlowSvcObject protocolOnlyService = new() { Id = 2, ProtoId = kIcmpProtoId, Hash = kRandomHash };
            FlowSyncFlowData flowData = CreateFlowData(svcObjects: [staleService, protocolOnlyService]);
            FlowHashRecalculatorTestApiConn apiConn = new();
            FlowHashRecalculator recalculator = new(apiConn);

            FlowHashRecalculationOutcome outcome = await recalculator.RecalculateFlowHashesAsync(flowData);

            List<(long Id, string Hash)> updates = ReadUpdates(apiConn.HashUpdateVariables.Single(), "svcObjectHashes", "svcobj_id", "svcobj_hash");
            Assert.Multiple(() =>
            {
                Assert.That(outcome, Is.EqualTo(FlowHashRecalculationOutcome.Updated));
                Assert.That(updates, Has.Count.EqualTo(1));
                Assert.That(updates[0].Id, Is.EqualTo(1));
                Assert.That(updates[0].Hash, Is.EqualTo(FlowHashGenerator.GenerateSvcObjectHash(kTcpProtoId, kHttpPort, kHttpPort)));
            });
        }

        [Test]
        public async Task RecalculateFlowHashesAsync_RecalculatesTimeObjectsAndKeepsCustomTimeObjects()
        {
            DateTime startTime = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.FromHours(kCentralEuropeanOffsetHours)).DateTime;
            DateTime endTime = new DateTimeOffset(2026, 5, 1, 18, 0, 0, TimeSpan.FromHours(kCentralEuropeanOffsetHours)).DateTime;
            FlowTimeObject staleTimeObject = new() { Id = 1, StartTime = startTime, EndTime = endTime, Hash = kStaleHashOne };
            FlowTimeObject customTimeObject = new() { Id = 2, Hash = kRandomHash };
            FlowSyncFlowData flowData = CreateFlowData(timeObjects: [staleTimeObject, customTimeObject]);
            FlowHashRecalculatorTestApiConn apiConn = new();
            FlowHashRecalculator recalculator = new(apiConn);

            FlowHashRecalculationOutcome outcome = await recalculator.RecalculateFlowHashesAsync(flowData);

            List<(long Id, string Hash)> updates = ReadUpdates(apiConn.HashUpdateVariables.Single(), "timeObjectHashes", "timeobj_id", "timeobj_hash");
            Assert.Multiple(() =>
            {
                Assert.That(outcome, Is.EqualTo(FlowHashRecalculationOutcome.Updated));
                Assert.That(updates, Has.Count.EqualTo(1));
                Assert.That(updates[0].Id, Is.EqualTo(1));
                Assert.That(updates[0].Hash, Is.EqualTo(FlowHashGenerator.GenerateTimeObjectHash(startTime, endTime)));
            });
        }

        [Test]
        public async Task RecalculateFlowHashesAsync_RecalculatesGroupsFromRecalculatedMemberHashes()
        {
            FlowNwObject staleObject = CreateFlowNwObject(1, kSourceIp, kStaleHashOne);
            FlowNwGroup nwGroup = new()
            {
                Id = 10,
                Hash = FlowHashGenerator.GenerateGroupHash([kStaleHashOne]),
                NwGroupMembers = [new FlowNwGroupMember { NwGroupId = 10, NwObjectId = 1 }]
            };
            FlowSvcObject staleService = new() { Id = 2, ProtoId = kTcpProtoId, PortStart = kHttpPort, PortEnd = kHttpPort, Hash = kStaleHashTwo };
            FlowSvcGroup svcGroup = new()
            {
                Id = 20,
                Hash = FlowHashGenerator.GenerateGroupHash([kStaleHashTwo]),
                SvcGroupMembers = [new FlowSvcGroupMember { SvcGroupId = 20, SvcObjectId = 2 }]
            };
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [staleObject], nwGroups: [nwGroup], svcObjects: [staleService], svcGroups: [svcGroup]);
            FlowHashRecalculatorTestApiConn apiConn = new();
            FlowHashRecalculator recalculator = new(apiConn);

            FlowHashRecalculationOutcome outcome = await recalculator.RecalculateFlowHashesAsync(flowData);

            object? variables = apiConn.HashUpdateVariables.Single();
            List<(long Id, string Hash)> nwGroupUpdates = ReadUpdates(variables, "nwGroupHashes", "nwgrp_id", "nwgrp_hash");
            List<(long Id, string Hash)> svcGroupUpdates = ReadUpdates(variables, "svcGroupHashes", "svcgrp_id", "svcgrp_hash");
            Assert.Multiple(() =>
            {
                Assert.That(outcome, Is.EqualTo(FlowHashRecalculationOutcome.Updated));
                Assert.That(nwGroupUpdates, Has.Count.EqualTo(1));
                Assert.That(nwGroupUpdates[0].Hash, Is.EqualTo(FlowHashGenerator.GenerateGroupHash([FlowHashGenerator.GenerateNwObjectHash(kSourceIp, kSourceIp)])));
                Assert.That(svcGroupUpdates, Has.Count.EqualTo(1));
                Assert.That(svcGroupUpdates[0].Hash, Is.EqualTo(FlowHashGenerator.GenerateGroupHash([FlowHashGenerator.GenerateSvcObjectHash(kTcpProtoId, kHttpPort, kHttpPort)])));
            });
        }

        [Test]
        public async Task RecalculateFlowHashesAsync_RecalculatesAccessesFromRecalculatedMemberHashes()
        {
            FlowNwObject staleSource = CreateFlowNwObject(1, kSourceIp, kStaleHashOne);
            FlowNwObject staleDestination = CreateFlowNwObject(2, kDestinationIp, kStaleHashTwo);
            FlowSvcObject staleService = new() { Id = 3, ProtoId = kTcpProtoId, PortStart = kHttpsPort, PortEnd = kHttpsPort, Hash = kStaleHashThree };
            FlowAccess access = new()
            {
                Id = 30,
                Hash = FlowHashGenerator.GenerateAccessHash([kStaleHashOne], [kStaleHashTwo], [kStaleHashThree], [], true),
                AllowsTraffic = true,
                Sources = [new FlowAccessSource { AccessId = 30, NwObjectId = 1 }],
                Destinations = [new FlowAccessDestination { AccessId = 30, NwObjectId = 2 }],
                Services = [new FlowAccessService { AccessId = 30, SvcObjectId = 3 }]
            };
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [staleSource, staleDestination], svcObjects: [staleService], accesses: [access]);
            FlowHashRecalculatorTestApiConn apiConn = new();
            FlowHashRecalculator recalculator = new(apiConn);

            FlowHashRecalculationOutcome outcome = await recalculator.RecalculateFlowHashesAsync(flowData);

            List<(long Id, string Hash)> updates = ReadUpdates(apiConn.HashUpdateVariables.Single(), "accessHashes", "access_id", "access_hash");
            string expectedHash = FlowHashGenerator.GenerateAccessHash(
                [FlowHashGenerator.GenerateNwObjectHash(kSourceIp, kSourceIp)],
                [FlowHashGenerator.GenerateNwObjectHash(kDestinationIp, kDestinationIp)],
                [FlowHashGenerator.GenerateSvcObjectHash(kTcpProtoId, kHttpsPort, kHttpsPort)],
                [],
                true);
            Assert.Multiple(() =>
            {
                Assert.That(outcome, Is.EqualTo(FlowHashRecalculationOutcome.Updated));
                Assert.That(updates, Has.Count.EqualTo(1));
                Assert.That(updates[0].Id, Is.EqualTo(30));
                Assert.That(updates[0].Hash, Is.EqualTo(expectedHash));
            });
        }

        [Test]
        public async Task RecalculateFlowHashesAsync_DoesNotUpdateAnythingWhenAllHashesAreUpToDate()
        {
            FlowNwObject nwObject = CreateFlowNwObject(1, kSourceIp, FlowHashGenerator.GenerateNwObjectHash(kSourceIp, kSourceIp));
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [nwObject]);
            FlowHashRecalculatorTestApiConn apiConn = new();
            FlowHashRecalculator recalculator = new(apiConn);

            FlowHashRecalculationOutcome outcome = await recalculator.RecalculateFlowHashesAsync(flowData);

            Assert.Multiple(() =>
            {
                Assert.That(outcome, Is.EqualTo(FlowHashRecalculationOutcome.NoChanges));
                Assert.That(apiConn.HashUpdateVariables, Is.Empty);
            });
        }

        [Test]
        public async Task RecalculateFlowHashesAsync_AbortsWhenRecalculatedHashesWouldNoLongerBeUnique()
        {
            FlowNwObject firstDuplicate = CreateFlowNwObject(1, kSourceIp, kStaleHashOne);
            FlowNwObject secondDuplicate = CreateFlowNwObject(2, kSourceIp, kStaleHashTwo);
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [firstDuplicate, secondDuplicate]);
            FlowHashRecalculatorTestApiConn apiConn = new();
            FlowHashRecalculator recalculator = new(apiConn);

            FlowHashRecalculationOutcome outcome = await recalculator.RecalculateFlowHashesAsync(flowData);

            Assert.Multiple(() =>
            {
                Assert.That(outcome, Is.EqualTo(FlowHashRecalculationOutcome.Conflict));
                Assert.That(apiConn.HashUpdateVariables, Is.Empty);
            });
        }

        [Test]
        public void Calculate_KeepsHashesThatCannotBeRecalculatedAndReportsConflicts()
        {
            FlowNwObject firstDuplicate = CreateFlowNwObject(1, kSourceIp, kStaleHashOne);
            FlowNwObject secondDuplicate = CreateFlowNwObject(2, kSourceIp, kStaleHashTwo);
            FlowNwGroup emptyGroup = new() { Id = 10, Hash = kStaleHashThree };
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [firstDuplicate, secondDuplicate], nwGroups: [emptyGroup]);

            FlowHashRecalculationResult result = FlowHashRecalculation.Calculate(flowData);

            Assert.Multiple(() =>
            {
                Assert.That(result.HasConflicts, Is.True);
                Assert.That(result.Conflicts, Has.Count.EqualTo(1));
                Assert.That(result.Conflicts[0], Does.Contain("network object ids 1, 2"));
                Assert.That(result.NwObjects, Has.Count.EqualTo(2));
                Assert.That(result.NwGroups, Is.Empty);
                Assert.That(result.ChangeCount, Is.EqualTo(2));
            });
        }

        private static FlowNwObject CreateFlowNwObject(long id, string ip, string hash)
        {
            return new() { Id = id, IpStart = ip, IpEnd = ip, Hash = hash };
        }

        private static FlowSyncFlowData CreateFlowData(List<FlowNwObject>? nwObjects = null, List<FlowNwGroup>? nwGroups = null,
            List<FlowSvcObject>? svcObjects = null, List<FlowSvcGroup>? svcGroups = null, List<FlowTimeObject>? timeObjects = null,
            List<FlowAccess>? accesses = null)
        {
            return new(new FlowSyncFlowDataInput
            {
                NwObjects = nwObjects ?? [],
                NwGroups = nwGroups ?? [],
                SvcObjects = svcObjects ?? [],
                SvcGroups = svcGroups ?? [],
                TimeObjects = timeObjects ?? [],
                Accesses = accesses ?? []
            });
        }

        /// <summary>
        /// Reads the id and hash of every update entry of one flow entry type from the mutation variables.
        /// </summary>
        private static List<(long Id, string Hash)> ReadUpdates(object? variables, string propertyName, string idField, string hashField)
        {
            List<object> updates = GetProperty<List<object>>(variables, propertyName) ?? [];
            List<(long Id, string Hash)> readUpdates = [];

            foreach (object update in updates)
            {
                object idFilter = GetProperty<object>(GetProperty<object>(update, "where"), idField);
                readUpdates.Add((GetProperty<long>(idFilter, "_eq"), GetProperty<string>(GetProperty<object>(update, "_set"), hashField)));
            }

            return readUpdates;
        }

        private static TValue GetProperty<TValue>(object? source, string propertyName)
        {
            PropertyInfo? property = source?.GetType().GetProperty(propertyName);
            return property != null ? (TValue)property.GetValue(source)! : default!;
        }
    }
}
