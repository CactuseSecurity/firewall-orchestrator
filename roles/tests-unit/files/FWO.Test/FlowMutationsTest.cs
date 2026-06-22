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
            Assert.That(FlowMutations.upsertFlowNwGroupMapping, Does.Contain("mutation upsertFlowNwGroupMapping"));
            Assert.That(FlowMutations.updateFlowNwGroup, Does.Contain("mutation updateFlowNwGroup"));
            Assert.That(FlowMutations.updateFlowNwObject, Does.Contain("mutation updateFlowNwObject"));
            Assert.That(FlowMutations.upsertFlowSvcObjectMapping, Does.Contain("mutation upsertFlowSvcObjectMapping"));
            Assert.That(FlowMutations.upsertFlowSvcGroupMapping, Does.Contain("mutation upsertFlowSvcGroupMapping"));
            Assert.That(FlowMutations.updateFlowSvcGroup, Does.Contain("mutation updateFlowSvcGroup"));
            Assert.That(FlowMutations.updateFlowSvcObject, Does.Contain("mutation updateFlowSvcObject"));
            Assert.That(FlowMutations.upsertFlowTimeObjectMapping, Does.Contain("mutation upsertFlowTimeObjectMapping"));
            Assert.That(FlowMutations.updateFlowTimeObject, Does.Contain("mutation updateFlowTimeObject"));
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
