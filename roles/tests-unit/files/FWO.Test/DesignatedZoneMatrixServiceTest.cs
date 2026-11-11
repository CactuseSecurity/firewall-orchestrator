using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Compliance.RulePositioning;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.RulePositioning;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using NUnit.Framework;
using System.Net;
using DesignatedZoneMatrixReadResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Compliance.RulePositioning.DesignatedZoneMatrixReadValue,
    FWO.Compliance.RulePositioning.DesignatedZoneMatrixReadFailureCategory>;

namespace FWO.Test;

[TestFixture]
internal class DesignatedZoneMatrixServiceTest
{
    [Test]
    public async Task ResolveZonesForObjectsAsync_ReturnsOrderedUniqueZonesForNestedGroups()
    {
        DesignatedZoneMatrixServiceApiConn apiConnection = new(
            [new ConfigItem { Key = "designatedZoneMatrix", Value = "12", User = 0 }],
            [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
            [
                new NetworkZone
                {
                    Id = 20,
                    Name = "Backend",
                    Description = "Backend zone",
                    IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.1"))]
                },
                new NetworkZone
                {
                    Id = 10,
                    Name = "DMZ",
                    Description = "Demilitarized zone",
                    IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("10.0.1.1"), IPAddress.Parse("10.0.1.1"))]
                }
            ]);
        DesignatedZoneMatrixService service = CreateService(apiConnection, new SimulatedGlobalConfig { DesignatedZoneMatrixId = 12 });

