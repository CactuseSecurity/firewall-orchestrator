using FWO.Data.Flow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowAccessCatalogHelperTest
    {
        [Test]
        public void BuildNwObjectCatalog_ReturnsUniqueSortedObjects()
        {
            FlowNwObject alpha = new() { Id = 2, Name = "alpha" };
            FlowNwObject beta = new() { Id = 1, Name = "beta" };

            List<FlowAccess> accesses =
            [
                new()
                {
                    Sources = [new FlowAccessSource { NwObject = beta }],
                    SourceGroups =
                    [
                        new FlowAccessSourceGroup
                        {
                            NwGroup = new FlowNwGroup
                            {
                                NwGroupMembers =
                                [
                                    new FlowNwGroupMember { NwObject = alpha }
                                ]
                            }
                        }
                    ]
                },
                new()
                {
                    Destinations = [new FlowAccessDestination { NwObject = beta }]
                }
            ];

            List<FlowNwObject> catalog = FlowAccessCatalogHelper.BuildNwObjectCatalog(accesses);

            Assert.That(catalog, Has.Count.EqualTo(2));
            Assert.That(catalog[0].Name, Is.EqualTo("alpha"));
            Assert.That(catalog[1].Name, Is.EqualTo("beta"));
        }

        [Test]
        public void ApplyNwObjectUpdate_UpdatesCatalogAndAccessReferences()
        {
            FlowNwObject accessCopy = new() { Id = 7, Name = "old", ShowInRequestModule = false };
            FlowNwObject catalogCopy = new() { Id = 7, Name = "old", ShowInRequestModule = false };
            FlowNwObject groupCopy = new() { Id = 7, Name = "old", ShowInRequestModule = false };

            FlowAccess access = new()
            {
                Sources = [new FlowAccessSource { NwObject = accessCopy }],
                SourceGroups =
                [
                    new FlowAccessSourceGroup
                    {
                        NwGroup = new FlowNwGroup
                        {
                            NwGroupMembers =
                            [
                                new FlowNwGroupMember { NwObject = groupCopy }
                            ]
                        }
                    }
                ]
            };

            List<FlowAccess> accesses = [access];
            List<FlowNwObject> catalog = [catalogCopy];
            FlowNwObject updated = new() { Id = 7, Name = "new", ShowInRequestModule = true };

            FlowAccessCatalogHelper.ApplyNwObjectUpdate(accesses, catalog, updated);

            Assert.That(accessCopy.Name, Is.EqualTo("new"));
            Assert.That(groupCopy.ShowInRequestModule, Is.True);
            Assert.That(catalog[0].Name, Is.EqualTo("new"));
            Assert.That(catalog[0].ShowInRequestModule, Is.True);
        }
    }
}
