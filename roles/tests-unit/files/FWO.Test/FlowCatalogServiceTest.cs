using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class FlowCatalogServiceTest
{
    [Test]
    public async Task GetServiceObjectsAsync_UsesReadableProtocolNamesAndFiltersWhenRequested()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.ServiceObjects =
        [
            new FlowSvcObject
            {
                Id = 10,
                Name = "HTTPS",
                PortStart = 443,
                PortEnd = 443,
                ProtoId = 6,
                State = FlowState.Requested
            }
        ];
        apiConnection.Protocols =
        [
            new IpProtocol { Id = 6, Name = "TCP" },
            new IpProtocol { Id = 17, Name = "UDP" }
        ];

        FlowCatalogService service = new(apiConnection);

        List<ServiceObjectResponse> result = await service.GetServiceObjectsAsync(true);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Protocol, Is.EqualTo("TCP"));
        Assert.That(apiConnection.SentQueries, Has.Count.EqualTo(2));
        Assert.That(apiConnection.SentQueries[0], Does.Contain("query getServiceObjects"));
        Assert.That(apiConnection.SentQueries[0], Does.Contain("show_in_request_module"));
        Assert.That(apiConnection.SentQueries[0], Does.Contain("_eq: true"));
        Assert.That(apiConnection.SentQueries[1], Is.EqualTo(StmQueries.getIpProtocols));
    }

    [Test]
    public async Task GetServiceObjectsAsync_FallsBackToProtocolIdWhenNameLookupFails()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.ServiceObjects =
        [
            new FlowSvcObject
            {
                Id = 11,
                Name = "Custom",
                PortStart = 1234,
                PortEnd = 1234,
                ProtoId = 250,
                State = FlowState.Requested
            }
        ];
        apiConnection.Protocols =
        [
            new IpProtocol { Id = 6, Name = "TCP" }
        ];

        FlowCatalogService service = new(apiConnection);

        List<ServiceObjectResponse> result = await service.GetServiceObjectsAsync(false);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Protocol, Is.EqualTo("250"));
        Assert.That(apiConnection.SentQueries[0], Does.Contain("query getServiceObjects"));
        Assert.That(apiConnection.SentQueries[0], Does.Contain("_eq: false"));
    }

    [Test]
    public async Task GetAddressGroupsAsync_MapsNestedMembers()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.AddressGroups =
        [
            new FlowNwGroup
            {
                Id = 20,
                Name = "Admins",
                State = FlowState.Implemented,
                NwGroupMembers =
                [
                    new FlowNwGroupMember
                    {
                        NwGroupId = 20,
                        NwObjectId = 100,
                        NwObject = new FlowNwObject { Id = 100, Name = "HostA" }
                    }
                ]
            }
        ];

        FlowCatalogService service = new(apiConnection);

        List<AddressGroupResponse> result = await service.GetAddressGroupsAsync(null);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Members, Has.Count.EqualTo(1));
        Assert.That(result[0].Members[0].Name, Is.EqualTo("HostA"));
        Assert.That(apiConnection.SentQueries[0], Does.Not.Contain("where: { show_in_request_module"));
    }

    [Test]
    public async Task GetTimeObjectsAsync_MapsTimestamps()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.TimeObjects =
        [
            new FlowTimeObject
            {
                Id = 30,
                Name = "BusinessHours",
                StartTime = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 6, 1, 17, 30, 0, DateTimeKind.Utc),
                State = FlowState.Requested
            }
        ];

        FlowCatalogService service = new(apiConnection);

        List<TimeObjectResponse> result = await service.GetTimeObjectsAsync(null);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].StartTime, Does.StartWith("2026-06-01T08:00:00"));
        Assert.That(result[0].EndTime, Does.StartWith("2026-06-01T17:30:00"));
    }

    [Test]
    public async Task GetAddressObjectIdAsync_ReturnsMatchingObjectAndAppliesVisibilityFilter()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.AddressObjects =
        [
            new FlowNwObject
            {
                Id = 40,
                Name = "HostX"
            }
        ];

        FlowCatalogService service = new(apiConnection);

        AddressObjectIdResponse result = await service.GetAddressObjectIdAsync("10.0.0.1", "10.0.0.2", true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(40));
            Assert.That(result.Name, Is.EqualTo("HostX"));
            Assert.That(apiConnection.SentQueries[0], Does.Contain("query getAddressObjectId"));
            Assert.That(apiConnection.SentQueries[0], Does.Contain("ip_start: { _eq: \"10.0.0.1\" }"));
            Assert.That(apiConnection.SentQueries[0], Does.Contain("ip_end: { _eq: \"10.0.0.2\" }"));
            Assert.That(apiConnection.SentQueries[0], Does.Contain("show_in_request_module: { _eq: true }"));
        });
    }

    [Test]
    public async Task GetServiceObjectIdAsync_ResolvesProtocolByName()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.ServiceObjects =
        [
            new FlowSvcObject
            {
                Id = 50,
                Name = "HTTPS",
                ProtoId = 6
            }
        ];
        apiConnection.Protocols =
        [
            new IpProtocol { Id = 6, Name = "TCP" },
            new IpProtocol { Id = 17, Name = "UDP" }
        ];

        FlowCatalogService service = new(apiConnection);

        ServiceObjectIdResponse result = await service.GetServiceObjectIdAsync("tcp", 443, 443, false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(50));
            Assert.That(result.Name, Is.EqualTo("HTTPS"));
            Assert.That(apiConnection.SentQueries[0], Is.EqualTo(StmQueries.getIpProtocols));
            Assert.That(apiConnection.SentQueries[1], Does.Contain("query getServiceObjectId"));
            Assert.That(apiConnection.SentQueries[1], Does.Contain("port_start: { _eq: 443 }"));
            Assert.That(apiConnection.SentQueries[1], Does.Contain("port_end: { _eq: 443 }"));
            Assert.That(apiConnection.SentQueries[1], Does.Contain("ip_proto_id: { _eq: 6 }"));
            Assert.That(apiConnection.SentQueries[1], Does.Contain("show_in_request_module: { _eq: false }"));
        });
    }

    [Test]
    public async Task GetServiceObjectIdAsync_ReturnsEmptyResponseForUnknownProtocol()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        FlowCatalogService service = new(apiConnection);

        ServiceObjectIdResponse result = await service.GetServiceObjectIdAsync("not-a-protocol", 443, 443, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(0));
            Assert.That(result.Name, Is.EqualTo(string.Empty));
            Assert.That(apiConnection.SentQueries, Has.Count.EqualTo(1));
            Assert.That(apiConnection.SentQueries[0], Is.EqualTo(StmQueries.getIpProtocols));
        });
    }

    private sealed class FlowCatalogServiceApiConn : SimulatedApiConnection
    {
        public List<string> SentQueries { get; } = [];
        public List<IpProtocol> Protocols { get; set; } = [];
        public List<FlowNwObject> AddressObjects { get; set; } = [];
        public List<FlowNwGroup> AddressGroups { get; set; } = [];
        public List<FlowSvcObject> ServiceObjects { get; set; } = [];
        public List<FlowSvcGroup> ServiceGroups { get; set; } = [];
        public List<FlowTimeObject> TimeObjects { get; set; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            SentQueries.Add(query);

            Type responseType = typeof(QueryResponseType);
            if (responseType == typeof(List<FlowNwObject>))
            {
                return Task.FromResult((QueryResponseType)(object)AddressObjects);
            }

            if (responseType == typeof(List<FlowNwGroup>))
            {
                return Task.FromResult((QueryResponseType)(object)AddressGroups);
            }

            if (responseType == typeof(List<FlowSvcObject>))
            {
                return Task.FromResult((QueryResponseType)(object)ServiceObjects);
            }

            if (responseType == typeof(List<FlowSvcGroup>))
            {
                return Task.FromResult((QueryResponseType)(object)ServiceGroups);
            }

            if (responseType == typeof(List<FlowTimeObject>))
            {
                return Task.FromResult((QueryResponseType)(object)TimeObjects);
            }

            if (responseType == typeof(List<IpProtocol>))
            {
                return Task.FromResult((QueryResponseType)(object)Protocols);
            }

            throw new NotImplementedException($"Unsupported response type {responseType.Name}");
        }

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            throw new NotImplementedException();
        }

        public override void SetAuthHeader(string jwt)
        {
        }

        public override void SetRole(string role)
        {
        }

        public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {
        }

        public override void SetProperRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {
        }

        public override void SwitchBack()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }

        public override void DisposeSubscriptions<T>()
        {
        }

        public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
