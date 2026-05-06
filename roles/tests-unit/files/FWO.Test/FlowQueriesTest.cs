using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowQueriesTest
    {
        [Test]
        public void FlowQueries_LoadObjectCatalogQuery()
        {
            Assert.That(FlowQueries.getFlowObjectCatalog, Does.Contain("query getFlowObjectCatalog"));
            Assert.That(FlowQueries.getFlowObjectCatalog, Does.Contain("fragment flowNwObjectDetails"));
        }

        [Test]
        public void FlowQueries_LoadAccessCatalogQuery()
        {
            Assert.That(FlowQueries.getFlowAccessCatalog, Does.Contain("query getFlowAccessCatalog"));
            Assert.That(FlowQueries.getFlowAccessCatalog, Does.Contain("fragment flowAccessDetails"));
        }

        [Test]
        public void FlowQueries_LoadNwObjectCatalogQuery()
        {
            Assert.That(FlowQueries.getFlowNwObjectCatalog, Does.Contain("query getFlowNwObjectCatalog"));
            Assert.That(FlowQueries.getFlowNwObjectCatalog, Does.Contain("fragment flowNwObjectDetails"));
        }

        [Test]
        public void FlowQueries_LoadSelectableManagementsQuery()
        {
            Assert.That(FlowQueries.getFlowSelectableManagements, Does.Contain("query getFlowSelectableManagements"));
            Assert.That(FlowQueries.getFlowSelectableManagements, Does.Contain("management(order_by: { mgm_name: asc })"));
        }
    }
}
