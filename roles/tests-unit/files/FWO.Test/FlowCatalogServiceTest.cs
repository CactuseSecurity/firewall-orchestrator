using FWO.Config.Api;
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

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

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

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        List<ServiceObjectResponse> result = await service.GetServiceObjectsAsync(false);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Protocol, Is.EqualTo("250"));
        Assert.That(apiConnection.SentQueries[0], Is.EqualTo(FlowQueries.getFlowServiceObjects));
        AssertWhereClauseContains(GetWhereClause(apiConnection.SentVariables[0]), ("show_in_request_module", false));
    }

    [Test]
    public async Task GetServiceObjectsAsync_PreservesNullPorts()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.ServiceObjects =
        [
            new FlowSvcObject { Id = 12, Name = "ANY", ProtoId = 0 }
        ];

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        List<ServiceObjectResponse> result = await service.GetServiceObjectsAsync(null);

        Assert.That(result[0].PortStart, Is.Null);
        Assert.That(result[0].PortEnd, Is.Null);
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

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        List<AddressGroupResponse> result = await service.GetAddressGroupsAsync(null);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ShowInRequest, Is.False);
        Assert.That(result[0].Members, Has.Count.EqualTo(1));
        Assert.That(result[0].Members[0].Name, Is.EqualTo("HostA"));
        Assert.That(apiConnection.SentQueries[0], Is.EqualTo(FlowQueries.getFlowAddressGroups));
        Assert.That(GetWhereClause(apiConnection.SentVariables[0]), Is.Empty);
    }

    [Test]
    public async Task GetSeparatedAddressGroupsAsync_SplitsZoneGroupsByConfiguredPatterns()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.AddressGroups = BuildSeparationTestGroups();

        GlobalConfig globalConfig = new()
        {
            FlowZoneGroupNamePatterns =
                "[{\"matchType\":\"Suffix\",\"caseSensitive\":false,\"value\":\"_zone\"},{\"matchType\":\"Suffix\",\"caseSensitive\":true,\"value\":\"-zone\"}]"
        };
        FlowCatalogService service = new(apiConnection, globalConfig);

        SeparatedAddressGroupsResponse result = await service.GetSeparatedAddressGroupsAsync(true);

        Assert.Multiple(() =>
        {
            Assert.That(result.ZoneGroups.Select(group => group.Name), Is.EqualTo(new List<string> { "dmz_zone", "dmz_ZONE", "dmz-zone" }));
            Assert.That(result.StandardGroups.Select(group => group.Name), Is.EqualTo(new List<string> { "DMZ-Servers", "dmz-ZONE" }));
        });
        Assert.That(apiConnection.SentQueries[0], Is.EqualTo(FlowQueries.getFlowAddressGroups));
        Assert.That(GetWhereClause(apiConnection.SentVariables[0]), Is.Not.Empty);
    }

    [Test]
    public async Task GetSeparatedAddressGroupsAsync_MapsMembersAndKeepsGroupDetails()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.AddressGroups =
        [
            new FlowNwGroup
            {
                Id = 501,
                Name = "zone1",
                State = FlowState.Implemented,
                ShowInRequestModule = true,
                NwGroupMembers =
                [
                    new FlowNwGroupMember
                    {
                        NwGroupId = 501,
                        NwObjectId = 502,
                        NwObject = new FlowNwObject { Id = 502, Name = "subnet1-from-zone1" }
                    }
                ]
            }
        ];

        GlobalConfig globalConfig = new()
        {
            FlowZoneGroupNamePatterns = "[{\"matchType\":\"Prefix\",\"caseSensitive\":false,\"value\":\"zone\"}]"
        };
        FlowCatalogService service = new(apiConnection, globalConfig);

        SeparatedAddressGroupsResponse result = await service.GetSeparatedAddressGroupsAsync(null);

        Assert.That(result.StandardGroups, Is.Empty);
        Assert.That(result.ZoneGroups, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.ZoneGroups[0].Id, Is.EqualTo(501));
            Assert.That(result.ZoneGroups[0].State, Is.EqualTo(FlowState.Implemented));
            Assert.That(result.ZoneGroups[0].ShowInRequest, Is.True);
            Assert.That(result.ZoneGroups[0].Members, Has.Count.EqualTo(1));
            Assert.That(result.ZoneGroups[0].Members[0].Id, Is.EqualTo(502));
            Assert.That(result.ZoneGroups[0].Members[0].Name, Is.EqualTo("subnet1-from-zone1"));
        });
    }

    [Test]
    public async Task GetSeparatedAddressGroupsAsync_WithoutConfiguredPatterns_ReturnsAllGroupsAsStandardGroups()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.AddressGroups = BuildSeparationTestGroups();

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        SeparatedAddressGroupsResponse result = await service.GetSeparatedAddressGroupsAsync(null);

        Assert.That(result.ZoneGroups, Is.Empty);
        Assert.That(result.StandardGroups, Has.Count.EqualTo(5));
    }

    private static List<FlowNwGroup> BuildSeparationTestGroups()
    {
        return
        [
            new FlowNwGroup { Id = 201, Name = "DMZ-Servers", State = FlowState.Implemented },
            new FlowNwGroup { Id = 501, Name = "dmz_zone", State = FlowState.Implemented },
            new FlowNwGroup { Id = 502, Name = "dmz_ZONE", State = FlowState.Implemented },
            new FlowNwGroup { Id = 503, Name = "dmz-zone", State = FlowState.Implemented },
            new FlowNwGroup { Id = 504, Name = "dmz-ZONE", State = FlowState.Implemented }
        ];
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

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        List<TimeObjectResponse> result = await service.GetTimeObjectsAsync(null);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].StartTime, Does.StartWith("2026-06-01T08:00:00"));
        Assert.That(result[0].EndTime, Does.StartWith("2026-06-01T17:30:00"));
        Assert.That(result[0].ShowInRequest, Is.True);
    }

    [Test]
    public async Task GetTimeObjectIdAsync_ReturnsMatchingObjectAndAppliesVisibilityFilter()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.TimeObjects =
        [
            new FlowTimeObject
            {
                Id = 31,
                Name = "BusinessHours"
            }
        ];

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        TimeObjectIdResponse result = await service.GetTimeObjectIdAsync(
            new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 17, 30, 0, TimeSpan.Zero),
            true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(31));
            Assert.That(result.Name, Is.EqualTo("BusinessHours"));
            Assert.That(apiConnection.SentQueries[0], Is.EqualTo(FlowQueries.getFlowTimeObjectId));
            AssertWhereClauseContains(GetWhereClause(apiConnection.SentVariables[0]),
                ("start_time", new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero)),
                ("end_time", new DateTimeOffset(2026, 6, 1, 17, 30, 0, TimeSpan.Zero)),
                ("show_in_request_module", true));
        });
    }

    [Test]
    public async Task GetTimeObjectIdAsync_AllowsStartOnlyLookups()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.TimeObjects =
        [
            new FlowTimeObject
            {
                Id = 33,
                Name = "DeadlineOnly"
            }
        ];

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        TimeObjectIdResponse result = await service.GetTimeObjectIdAsync(
            new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            null,
            null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(33));
            Assert.That(result.Name, Is.EqualTo("DeadlineOnly"));
            AssertWhereClauseContainsLookup(GetWhereClause(apiConnection.SentVariables[0]),
                ("start_time", new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero)),
                ("end_time", null));
        });
    }

    [Test]
    public async Task GetTimeObjectIdAsync_AllowsEndOnlyLookups()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.TimeObjects =
        [
            new FlowTimeObject
            {
                Id = 34,
                Name = "StartOnly"
            }
        ];

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        TimeObjectIdResponse result = await service.GetTimeObjectIdAsync(
            null,
            new DateTimeOffset(2026, 6, 1, 17, 30, 0, TimeSpan.Zero),
            false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(34));
            Assert.That(result.Name, Is.EqualTo("StartOnly"));
            AssertWhereClauseContainsLookup(GetWhereClause(apiConnection.SentVariables[0]),
                ("start_time", null),
                ("end_time", new DateTimeOffset(2026, 6, 1, 17, 30, 0, TimeSpan.Zero)),
                ("show_in_request_module", false));
        });
    }

    [Test]
    public async Task GetTimeObjectIdAsync_NormalizesNullNameToEmptyString()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.TimeObjects =
        [
            new FlowTimeObject
            {
                Id = 32,
                Name = null!
            }
        ];

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        TimeObjectIdResponse result = await service.GetTimeObjectIdAsync(
            new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 17, 30, 0, TimeSpan.Zero),
            null);

        Assert.That(result.Name, Is.EqualTo(string.Empty));
        Assert.That(result.Id, Is.EqualTo(32));
    }

    [Test]
    public async Task GetTimeObjectIdAsync_ReturnsEmptyResponseWhenNoMatchExists()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        TimeObjectIdResponse result = await service.GetTimeObjectIdAsync(
            new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 17, 30, 0, TimeSpan.Zero),
            null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(0));
            Assert.That(result.Name, Is.EqualTo(string.Empty));
            Assert.That(apiConnection.SentQueries[0], Is.EqualTo(FlowQueries.getFlowTimeObjectId));
        });
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

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        List<AddressObjectResponse> result = await service.GetAddressObjectsAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("HostA"));
            Assert.That(result[0].ShowInRequest, Is.True);
        });
    }

    [TestCase("10.0.0.1", "10.0.0.1", "host")]
    [TestCase("10.0.0.0", "10.0.0.255", "network")]
    [TestCase("10.0.0.1", "10.0.0.255", "range")]
    [TestCase("10.0.0.1/32", "10.0.0.1/32", "host")]
    [TestCase("2001:db8::", "2001:db8::ffff", "network")]
    [TestCase(null, null, "fqdn")]
    [TestCase("", "", "fqdn")]
    public async Task GetAddressObjectsAsync_ResolvesAddressType(string? ipStart, string? ipEnd, string expectedType)
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.AddressObjects =
        [
            new FlowNwObject
            {
                Id = 16,
                Name = "AddressObject",
                IpStart = ipStart,
                IpEnd = ipEnd
            }
        ];

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        List<AddressObjectResponse> result = await service.GetAddressObjectsAsync(null);

        Assert.That(result[0].Type, Is.EqualTo(expectedType));
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

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

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

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

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

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

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
    public async Task GetServiceObjectIdAsync_LooksUpNullPorts()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        apiConnection.Protocols = [new IpProtocol { Id = 0, Name = "ANY" }];
        apiConnection.ServiceObjects = [new FlowSvcObject { Id = 51, Name = "ANY", ProtoId = 0 }];

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

        ServiceObjectIdResponse result = await service.GetServiceObjectIdAsync("ANY", null, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(51));
            Assert.That(apiConnection.SentQueries[1], Is.EqualTo(FlowQueries.getFlowServiceObjectId));
            AssertWhereClauseContainsLookup(GetWhereClause(apiConnection.SentVariables[1]),
                ("port_start", null),
                ("port_end", null),
                ("ip_proto_id", 0));
        });
    }

    [Test]
    public async Task GetServiceObjectIdAsync_ReturnsEmptyResponseForUnknownProtocol()
    {
        FlowCatalogServiceApiConn apiConnection = new();
        FlowCatalogService service = new(apiConnection, new GlobalConfig());

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

        FlowCatalogService service = new(apiConnection, new GlobalConfig());

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

    private static void AssertWhereClauseContainsLookup(Dictionary<string, object> whereClause, params (string FieldName, object? ExpectedValue)[] conditions)
    {
        foreach ((string fieldName, object? expectedValue) in conditions)
        {
            Assert.That(whereClause.TryGetValue(fieldName, out object? conditionObject), Is.True, $"Missing where clause for {fieldName}.");
            Assert.That(conditionObject, Is.TypeOf<Dictionary<string, object>>(), $"Expected lookup expression for {fieldName}.");
            Dictionary<string, object> expression = (Dictionary<string, object>)conditionObject!;

            if (expectedValue == null)
            {
                Assert.That(expression.TryGetValue("_is_null", out object? isNullValue), Is.True, $"Missing _is_null for {fieldName}.");
                Assert.That(isNullValue, Is.EqualTo(true), $"Unexpected null predicate for {fieldName}.");
                continue;
            }

            Assert.That(expression.TryGetValue("_eq", out object? actualValue), Is.True, $"Missing _eq for {fieldName}.");
            Assert.That(actualValue, Is.EqualTo(expectedValue), $"Unexpected value for {fieldName}.");
        }
    }
}
