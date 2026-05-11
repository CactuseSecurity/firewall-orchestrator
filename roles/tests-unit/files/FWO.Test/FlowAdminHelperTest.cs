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
    }
}