        List<ComplianceDesignatedZoneResponse> result = await service.ResolveZonesForObjectsAsync(new ResolveZonesForObjectsRequest
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.GroupObjectRequest
                {
                    Name = "Root Group",
                    Members =
                    [
                        new ResolveZonesForObjectsRequest.GroupObjectRequest
                        {
                            Name = "Sub Group",
                            Members =
                            [
                                new ResolveZonesForObjectsRequest.LeafObjectRequest
                                {
                                    Name = "Backend Host",
                                    Type = "host",
                                    IpHost = "10.0.0.1"
                                },
                                new ResolveZonesForObjectsRequest.LeafObjectRequest
                                {
                                    Name = "Backend Host Duplicate",
                                    Type = "network",
                                    IpHost = "10.0.0.1"
                                },
                                new ResolveZonesForObjectsRequest.LeafObjectRequest
                                {
                                    Name = "DMZ Host",
                                    Type = "ip_range",
                                    IpHost = "10.0.1.1"
                                }
                            ]
                        }
                    ]
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Select(zone => zone.Name), Is.EqualTo(["Backend", "DMZ"]));
            Assert.That(apiConnection.MatrixQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.NetworkZoneQueryCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ResolveZonesForObjectsAsync_ReturnsEmptyWhenNoObjectsProvideRanges()
    {
        DesignatedZoneMatrixService service = CreateService(new DesignatedZoneMatrixServiceApiConn([], [], []), new SimulatedGlobalConfig { DesignatedZoneMatrixId = 12 });

        List<ComplianceDesignatedZoneResponse> result = await service.ResolveZonesForObjectsAsync(new ResolveZonesForObjectsRequest
        {
            Objects = []
        });

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task ResolveZonesForObjectsAsync_ReturnsEmptyWhenNoZonesAreLoaded()
    {
        DesignatedZoneMatrixServiceApiConn apiConnection = new(
            [new ConfigItem { Key = "designatedZoneMatrix", Value = "12", User = 0 }],
            [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
            []);
        DesignatedZoneMatrixService service = CreateService(apiConnection, new SimulatedGlobalConfig { DesignatedZoneMatrixId = 12 });

        List<ComplianceDesignatedZoneResponse> result = await service.ResolveZonesForObjectsAsync(new ResolveZonesForObjectsRequest
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Leaf",
                    Type = "network",
                    IpHost = "10.0.0.1"
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Empty);
            Assert.That(apiConnection.MatrixQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.NetworkZoneQueryCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ResolveZonesForObjectsAsync_DoesNotExposeSyntheticInternetFallbackZone()
    {
        DesignatedZoneMatrixServiceApiConn apiConnection = new(
            [new ConfigItem { Key = "designatedZoneMatrix", Value = "12", User = 0 }],
            [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
            [
                new NetworkZone
                {
                    Id = 10,
                    Name = "DMZ",
                    Description = "Demilitarized zone",
                    IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.1"))]
                }
            ]);
        DesignatedZoneMatrixService service = CreateService(apiConnection, new SimulatedGlobalConfig
        {
            DesignatedZoneMatrixId = 12,
            AutoCalculateInternetZone = false
        });

        List<ComplianceDesignatedZoneResponse> result = await service.ResolveZonesForObjectsAsync(new ResolveZonesForObjectsRequest
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "External Host",
                    Type = "host",
                    IpHost = "203.0.113.10"
                }
            ]
        });

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ResolveZonesForObjectsAsync_ReportsUnassignableIpv6Range()
    {
        ConfigItem[] configItems = [new ConfigItem { Key = "designatedZoneMatrix", Value = "12", User = 0 }];
        List<ComplianceCriterion> matrices = [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }];
        List<NetworkZone> zones = [new NetworkZone
        {
            Id = 10,
            Name = "IPv4 Zone",
            IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.1"))]
        }];
        DesignatedZoneMatrixServiceApiConn apiConnection = new(
            configItems,
            matrices,
            zones);
        DesignatedZoneMatrixService service = CreateService(apiConnection, new SimulatedGlobalConfig { DesignatedZoneMatrixId = 12 });
        ResolveZonesForObjectsRequest request = new()
        {
            Objects = [new ResolveZonesForObjectsRequest.LeafObjectRequest
            {
                Name = "IPv6 Host",
                Type = "host",
                IpHost = "2001:db8::1"
            }]
        };

        UnassignableIpRangesException? exception = Assert.ThrowsAsync<UnassignableIpRangesException>(
            async () => await service.ResolveZonesForObjectsAsync(request));

        Assert.That(exception!.Message, Does.Contain("2001:db8::1-2001:db8::1"));
    }

    /// <summary>
    /// Verifies the service normalizes the address of a leaf itself, including clearing host bits. IPv6 is used
    /// because only an unassignable IPv6 range is reported with the bounds the service derived.
    /// </summary>
    [TestCase("2001:db8::10", null, "2001:db8::10-2001:db8::10")]
    [TestCase(null, "2001:db8::/126", "2001:db8::-2001:db8::3")]
    [TestCase(null, "2001:db8::5/126", "2001:db8::4-2001:db8::7")]
    public void ResolveZonesForObjectsAsync_NormalizesLeafAddress(string? ipHost, string? ipNetwork, string expectedRange)
    {
        DesignatedZoneMatrixService service = CreateServiceWithSingleIpv4Zone();
        ResolveZonesForObjectsRequest request = new()
        {
            Objects = [new ResolveZonesForObjectsRequest.LeafObjectRequest
            {
                Name = "Leaf",
                Type = "network",
                IpHost = ipHost ?? string.Empty,
                IpNetwork = ipNetwork ?? string.Empty
            }]
        };

        UnassignableIpRangesException? exception = Assert.ThrowsAsync<UnassignableIpRangesException>(
            async () => await service.ResolveZonesForObjectsAsync(request));

        Assert.That(exception!.Message, Does.Contain(expectedRange));
    }

    /// <summary>
    /// Verifies an invalid address that bypassed the validator surfaces as a programming error naming the leaf.
    /// </summary>
    [Test]
    public void ResolveZonesForObjectsAsync_InvalidLeafAddress_ThrowsInvalidOperation()
    {
        DesignatedZoneMatrixService service = CreateServiceWithSingleIpv4Zone();
        ResolveZonesForObjectsRequest request = new()
        {
            Objects = [new ResolveZonesForObjectsRequest.LeafObjectRequest
            {
                Name = "Broken Leaf",
                Type = "host",
                IpHost = "banana"
            }]
        };

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.ResolveZonesForObjectsAsync(request));

        Assert.That(exception!.Message, Does.Contain("'Broken Leaf' passed validation but could not be converted"));
    }

    private static DesignatedZoneMatrixService CreateServiceWithSingleIpv4Zone()
    {
        DesignatedZoneMatrixServiceApiConn apiConnection = new(
            [new ConfigItem { Key = "designatedZoneMatrix", Value = "12", User = 0 }],
            [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
            [new NetworkZone
            {
                Id = 10,
                Name = "IPv4 Zone",
                IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("192.0.2.1"), IPAddress.Parse("192.0.2.1"))]
            }]);
        return CreateService(apiConnection, new SimulatedGlobalConfig { DesignatedZoneMatrixId = 12 });
    }

    [Test]
    public async Task ResolveZonesForObjectsAsync_ResolvesRangeLeaves()
    {
        DesignatedZoneMatrixServiceApiConn apiConnection = new(
            [new ConfigItem { Key = "designatedZoneMatrix", Value = "12", User = 0 }],
            [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
            [
                new NetworkZone
                {
                    Id = 10,
                    Name = "Network Zone",
                    Description = "Network zone",
                    IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("10.0.2.5"), IPAddress.Parse("10.0.2.5"))]
                },
                new NetworkZone
                {
                    Id = 20,
                    Name = "Range Zone",
                    Description = "Range zone",
                    IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("10.0.3.7"), IPAddress.Parse("10.0.3.7"))]
                }
            ]);
        DesignatedZoneMatrixService service = CreateService(apiConnection, new SimulatedGlobalConfig { DesignatedZoneMatrixId = 12 });

