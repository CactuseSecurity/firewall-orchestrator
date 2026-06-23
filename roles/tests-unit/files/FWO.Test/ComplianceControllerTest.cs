using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Services;
using NUnit.Framework;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Test
{
    [TestFixture]
    internal class ComplianceControllerTest
    {
        [Test]
        public void GetInitialComplianceCheckStatus_ReturnsNotFoundForUnknownJob()
        {
            ComplianceController controller = new(new DummyApiConnection(), new ComplianceCheckStatusTracker());

            var result = controller.GetInitialComplianceCheckStatus("missing");

            Assert.That(result.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.NotFoundResult>());
        }

        [Test]
        public void StartInitialComplianceCheck_ReturnsConflictWhenJobAlreadyActive()
        {
            ComplianceCheckStatusTracker tracker = new();
            tracker.CreateQueuedJob();
            ComplianceController controller = new(new DummyApiConnection(), tracker);

            var result = controller.StartInitialComplianceCheck();

            Assert.That(result.Result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.ConflictObjectResult>());
        }

        [Test]
        public async Task GetDesignatedZoneMatrixZones_ReturnsZonesForConfiguredMatrix()
        {
            DummyApiConnection apiConnection = new(
                [new ConfigItem { Key = "complianceDesignatedZoneMatrix", Value = "12", User = 0 }],
                [new ComplianceNetworkZone { Id = 99, Name = "DMZ" }]);
            ComplianceController controller = new(apiConnection, new ComplianceCheckStatusTracker());

            ActionResult<List<ComplianceNetworkZone>> result = await controller.GetDesignatedZoneMatrixZones();

            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            List<ComplianceNetworkZone> zones = ((OkObjectResult)result.Result!).Value as List<ComplianceNetworkZone> ?? [];
            Assert.That(zones, Has.Count.EqualTo(1));
            Assert.That(zones[0].Id, Is.EqualTo(99));
            Assert.That(zones[0].Name, Is.EqualTo("DMZ"));
        }

        private sealed class DummyApiConnection : ApiConnection
        {
            private readonly ConfigItem[] configItems;
            private readonly List<ComplianceNetworkZone> zones;

            public DummyApiConnection(ConfigItem[]? configItems = null, List<ComplianceNetworkZone>? zones = null)
            {
                this.configItems = configItems ?? [];
                this.zones = zones ?? [];
            }

            public override void SetAuthHeader(string jwt) { }
            public override void SetRole(string role) { }
            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SwitchBack() { }
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(ConfigItem[]) && query == ConfigQueries.getConfigItemsByUser)
                {
                    return Task.FromResult((QueryResponseType)(object)configItems);
                }

                if (typeof(QueryResponseType) == typeof(List<ComplianceNetworkZone>) && query == ComplianceQueries.getNetworkZonesForMatrix)
                {
                    return Task.FromResult((QueryResponseType)(object)zones);
                }

                throw new NotImplementedException();
            }
            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null) => throw new NotImplementedException();
            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null) => throw new NotImplementedException();
            protected override void Dispose(bool disposing) { }
            public override void DisposeSubscriptions<T>() { }
            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct) => throw new NotImplementedException();
        }
    }
}
