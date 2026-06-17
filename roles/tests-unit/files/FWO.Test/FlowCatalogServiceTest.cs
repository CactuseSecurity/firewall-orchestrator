using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using NUnit.Framework;
using System.Threading;

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
                State = FlowState.Requested,
                ShowInRequestModule = true
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
        Assert.That(result[0].ShowInRequest, Is.True);
        Assert.That(apiConnection.SentQueries, Has.Count.EqualTo(2));
        Assert.That(apiConnection.SentQueries[0], Is.EqualTo(FlowQueries.getFlowServiceObjects));
        AssertWhereClauseContains(GetWhereClause(apiConnection.SentVariables[0]), ("show_in_request_module", true));
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
        Assert.That(apiConnection.SentQueries[0], Is.EqualTo(FlowQueries.getFlowServiceObjects));
        AssertWhereClauseContains(GetWhereClause(apiConnection.SentVariables[0]), ("show_in_request_module", false));
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
                ShowInRequestModule = false,
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
        Assert.That(result[0].ShowInRequest, Is.False);
        Assert.That(result[0].Members, Has.Count.EqualTo(1));
        Assert.That(result[0].Members[0].Name, Is.EqualTo("HostA"));
        Assert.That(apiConnection.SentQueries[0], Is.EqualTo(FlowQueries.getFlowAddressGroups));
        Assert.That(GetWhereClause(apiConnection.SentVariables[0]), Is.Empty);
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
                State = FlowState.Requested,
                ShowInRequestModule = true
            }
        ];

        FlowCatalogService service = new(apiConnection);

        List<TimeObjectResponse> result = await service.GetTimeObjectsAsync(null);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].StartTime, Does.StartWith("2026-06-01T08:00:00"));
        Assert.That(result[0].EndTime, Does.StartWith("2026-06-01T17:30:00"));
        Assert.That(result[0].ShowInRequest, Is.True);
    }

    [Test]
    public async Task GetAddressObjectsAsync_MapsShowInRequestFlag()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.AddressObjects =
        [
            new FlowNwObject
            {
                Id = 15,
                Name = "HostA",
                IpStart = "10.0.0.1",
                IpEnd = "10.0.0.1",
                State = FlowState.Requested,
                ShowInRequestModule = true
            }
        ];

        FlowCatalogService service = new(apiConnection);

        List<AddressObjectResponse> result = await service.GetAddressObjectsAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("HostA"));
            Assert.That(result[0].ShowInRequest, Is.True);
        });
    }

    [Test]
    public async Task GetServiceGroupsAsync_MapsShowInRequestFlag()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.ServiceGroups =
        [
            new FlowSvcGroup
            {
                Id = 25,
                Name = "Web",
                State = FlowState.Implemented,
                ShowInRequestModule = true,
                SvcGroupMembers =
                [
                    new FlowSvcGroupMember
                    {
                        SvcGroupId = 25,
                        SvcObjectId = 200,
                        SvcObject = new FlowSvcObject { Id = 200, Name = "HTTPS" }
                    }
                ]
            }
        ];

        FlowCatalogService service = new(apiConnection);

        List<ServiceGroupResponse> result = await service.GetServiceGroupsAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].ShowInRequest, Is.True);
            Assert.That(result[0].Members, Has.Count.EqualTo(1));
            Assert.That(result[0].Members[0].Name, Is.EqualTo("HTTPS"));
        });
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
            Assert.That(apiConnection.SentQueries[0], Is.EqualTo(FlowQueries.getFlowAddressObjectId));
            AssertWhereClauseContains(GetWhereClause(apiConnection.SentVariables[0]),
                ("ip_start", "10.0.0.1"),
                ("ip_end", "10.0.0.2"),
                ("show_in_request_module", true));
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
            Assert.That(apiConnection.SentQueries[1], Is.EqualTo(FlowQueries.getFlowServiceObjectId));
            AssertWhereClauseContains(GetWhereClause(apiConnection.SentVariables[1]),
                ("port_start", 443),
                ("port_end", 443),
                ("ip_proto_id", 6),
                ("show_in_request_module", false));
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

    [Test]
    public async Task GetServiceObjectsAsync_LoadsProtocolCacheOnlyOnceForConcurrentRequests()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.ServiceObjects =
        [
            new FlowSvcObject
            {
                Id = 60,
                Name = "HTTPS",
                PortStart = 443,
                PortEnd = 443,
                ProtoId = 6
            }
        ];
        apiConnection.Protocols =
        [
            new IpProtocol { Id = 6, Name = "TCP" }
        ];

        TaskCompletionSource<bool> protocolQueryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseProtocolQuery = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int protocolQueryCount = 0;
        apiConnection.BeforeSendQueryAsync = async (responseType, query) =>
        {
            if (responseType != typeof(List<IpProtocol>) || query != StmQueries.getIpProtocols)
            {
                return;
            }

            Interlocked.Increment(ref protocolQueryCount);
            protocolQueryStarted.TrySetResult(true);
            await releaseProtocolQuery.Task;
        };

        FlowCatalogService service = new(apiConnection);

        Task<List<ServiceObjectResponse>> firstCall = service.GetServiceObjectsAsync(null);
        await protocolQueryStarted.Task;
        Task<List<ServiceObjectResponse>> secondCall = service.GetServiceObjectsAsync(null);
        releaseProtocolQuery.TrySetResult(true);

        List<ServiceObjectResponse>[] results = await Task.WhenAll(firstCall, secondCall);

        Assert.Multiple(() =>
        {
            Assert.That(protocolQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.SentQueries.FindAll(query => query == StmQueries.getIpProtocols), Has.Count.EqualTo(1));
            Assert.That(results[0], Has.Count.EqualTo(1));
            Assert.That(results[1], Has.Count.EqualTo(1));
            Assert.That(results[0][0].Protocol, Is.EqualTo("TCP"));
            Assert.That(results[1][0].Protocol, Is.EqualTo("TCP"));
        });
    }

    private sealed class FlowCatalogServiceApiConn : SimulatedApiConnection
    {
        public List<string> SentQueries { get; } = [];
        public List<object?> SentVariables { get; } = [];
        public Func<Type, string, Task>? BeforeSendQueryAsync { get; set; }
        public List<IpProtocol> Protocols { get; set; } = [];
        public List<FlowNwObject> AddressObjects { get; set; } = [];
        public List<FlowNwGroup> AddressGroups { get; set; } = [];
        public List<FlowSvcObject> ServiceObjects { get; set; } = [];
        public List<FlowSvcGroup> ServiceGroups { get; set; } = [];
        public List<FlowTimeObject> TimeObjects { get; set; } = [];

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            SentQueries.Add(query);
            SentVariables.Add(variables);

            Type responseType = typeof(QueryResponseType);
            if (BeforeSendQueryAsync != null)
            {
                await BeforeSendQueryAsync(responseType, query);
            }

            if (responseType == typeof(List<FlowNwObject>))
            {
                return (QueryResponseType)(object)AddressObjects;
            }

            if (responseType == typeof(List<FlowNwGroup>))
            {
                return (QueryResponseType)(object)AddressGroups;
            }

            if (responseType == typeof(List<FlowSvcObject>))
            {
                return (QueryResponseType)(object)ServiceObjects;
            }

            if (responseType == typeof(List<FlowSvcGroup>))
            {
                return (QueryResponseType)(object)ServiceGroups;
            }

            if (responseType == typeof(List<FlowTimeObject>))
            {
                return (QueryResponseType)(object)TimeObjects;
            }

            if (responseType == typeof(List<IpProtocol>))
            {
                return (QueryResponseType)(object)Protocols;
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

    private static Dictionary<string, object> GetWhereClause(object? variables)
    {
        Assert.That(variables, Is.TypeOf<Dictionary<string, object>>());
        Dictionary<string, object> queryVariables = (Dictionary<string, object>)variables!;
        Assert.That(queryVariables.TryGetValue("where", out object? whereObject), Is.True);
        Assert.That(whereObject, Is.TypeOf<Dictionary<string, object>>());
        return (Dictionary<string, object>)whereObject!;
    }

    private static void AssertWhereClauseContains(Dictionary<string, object> whereClause, params (string FieldName, object ExpectedValue)[] conditions)
    {
        foreach ((string fieldName, object expectedValue) in conditions)
        {
            Assert.That(whereClause.TryGetValue(fieldName, out object? conditionObject), Is.True, $"Missing where clause for {fieldName}.");
            Assert.That(conditionObject, Is.TypeOf<Dictionary<string, object>>(), $"Expected _eq expression for {fieldName}.");
            Dictionary<string, object> equalsExpression = (Dictionary<string, object>)conditionObject!;
            Assert.That(equalsExpression.TryGetValue("_eq", out object? actualValue), Is.True, $"Missing _eq for {fieldName}.");
            Assert.That(actualValue, Is.EqualTo(expectedValue), $"Unexpected value for {fieldName}.");
        }
    }
}