        List<ComplianceDesignatedZoneResponse> result = await service.ResolveZonesForObjectsAsync(new ResolveZonesForObjectsRequest
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Network Leaf",
                    Type = "network",
                    IpRange = ["10.0.2.1", "10.0.2.10"]
                },
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Range Leaf",
                    Type = "ip_range",
                    IpRange = ["10.0.3.1", "10.0.3.10"]
                }
            ]
        });

        Assert.That(result.Select(zone => zone.Name), Is.EqualTo(["Network Zone", "Range Zone"]));
    }

    [Test]
    public async Task ResolveZonesForObjectsAsync_UsesDefaultTypeNormalizationForUnknownLeafType()
    {
        DesignatedZoneMatrixServiceApiConn apiConnection = new(
            [new ConfigItem { Key = "designatedZoneMatrix", Value = "12", User = 0 }],
            [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
            [
                new NetworkZone
                {
                    Id = 10,
                    Name = "DMZ",
                    Description = "Demilitarized zone",
                    IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.1"))]
                }
            ]);
        DesignatedZoneMatrixService service = CreateService(apiConnection, new SimulatedGlobalConfig { DesignatedZoneMatrixId = 12 });

        List<ComplianceDesignatedZoneResponse> result = await service.ResolveZonesForObjectsAsync(new ResolveZonesForObjectsRequest
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Alias",
                    Type = "alias",
                    IpHost = "10.0.0.1"
                }
            ]
        });

        Assert.That(result.Select(zone => zone.Name), Is.EqualTo(["DMZ"]));
    }

    [TestCase(DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid)]
    [TestCase(DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable)]
    public async Task GetDesignatedZoneMatrixZonesAsync_ReturnsEmptyForUnavailableConfiguration(
        DesignatedZoneMatrixReadFailureCategory category)
    {
        DesignatedZoneMatrixService service = new(
            new StubZoneMatrixReader(DesignatedZoneMatrixReadResult.Failure(category)),
            new SimulatedGlobalConfig());

        ListPage<ComplianceDesignatedZoneResponse> result = await service.GetDesignatedZoneMatrixZonesAsync(
            GlobalConst.kDefaultMaxRestResponseObjectLimit, null);

        Assert.That(result.Items, Is.Empty);
    }

    [Test]
    public void GetDesignatedZoneMatrixZonesAsync_UsesGenericSafeQueryFailurePath()
    {
        DesignatedZoneMatrixReadFailureCategory failure = DesignatedZoneMatrixReadFailureCategory.QueryFailed;
        DesignatedZoneMatrixService service = new(
            new StubZoneMatrixReader(DesignatedZoneMatrixReadResult.Failure(failure)),
            new SimulatedGlobalConfig());

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetDesignatedZoneMatrixZonesAsync(GlobalConst.kDefaultMaxRestResponseObjectLimit, null));

        Assert.That(exception!.Message, Is.EqualTo(failure.GetDiagnostic()));
    }

    [Test]
    public void ResolveZonesForObjectsAsync_ThrowsForUnsupportedObjectNodeType()
    {
        DesignatedZoneMatrixService service = CreateService(new DesignatedZoneMatrixServiceApiConn([], [], []), new SimulatedGlobalConfig { DesignatedZoneMatrixId = 12 });

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ResolveZonesForObjectsAsync(new ResolveZonesForObjectsRequest
            {
                Objects =
                [
                    new UnsupportedObjectRequest
                    {
                        Name = "Unsupported"
                    }
                ]
            }));
    }

    private static DesignatedZoneMatrixService CreateService(DesignatedZoneMatrixServiceApiConn apiConnection, SimulatedGlobalConfig globalConfig)
    {
        return new DesignatedZoneMatrixService(new DesignatedZoneMatrixReader(apiConnection), globalConfig);
    }

    private sealed class StubZoneMatrixReader(DesignatedZoneMatrixReadResult result) : IDesignatedZoneMatrixReader
    {
        public Task<DesignatedZoneMatrixReadResult> ReadAsync(
            int criterionId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class DesignatedZoneMatrixServiceApiConn : SimulatedApiConnection
    {
        private readonly ConfigItem[] configItems;
        private readonly List<ComplianceCriterion> matrices;
        private readonly List<NetworkZone> zones;

        public int MatrixQueryCount { get; private set; }
        public int NetworkZoneQueryCount { get; private set; }

        public DesignatedZoneMatrixServiceApiConn(ConfigItem[] configItems, List<ComplianceCriterion> matrices, List<NetworkZone> zones)
        {
            this.configItems = configItems;
            this.matrices = matrices;
            this.zones = zones;
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(ConfigItem[]) && query == ConfigQueries.getConfigItemsByUser)
            {
                return Task.FromResult((QueryResponseType)(object)configItems);
            }

            if (typeof(QueryResponseType) == typeof(List<ComplianceCriterion>) && query == ComplianceQueries.getMatrixById)
            {
                MatrixQueryCount++;
                return Task.FromResult((QueryResponseType)(object)matrices);
            }

            if (typeof(QueryResponseType) == typeof(List<NetworkZone>) && query == NetworkZoneQueries.getNetworkZonesForMatrix)
            {
                NetworkZoneQueryCount++;
                return Task.FromResult((QueryResponseType)(object)zones);
            }

            throw new NotImplementedException();
        }

        public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            throw new NotImplementedException();
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

    private sealed class UnsupportedObjectRequest : ResolveZonesForObjectsRequest.ObjectRequest
    {
    }
}
