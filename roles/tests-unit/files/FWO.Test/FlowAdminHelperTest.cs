using FWO.Data.Flow;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowAdminHelperTest
    {
        [Test]
        public void BuildDuplicateGroups_FindsInactiveConflicts()
        {
            List<FlowNwObject> flowObjects =
            [
                new FlowNwObject
                {
                    Id = 1,
                    Name = "duplicate-flow",
                    Objects =
                    [
                        new NetworkObject
                        {
                            Id = 100,
                            Name = "obj-a",
                            FlowNetworkObjectId = 1,
                            FlowActive = false
                        },
                        new NetworkObject
                        {
                            Id = 200,
                            Name = "obj-b",
                            FlowNetworkObjectId = 1,
                            FlowActive = false
                        }
                    ]
                }
            ];

            List<FlowNwObjectDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowObjects);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].FlowNwObjectId, Is.EqualTo(1));
            Assert.That(groups[0].Objects, Has.Count.EqualTo(2));
        }

        [Test]
        public void BuildDuplicateGroups_SkipsActiveMappings()
        {
            List<FlowNwObject> flowObjects =
            [
                new FlowNwObject
                {
                    Id = 1,
                    Name = "non-duplicate-flow",
                    Objects =
                    [
                        new NetworkObject
                        {
                            Id = 100,
                            Name = "obj-a",
                            FlowNetworkObjectId = 1,
                            FlowActive = true
                        },
                        new NetworkObject
                        {
                            Id = 200,
                            Name = "obj-b",
                            FlowNetworkObjectId = 1,
                            FlowActive = false
                        }
                    ]
                }
            ];

            List<FlowNwObjectDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowObjects);

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public void FilterCustomObjectCandidates_FindsMatchesAcrossRelevantFields()
        {
            List<NetworkObject> candidates =
            [
                new NetworkObject
                {
                    Id = 10,
                    Name = "thisisatestobject",
                    IP = "10.0.0.1",
                    IpEnd = "",
                    Uid = "uid-10",
                    Active = false,
                    Type = new NetworkObjectType { Id = 1, Name = "host" }
                },
                new NetworkObject
                {
                    Id = 20,
                    Name = "another-object",
                    IP = "192.0.2.1",
                    IpEnd = "",
                    Uid = "uid-20",
                    Active = true,
                    Type = new NetworkObjectType { Id = 2, Name = "network" }
                }
            ];

            List<NetworkObject> byName = FlowAdminHelper.FilterCustomObjectCandidates(candidates, "test");
            List<NetworkObject> byIp = FlowAdminHelper.FilterCustomObjectCandidates(candidates, "10.0.0.1");
            List<NetworkObject> byUid = FlowAdminHelper.FilterCustomObjectCandidates(candidates, "uid-10");
            List<NetworkObject> byType = FlowAdminHelper.FilterCustomObjectCandidates(candidates, "host");

            Assert.That(byName, Has.Count.EqualTo(1));
            Assert.That(byName[0].Id, Is.EqualTo(10));
            Assert.That(byIp, Has.Count.EqualTo(1));
            Assert.That(byIp[0].Id, Is.EqualTo(10));
            Assert.That(byUid, Has.Count.EqualTo(1));
            Assert.That(byUid[0].Id, Is.EqualTo(10));
            Assert.That(byType, Has.Count.EqualTo(1));
            Assert.That(byType[0].Id, Is.EqualTo(10));
        }

        [Test]
        public void HasNoTechnicalAddress_OnlyReturnsTrueForObjectsWithoutIpData()
        {
            NetworkObject customObject = new()
            {
                Id = 100,
                IP = "",
                IpEnd = "",
                Uid = "fw-uid"
            };
            NetworkObject technicalObject = new()
            {
                Id = 200,
                IP = "192.0.2.10",
                IpEnd = "",
                Uid = "fw-uid-2"
            };

            Assert.That(FlowAdminHelper.HasNoTechnicalAddress(customObject), Is.True);
            Assert.That(FlowAdminHelper.HasNoTechnicalAddress(technicalObject), Is.False);
        }

        [Test]
        public void FormatNetworkObjectTechnicalDetails_IncludesTheTechnicalIdentifier()
        {
            NetworkObject candidate = new()
            {
                Id = 42,
                Name = "candidate",
                IP = "",
                IpEnd = "",
                Uid = "uid-42"
            };

            string details = FlowAdminHelper.FormatNetworkObjectTechnicalDetails(candidate);

            Assert.That(details, Does.Contain("candidate"));
            Assert.That(details, Does.Contain("uid-42"));
        }

        [Test]
        public void FormatNetworkObjectTechnicalDetails_UsesTechnicalIdentifierForNoIpObjects()
        {
            NetworkObject candidate = new()
            {
                Id = 42,
                Name = "",
                IP = "",
                IpEnd = "",
                Uid = "uid-42"
            };

            string details = FlowAdminHelper.FormatNetworkObjectTechnicalDetails(candidate);

            Assert.That(details, Is.EqualTo("uid-42"));
        }

        [Test]
        public void FormatFlowNwObjectTechnicalDetails_UsesIpRangeWhenAvailable()
        {
            FlowNwObject candidate = new()
            {
                Id = 42,
                Name = "flow-candidate",
                IpStart = "192.0.2.10",
                IpEnd = "",
                Hash = "hash-42"
            };

            string details = FlowAdminHelper.FormatFlowNwObjectTechnicalDetails(candidate);

            Assert.That(details, Is.EqualTo("192.0.2.10"));
        }

        [Test]
        public void FormatFlowNwObjectTechnicalDetails_ReturnsEmptyForObjectsWithoutIpData()
        {
            FlowNwObject candidate = new()
            {
                Id = 42,
                Name = "flow-candidate",
                IpStart = "",
                IpEnd = "",
                Hash = "hash-42"
            };

            string details = FlowAdminHelper.FormatFlowNwObjectTechnicalDetails(candidate);

            Assert.That(details, Is.EqualTo(""));
        }

        [Test]
        public void FormatDuplicateObjectSummary_TruncatesLongLists()
        {
            List<NetworkObject> candidates =
            [
                new NetworkObject { Id = 1, Name = "one", IP = "", IpEnd = "", Uid = "uid-1" },
                new NetworkObject { Id = 2, Name = "two", IP = "", IpEnd = "", Uid = "uid-2" },
                new NetworkObject { Id = 3, Name = "three", IP = "", IpEnd = "", Uid = "uid-3" }
            ];

            string summary = FlowAdminHelper.FormatDuplicateObjectSummary(candidates, 2);

            Assert.That(summary, Is.EqualTo("one [uid-1], two [uid-2], ... and 1 more"));
        }

        [Test]
        public void FormatDuplicateObjectSummary_ReturnsPlaceholderForEmptyLists()
        {
            string summary = FlowAdminHelper.FormatDuplicateObjectSummary([], 2);

            Assert.That(summary, Is.EqualTo("-"));
        }
    }
}
