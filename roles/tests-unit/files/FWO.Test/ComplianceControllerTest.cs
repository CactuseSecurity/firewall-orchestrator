using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using NUnit.Framework;
using Microsoft.AspNetCore.Mvc;
using NetTools;
using System.Net;

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
                [new ComplianceNetworkZone
                {
                    Id = 99,
                    Name = "DMZ",
                    Description = "Demilitarized zone",
                    IPRanges = [new IPAddressRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.0.0.255"))]
                }]);
            ComplianceController controller = new(apiConnection, new ComplianceCheckStatusTracker());

            ActionResult<List<ComplianceDesignatedZoneResponse>> result = await controller.GetDesignatedZoneMatrixZones();

            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            List<ComplianceDesignatedZoneResponse> zones = ((OkObjectResult)result.Result!).Value as List<ComplianceDesignatedZoneResponse> ?? [];
            Assert.That(zones, Has.Count.EqualTo(1));
            Assert.That(zones[0].Id, Is.EqualTo(99));
            Assert.That(zones[0].Name, Is.EqualTo("DMZ"));
            Assert.That(zones[0].Description, Is.EqualTo("Demilitarized zone"));
            Assert.That(zones[0].IpRanges, Has.Count.EqualTo(1));
            Assert.That(zones[0].IpRanges[0].IpStart, Is.EqualTo("10.0.0.0"));
            Assert.That(zones[0].IpRanges[0].IpEnd, Is.EqualTo("10.0.0.255"));
        }

        [Test]
        public async Task GetDesignatedZoneMatrixZones_PassesConfiguredMatrixIdToGraphQl()
        {
            DummyApiConnection apiConnection = new(
                [new ConfigItem { Key = "complianceDesignatedZoneMatrix", Value = "12", User = 0 }],
                [new ComplianceNetworkZone { Id = 99, Name = "DMZ" }]);
            ComplianceController controller = new(apiConnection, new ComplianceCheckStatusTracker());

            _ = await controller.GetDesignatedZoneMatrixZones();

            Assert.That(apiConnection.LastNetworkZoneQuery, Is.EqualTo(ComplianceQueries.getNetworkZonesForMatrix));
            Assert.That(GetAnonymousProperty<int>(apiConnection.LastNetworkZoneQueryVariables, "criterionId"), Is.EqualTo(12));
        }

        [Test]
        public async Task GetDesignatedZoneMatrixZones_ReturnsEmptyListWhenNoMatrixConfigured()
        {
            DummyApiConnection apiConnection = new();
            ComplianceController controller = new(apiConnection, new ComplianceCheckStatusTracker());

            ActionResult<List<ComplianceDesignatedZoneResponse>> result = await controller.GetDesignatedZoneMatrixZones();

            Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
            List<ComplianceDesignatedZoneResponse> zones = ((OkObjectResult)result.Result!).Value as List<ComplianceDesignatedZoneResponse> ?? [];
            Assert.That(zones, Is.Empty);
            Assert.That(apiConnection.LastNetworkZoneQuery, Is.Null);
            Assert.That(apiConnection.NetworkZoneQueryCount, Is.EqualTo(0));
        }

        private sealed class DummyApiConnection : ApiConnection
        {
            private readonly ConfigItem[] configItems;
            private readonly List<ComplianceNetworkZone> zones;

            public string? LastNetworkZoneQuery { get; private set; }
            public object? LastNetworkZoneQueryVariables { get; private set; }
            public int NetworkZoneQueryCount { get; private set; }

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
                    LastNetworkZoneQuery = query;
                    LastNetworkZoneQueryVariables = variables;
                    NetworkZoneQueryCount++;
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

        private static T GetAnonymousProperty<T>(object? obj, string propertyName)
        {
            Assert.That(obj, Is.Not.Null);
            object? value = obj!.GetType().GetProperty(propertyName)?.GetValue(obj);
            Assert.That(value, Is.Not.Null, $"Expected property '{propertyName}' to exist and have a value.");
            return (T)value!;
        }
    }
}
