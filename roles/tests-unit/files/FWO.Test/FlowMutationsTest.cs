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
            Assert.That(FlowMutations.updateFlowNwObject, Does.Contain("mutation updateFlowNwObject"));
            Assert.That(FlowMutations.updateFlowSvcGroup, Does.Contain("mutation updateFlowSvcGroup"));
            Assert.That(FlowMutations.updateFlowSvcObject, Does.Contain("mutation updateFlowSvcObject"));
            Assert.That(FlowMutations.updateFlowTimeObject, Does.Contain("mutation updateFlowTimeObject"));
        }
    }
}
