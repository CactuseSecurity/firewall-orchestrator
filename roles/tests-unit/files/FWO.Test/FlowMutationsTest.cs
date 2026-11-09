using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class FlowMutationsTest
    {
        [Test]
        public void FlowMutations_LoadObjectMutations()
        {
            Assert.That(FlowMutations.insertFlowNwObject, Does.Contain("mutation insertFlowNwObject"));
            Assert.That(FlowMutations.upsertFlowNwObjectMapping, Does.Contain("mutation upsertFlowNwObjectMapping"));
            Assert.That(FlowMutations.upsertFlowNwObjectMapping, Does.Contain("update_firewall_nw_object_by_pk"));
            Assert.That(FlowMutations.upsertFlowNwObjectMapping, Does.Contain("flow_nwgrp_id: null"));
            Assert.That(FlowMutations.upsertFlowNwGroupMapping, Does.Contain("mutation upsertFlowNwGroupMapping"));
            Assert.That(FlowMutations.upsertFlowNwGroupMapping, Does.Contain("update_firewall_nw_object_by_pk"));
            Assert.That(FlowMutations.upsertFlowNwGroupMapping, Does.Contain("flow_nwobj_id: null"));
            Assert.That(FlowMutations.updateFlowNwGroup, Does.Contain("mutation updateFlowNwGroup"));
            Assert.That(FlowMutations.updateFlowNwObject, Does.Contain("mutation updateFlowNwObject"));
            Assert.That(FlowMutations.updateFlowNwGroups, Does.Contain("mutation updateFlowNwGroups"));
            Assert.That(FlowMutations.updateFlowNwObjects, Does.Contain("mutation updateFlowNwObjects"));
            Assert.That(FlowMutations.upsertFlowSvcObjectMapping, Does.Contain("mutation upsertFlowSvcObjectMapping"));
            Assert.That(FlowMutations.upsertFlowSvcObjectMapping, Does.Contain("update_firewall_nw_service_by_pk"));
            Assert.That(FlowMutations.upsertFlowSvcObjectMapping, Does.Contain("pk_columns: { svc_id: $svcId }"));
            Assert.That(FlowMutations.upsertFlowSvcObjectMapping, Does.Contain("flow_svcobj_id: $flowSvcobjId"));
            Assert.That(FlowMutations.upsertFlowSvcObjectMapping, Does.Contain("flow_svcgrp_id: null"));
            Assert.That(FlowMutations.upsertFlowSvcObjectMapping, Does.Contain("flow_active: $activeOnMgm"));
            Assert.That(FlowMutations.upsertFlowSvcGroupMapping, Does.Contain("mutation upsertFlowSvcGroupMapping"));
            Assert.That(FlowMutations.upsertFlowSvcGroupMapping, Does.Contain("update_firewall_nw_service_by_pk"));
            Assert.That(FlowMutations.upsertFlowSvcGroupMapping, Does.Contain("flow_svcobj_id: null"));
            Assert.That(FlowMutations.updateFlowSvcGroup, Does.Contain("mutation updateFlowSvcGroup"));
            Assert.That(FlowMutations.updateFlowSvcObject, Does.Contain("mutation updateFlowSvcObject"));
            Assert.That(FlowMutations.updateFlowSvcGroups, Does.Contain("mutation updateFlowSvcGroups"));
            Assert.That(FlowMutations.updateFlowSvcObjects, Does.Contain("mutation updateFlowSvcObjects"));
            Assert.That(FlowMutations.upsertFlowTimeObjectMapping, Does.Contain("mutation upsertFlowTimeObjectMapping"));
            Assert.That(FlowMutations.upsertFlowTimeObjectMapping, Does.Contain("update_time_object_by_pk"));
            Assert.That(FlowMutations.upsertFlowTimeObjectMapping, Does.Not.Contain("\n    active\n"));
            Assert.That(FlowMutations.updateFlowTimeObject, Does.Contain("mutation updateFlowTimeObject"));
            Assert.That(FlowMutations.updateFlowTimeObjects, Does.Contain("mutation updateFlowTimeObjects"));
        }

        [Test]
        public void ResetFlowDbMutation_LoadsDeleteAndResetStatements()
        {
            Assert.That(FlowMutations.resetFlowDB, Does.Contain("mutation resetFlowDB"));
            Assert.That(FlowMutations.resetFlowDB, Does.Contain("update_import_control"));
            Assert.That(FlowMutations.resetFlowDB, Does.Contain("delete_flow_nwobject"));
        }
    }
}
