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
                    NwObjectMappings =
                    [
                        new FlowNwObjectMapping
                        {
                            FlowNwObjectId = 1,
                            MgmId = 10,
                            ObjId = 100,
                            ActiveOnMgm = false,
                            Management = new Management { Id = 10, Name = "Mgmt A" },
                            Object = new NetworkObject { Id = 100, Name = "obj-a" }
                        },
                        new FlowNwObjectMapping
                        {
                            FlowNwObjectId = 1,
                            MgmId = 10,
                            ObjId = 200,
                            ActiveOnMgm = false,
                            Management = new Management { Id = 10, Name = "Mgmt A" },
                            Object = new NetworkObject { Id = 200, Name = "obj-b" }
                        }
                    ]
                }
            ];

            List<FlowNwObjectDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowObjects);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].FlowNwObjectId, Is.EqualTo(1));
            Assert.That(groups[0].ManagementId, Is.EqualTo(10));
            Assert.That(groups[0].Mappings, Has.Count.EqualTo(2));
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
                    NwObjectMappings =
                    [
                        new FlowNwObjectMapping
                        {
                            FlowNwObjectId = 1,
                            MgmId = 10,
                            ObjId = 100,
                            ActiveOnMgm = true,
                            Management = new Management { Id = 10, Name = "Mgmt A" },
                            Object = new NetworkObject { Id = 100, Name = "obj-a" }
                        },
                        new FlowNwObjectMapping
                        {
                            FlowNwObjectId = 1,
                            MgmId = 10,
                            ObjId = 200,
                            ActiveOnMgm = false,
                            Management = new Management { Id = 10, Name = "Mgmt A" },
                            Object = new NetworkObject { Id = 200, Name = "obj-b" }
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
    }
}
