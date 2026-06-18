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
        public void FlowQueries_LoadCustomObjectCandidatesQuery()
        {
            Assert.That(FlowQueries.getFlowCustomObjectCandidates, Does.Contain("query getFlowCustomObjectCandidates"));
            Assert.That(FlowQueries.getFlowCustomObjectCandidates, Does.Contain("networkObjects: objects"));
        }

        [Test]
        public void FlowQueries_LoadFlowCatalogQueries()
        {
            Assert.That(FlowQueries.getFlowAddressObjects, Does.Contain("query getAddressObjects"));
            Assert.That(FlowQueries.getFlowAddressObjects, Does.Contain("fragment flowNwObjectDetails"));
            Assert.That(FlowQueries.getFlowAddressGroups, Does.Contain("query getAddressGroups"));
            Assert.That(FlowQueries.getFlowAddressGroups, Does.Contain("fragment flowNwGroupDetails"));
            Assert.That(FlowQueries.getFlowServiceObjects, Does.Contain("query getServiceObjects"));
            Assert.That(FlowQueries.getFlowServiceObjects, Does.Contain("fragment flowSvcObjectDetails"));
            Assert.That(FlowQueries.getFlowServiceGroups, Does.Contain("query getServiceGroups"));
            Assert.That(FlowQueries.getFlowServiceGroups, Does.Contain("fragment flowSvcGroupDetails"));
            Assert.That(FlowQueries.getFlowTimeObjects, Does.Contain("query getTimeObjects"));
            Assert.That(FlowQueries.getFlowTimeObjects, Does.Contain("fragment flowTimeObjectDetails"));
            Assert.That(FlowQueries.getFlowAddressObjectId, Does.Contain("query getAddressObjectId"));
            Assert.That(FlowQueries.getFlowServiceObjectId, Does.Contain("query getServiceObjectId"));
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

        [Test]
        public void FlowQueries_LoadFlowSyncQueries()
        {
            Assert.That(FlowQueries.getFlowSyncNwObjects, Does.Contain("query getFlowSyncNwObjects"));
            Assert.That(FlowQueries.getFlowSyncNwGroups, Does.Contain("query getFlowSyncNwGroups"));
            Assert.That(FlowQueries.getFlowSyncSvcObjects, Does.Contain("query getFlowSyncSvcObjects"));
            Assert.That(FlowQueries.getFlowSyncSvcGroups, Does.Contain("query getFlowSyncSvcGroups"));
            Assert.That(FlowQueries.getFlowSyncTimeObjects, Does.Contain("query getFlowSyncTimeObjects"));
            Assert.That(FlowQueries.getFlowSyncAccesses, Does.Contain("query getFlowSyncAccesses"));
            Assert.That(FlowQueries.getPendingFlowSyncImports, Does.Contain("query getPendingFlowSyncImports"));
        }

        [Test]
        public void FlowQueries_LoadFlowSyncManagementDataQuery()
        {
            Assert.That(FlowQueries.getFlowSyncManagementData, Does.Contain("query getFlowSyncManagementData"));
            Assert.That(FlowQueries.getFlowSyncManagementData, Does.Contain("fragment networkObjectFlowSyncDetails"));
            Assert.That(FlowQueries.getFlowSyncManagementData, Does.Contain("fragment networkServiceFlowSyncDetails"));
            Assert.That(FlowQueries.getFlowSyncManagementData, Does.Contain("fragment timeObjectFlowSyncDetails"));
            Assert.That(FlowQueries.getFlowSyncManagementData, Does.Contain("fragment ruleFlowSyncDetails"));
        }

        [Test]
        public void FlowQueries_LoadFlowSyncMutations()
        {
            Assert.That(FlowQueries.insertFlowNwObjects, Does.Contain("mutation insertFlowNwObjects"));
            Assert.That(FlowQueries.insertFlowSvcObjects, Does.Contain("mutation insertFlowSvcObjects"));
            Assert.That(FlowQueries.insertFlowTimeObjects, Does.Contain("mutation insertFlowTimeObjects"));
            Assert.That(FlowQueries.insertFlowNwGroups, Does.Contain("mutation insertFlowNwGroups"));
            Assert.That(FlowQueries.insertFlowNwGroupMembers, Does.Contain("mutation insertFlowNwGroupMembers"));
            Assert.That(FlowQueries.insertFlowSvcGroups, Does.Contain("mutation insertFlowSvcGroups"));
            Assert.That(FlowQueries.insertFlowSvcGroupMembers, Does.Contain("mutation insertFlowSvcGroupMembers"));
            Assert.That(FlowQueries.insertFlowAccesses, Does.Contain("mutation insertFlowAccesses"));
            Assert.That(FlowQueries.updateObjectFlowMappings, Does.Contain("mutation updateObjectFlowMappings"));
            Assert.That(FlowQueries.updateServiceFlowMappings, Does.Contain("mutation updateServiceFlowMappings"));
            Assert.That(FlowQueries.updateTimeObjectFlowMappings, Does.Contain("mutation updateTimeObjectFlowMappings"));
            Assert.That(FlowQueries.updateRuleFlowMappings, Does.Contain("mutation updateRuleFlowMappings"));
            Assert.That(FlowQueries.updateImportControlForFlowSync, Does.Contain("mutation updateImportControlForFlowSync"));
        }
    }
}
