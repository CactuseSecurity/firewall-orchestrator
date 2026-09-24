using FWO.Data.Flow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowHashInconsistencyTest
    {
        private const string kSourceIp = "10.0.0.1";
        private const string kStaleHash = "1111111111111111111111111111111111111111111111111111111111111111";
        private const string kRandomHash = "8bf9f27b21be47a2b1f6a6cf9e3ac0f1";
        private const int kDescribedInconsistencies = 20;

        [Test]
        public void GetHashInconsistencies_ReportsStoredAndRecalculatedHashOfEveryStaleEntry()
        {
            FlowNwObject staleObject = new() { Id = 1, IpStart = kSourceIp, IpEnd = kSourceIp, Hash = kStaleHash };
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [staleObject]);

            List<FlowHashInconsistency> inconsistencies = flowData.GetHashInconsistencies();

            Assert.That(inconsistencies, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(inconsistencies[0].EntryType, Is.EqualTo(FlowEntryType.kNwObject));
                Assert.That(inconsistencies[0].Id, Is.EqualTo(1));
                Assert.That(inconsistencies[0].StoredHash, Is.EqualTo(kStaleHash));
                Assert.That(inconsistencies[0].RecalculatedHash, Is.EqualTo(FlowHashGenerator.GenerateNwObjectHash(kSourceIp, kSourceIp)));
            });
        }

        [Test]
        public void GetHashInconsistencies_IgnoresEntriesWithARandomlyGeneratedHash()
        {
            FlowNwObject dynamicObject = new() { Id = 1, Hash = kRandomHash };
            FlowSvcObject protocolOnlyService = new() { Id = 2, ProtoId = 1, Hash = kRandomHash };
            FlowTimeObject customTimeObject = new() { Id = 3, Hash = kRandomHash };
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [dynamicObject], svcObjects: [protocolOnlyService], timeObjects: [customTimeObject]);

            Assert.That(flowData.GetHashInconsistencies(), Is.Empty);
        }

        [Test]
        public void GetHashInconsistencies_ReportsGroupWhoseHashCanNoLongerBeCalculated()
        {
            FlowNwGroup memberlessGroup = new() { Id = 10, Hash = kStaleHash };
            FlowSyncFlowData flowData = CreateFlowData(nwGroups: [memberlessGroup]);

            List<FlowHashInconsistency> inconsistencies = flowData.GetHashInconsistencies();

            Assert.That(inconsistencies, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(inconsistencies[0].EntryType, Is.EqualTo(FlowEntryType.kNwGroup));
                Assert.That(inconsistencies[0].Id, Is.EqualTo(10));
                Assert.That(inconsistencies[0].RecalculatedHash, Is.Null);
                // this entry is inconsistent although the recalculation cannot produce a change for it
                Assert.That(FlowHashRecalculation.Calculate(flowData).HasChanges, Is.False);
            });
        }

        [Test]
        public void GetHashInconsistencies_IsEmptyWhenEveryStoredHashMatches()
        {
            FlowNwObject nwObject = new() { Id = 1, IpStart = kSourceIp, IpEnd = kSourceIp, Hash = FlowHashGenerator.GenerateNwObjectHash(kSourceIp, kSourceIp) };
            FlowSyncFlowData flowData = CreateFlowData(nwObjects: [nwObject]);

            Assert.That(flowData.GetHashInconsistencies(), Is.Empty);
        }

        [Test]
        public void Describe_NamesEntryTypeIdAndBothHashes()
        {
            FlowHashInconsistency inconsistency = new()
            {
                EntryType = FlowEntryType.kAccess,
                Id = 30,
                StoredHash = kStaleHash,
                RecalculatedHash = null
            };

            string description = FlowHashInconsistency.Describe([inconsistency]);

            Assert.Multiple(() =>
            {
                Assert.That(description, Does.Contain(FlowEntryType.kAccess));
                Assert.That(description, Does.Contain("id 30"));
                Assert.That(description, Does.Contain(kStaleHash));
                Assert.That(description, Does.Contain("recalculated none"));
            });
        }

        [Test]
        public void Describe_CapsTheNumberOfNamedEntries()
        {
            List<FlowHashInconsistency> inconsistencies = [.. Enumerable.Range(1, kDescribedInconsistencies + 5)
                .Select(id => new FlowHashInconsistency { EntryType = FlowEntryType.kNwObject, Id = id, StoredHash = kStaleHash })];

            string description = FlowHashInconsistency.Describe(inconsistencies);

            Assert.Multiple(() =>
            {
                Assert.That(description, Does.Contain($"id {kDescribedInconsistencies}"));
                Assert.That(description, Does.Not.Contain($"id {kDescribedInconsistencies + 1} "));
                Assert.That(description, Does.Contain("and 5 more"));
            });
        }

        private static FlowSyncFlowData CreateFlowData(List<FlowNwObject>? nwObjects = null, List<FlowNwGroup>? nwGroups = null,
            List<FlowSvcObject>? svcObjects = null, List<FlowTimeObject>? timeObjects = null)
        {
            return new(new FlowSyncFlowDataInput
            {
                NwObjects = nwObjects ?? [],
                NwGroups = nwGroups ?? [],
                SvcObjects = svcObjects ?? [],
                TimeObjects = timeObjects ?? []
            });
        }
    }
}
