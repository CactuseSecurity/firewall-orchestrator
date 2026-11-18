using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the target management of implementation tasks the workflow handler resolves with the shared resolver: the
    /// management of the gateway wins, a stored management must match it, and only tasks without gateway fall back.
    /// </summary>
    [TestFixture]
    internal class WfHandlerImplTaskTargetManagementTest
    {
        private const int kGatewayManagementId = 21;
        private const int kOtherManagementId = 22;
        private const int kRequestManagementId = 23;
        private const int kGatewayId = 1;
        private const int kUnknownGatewayId = 99;

        private static WfHandler CreateHandler()
        {
            WfHandler handler = new(new SimulatedUserConfig(), new InMemoryDeviceSpecificSettingsApiConnection(), WorkflowPhases.implementation, null);
            handler.Devices =
            [
                new Device { Id = kGatewayId, Name = "gw-a", Management = new Management { Id = kGatewayManagementId } }
            ];
            return handler;
        }

        private static WfReqTask RequestTask() => new() { Id = 1, ManagementId = kRequestManagementId };

        private static WfImplTask ImplTask(int? managementId, int? gatewayId) => new() { Id = 11, ManagementId = managementId, DeviceId = gatewayId };

        [Test]
        public async Task GatewayTaskWithoutStoredManagement_TargetsTheManagementOfTheGateway()
        {
            int? managementId = await CreateHandler().ResolveImplTaskManagementId(RequestTask(), ImplTask(null, kGatewayId));

            Assert.That(managementId, Is.EqualTo(kGatewayManagementId), "the management of the request task is no fallback for a gateway task");
        }

        [Test]
        public async Task GatewayTaskWithMatchingManagement_TargetsIt()
        {
            int? managementId = await CreateHandler().ResolveImplTaskManagementId(RequestTask(), ImplTask(kGatewayManagementId, kGatewayId));

            Assert.That(managementId, Is.EqualTo(kGatewayManagementId));
        }

        [Test]
        public async Task GatewayTaskWithOtherManagementOrUnknownGateway_IsNotResolved()
        {
            WfHandler handler = CreateHandler();

            int? mismatch = await handler.ResolveImplTaskManagementId(RequestTask(), ImplTask(kOtherManagementId, kGatewayId));
            int? unknownGateway = await handler.ResolveImplTaskManagementId(RequestTask(), ImplTask(kGatewayManagementId, kUnknownGatewayId));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(mismatch, Is.Null);
                Assert.That(unknownGateway, Is.Null);
            }
        }

        [Test]
        public async Task TaskWithoutGateway_TargetsItsOwnElseTheRequestManagement()
        {
            WfHandler handler = CreateHandler();

            int? own = await handler.ResolveImplTaskManagementId(RequestTask(), ImplTask(kOtherManagementId, null));
            int? fallback = await handler.ResolveImplTaskManagementId(RequestTask(), ImplTask(null, null));
            int? none = await handler.ResolveImplTaskManagementId(null, ImplTask(null, null));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(own, Is.EqualTo(kOtherManagementId));
                Assert.That(fallback, Is.EqualTo(kRequestManagementId));
                Assert.That(none, Is.Null);
            }
        }
    }
}
