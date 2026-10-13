using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerImplTaskManagementNameTest
    {
        private const int kVisibleManagementId = 21;
        private const int kHiddenManagementId = 22;
        private const int kRequestManagementId = 23;
        private const int kVisibleGatewayId = 1;
        private const int kHiddenGatewayId = 2;
        private const string kVisibleManagementName = "domain-a";
        private const string kRequestManagementName = "domain-c";
        private const string kNotVisible = "Not Visible";

        private static WfHandler CreateHandler()
        {
            WfHandler handler = new(new SimulatedUserConfig(), new InMemoryProvisioningApiConnection(), WorkflowPhases.planning, null);
            handler.Managements =
            [
                new Management { Id = kVisibleManagementId, Name = kVisibleManagementName },
                new Management { Id = kRequestManagementId, Name = kRequestManagementName }
            ];
            handler.Devices =
            [
                new Device { Id = kVisibleGatewayId, Name = "gw-a", Management = new Management { Id = kVisibleManagementId } }
            ];
            return handler;
        }

        [Test]
        public void ReturnsTheNameOfTheTargetManagementOfTheTask()
        {
            WfImplTask implTask = new() { ManagementId = kVisibleManagementId };

            Assert.That(CreateHandler().GetImplTaskManagementName(implTask, kRequestManagementId), Is.EqualTo(kVisibleManagementName));
        }

        [Test]
        public void ReturnsThePlaceholderForATargetManagementNotVisibleToTheUser()
        {
            WfImplTask implTask = new() { ManagementId = kHiddenManagementId };

            Assert.That(CreateHandler().GetImplTaskManagementName(implTask, kRequestManagementId), Is.EqualTo(kNotVisible));
        }

        [Test]
        public void ReturnsTheManagementOfTheGatewayWhenTheTaskHasNoOwnManagement()
        {
            WfImplTask implTask = new() { DeviceId = kVisibleGatewayId };

            Assert.That(CreateHandler().GetImplTaskManagementName(implTask, kRequestManagementId), Is.EqualTo(kVisibleManagementName));
        }

        [Test]
        public void ReturnsThePlaceholderForAGatewayNotVisibleToTheUser()
        {
            WfImplTask implTask = new() { DeviceId = kHiddenGatewayId };

            Assert.That(CreateHandler().GetImplTaskManagementName(implTask, kRequestManagementId), Is.EqualTo(kNotVisible));
        }

        [Test]
        public void ReturnsTheRequestManagementWhenTheTaskHasNeitherManagementNorGateway()
        {
            WfImplTask implTask = new();

            Assert.That(CreateHandler().GetImplTaskManagementName(implTask, kRequestManagementId), Is.EqualTo(kRequestManagementName));
        }

        [Test]
        public void ReturnsEmptyWhenNoManagementIsKnown()
        {
            WfImplTask implTask = new() { DeviceId = 0 };

            Assert.That(CreateHandler().GetImplTaskManagementName(implTask, null), Is.Empty);
        }

        [Test]
        public async Task LoadVisibleManagementsTakesTheManagementsTheUserSees()
        {
            WfHandler handler = CreateHandler();
            List<Management> visibleManagements = [new Management { Id = kVisibleManagementId, Name = kVisibleManagementName }];

            await InvokeLoadVisibleManagements(handler, new ManagementApiConnection(visibleManagements));

            Assert.That(handler.Managements, Is.EqualTo(visibleManagements));
        }

        [Test]
        public async Task LoadVisibleManagementsKeepsTheInitializationRunningWhenTheQueryFails()
        {
            WfHandler handler = CreateHandler();

            await InvokeLoadVisibleManagements(handler, new ManagementApiConnection(null));

            Assert.Multiple(() =>
            {
                Assert.That(handler.Managements, Is.Empty);
                Assert.That(handler.GetImplTaskManagementName(new WfImplTask { ManagementId = kVisibleManagementId }, null), Is.EqualTo(kNotVisible));
            });
        }

        private static async Task InvokeLoadVisibleManagements(WfHandler handler, ApiConnection apiConnection)
        {
            MethodInfo method = typeof(WfHandler).GetMethod("LoadVisibleManagements", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(nameof(WfHandler), "LoadVisibleManagements");
            object[] arguments = [apiConnection];
            await (Task)method.Invoke(handler, arguments)!;
        }

        /// <summary>
        /// Answers the management query with the given managements, or fails when there are none.
        /// </summary>
        private sealed class ManagementApiConnection(List<Management>? managements) : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null,
                FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == DeviceQueries.getManagementHierarchy && managements != null)
                {
                    return Task.FromResult((QueryResponseType)(object)managements);
                }
                throw new InvalidOperationException("Unexpected query " + query);
            }
        }
    }
}
