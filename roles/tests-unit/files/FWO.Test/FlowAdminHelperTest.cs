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
            List<NetworkObject> byType = FlowAdminHelper.FilterCustomObjectCandidates(candidates, "host");

            Assert.That(byName, Has.Count.EqualTo(1));
            Assert.That(byName[0].Id, Is.EqualTo(10));
            Assert.That(byIp, Has.Count.EqualTo(1));
            Assert.That(byIp[0].Id, Is.EqualTo(10));
            Assert.That(byType, Has.Count.EqualTo(1));
            Assert.That(byType[0].Id, Is.EqualTo(10));
        }

        [Test]
        public void FilterCustomServiceCandidates_FindsMatchesAcrossRelevantFields()
        {
            List<NetworkService> candidates =
            [
                new NetworkService
                {
                    Id = 10,
                    Name = "svc-alpha",
                    Uid = "uid-10",
                    DestinationPort = 443,
                    DestinationPortEnd = 443,
                    SourcePort = 0,
                    SourcePortEnd = 0,
                    Code = "alpha",
                    Type = new NetworkServiceType { Name = "service" },
                    Protocol = new NetworkProtocol { Id = 6, Name = "tcp" },
                    Active = true,
                    FlowActive = false
                },
                new NetworkService
                {
                    Id = 20,
                    Name = "svc-beta",
                    Uid = "uid-20",
                    DestinationPort = 53,
                    DestinationPortEnd = 53,
                    SourcePort = 0,
                    SourcePortEnd = 0,
                    Code = "beta",
                    Type = new NetworkServiceType { Name = "service" },
                    Protocol = new NetworkProtocol { Id = 17, Name = "udp" },
                    Active = false,
                    FlowActive = true
                }
            ];

            List<NetworkService> byName = FlowAdminHelper.FilterCustomServiceCandidates(candidates, "alpha");
            List<NetworkService> byPort = FlowAdminHelper.FilterCustomServiceCandidates(candidates, "443");
            List<NetworkService> byProto = FlowAdminHelper.FilterCustomServiceCandidates(candidates, "udp");

            Assert.That(byName, Has.Count.EqualTo(1));
            Assert.That(byName[0].Id, Is.EqualTo(10));
            Assert.That(byPort, Has.Count.EqualTo(1));
            Assert.That(byPort[0].Id, Is.EqualTo(10));
            Assert.That(byProto, Has.Count.EqualTo(1));
            Assert.That(byProto[0].Id, Is.EqualTo(20));
        }
    }
}
