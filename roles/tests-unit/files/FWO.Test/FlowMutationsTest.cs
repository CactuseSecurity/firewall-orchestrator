using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowMutationsTest
    {
        [Test]
        public void FlowMutations_LoadObjectMutations()
        {
            Assert.That(FlowMutations.insertFlowNwObject, Does.Contain("mutation insertFlowNwObject"));
            Assert.That(FlowMutations.upsertFlowNwObjectMapping, Does.Contain("mutation upsertFlowNwObjectMapping"));
            Assert.That(FlowMutations.updateFlowNwGroup, Does.Contain("mutation updateFlowNwGroup"));
            Assert.That(FlowMutations.updateFlowNwGroup, Does.Contain("nwgroup_members(order_by: { nwobj_id: asc })"));
            Assert.That(FlowMutations.updateFlowNwGroup, Does.Contain("objects(order_by: { obj_name: asc })"));
            Assert.That(FlowMutations.updateFlowNwObject, Does.Contain("mutation updateFlowNwObject"));
            Assert.That(FlowMutations.updateFlowNwObject, Does.Contain("objects(order_by: { obj_name: asc })"));
            Assert.That(FlowMutations.updateFlowSvcGroup, Does.Contain("mutation updateFlowSvcGroup"));
            Assert.That(FlowMutations.updateFlowSvcGroup, Does.Contain("svcgroup_members(order_by: { svcobj_id: asc })"));
            Assert.That(FlowMutations.updateFlowSvcGroup, Does.Contain("services(order_by: { svc_name: asc })"));
            Assert.That(FlowMutations.updateFlowSvcObject, Does.Contain("mutation updateFlowSvcObject"));
            Assert.That(FlowMutations.updateFlowSvcObject, Does.Contain("services(order_by: { svc_name: asc })"));
            Assert.That(FlowMutations.updateFlowTimeObject, Does.Contain("mutation updateFlowTimeObject"));
            Assert.That(FlowMutations.updateFlowTimeObject, Does.Contain("time_objects(order_by: { time_obj_name: asc })"));
        }
    }
}
