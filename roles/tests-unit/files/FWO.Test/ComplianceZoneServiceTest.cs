using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using NUnit.Framework;
using System.Net;

namespace FWO.Test;

[TestFixture]
internal class ComplianceZoneServiceTest
{
    [Test]
    public async Task ResolveZonesForObjectsAsync_ReturnsOrderedUniqueZonesForNestedGroups()
    {
        ComplianceZoneServiceApiConn apiConnection = new(
            [new ConfigItem { Key = "complianceDesignatedZoneMatrix", Value = "12", User = 0 }],
            [new ComplianceCriterion { Id = 12, Name = "Designated Matrix" }],
            [
                new ComplianceNetworkZone
                {
                    Id = 20,
                    Name = "Backend",
                    Description = "Backend zone",
                    IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.1"))]
                },
                new ComplianceNetworkZone
                {
                    Id = 10,
                    Name = "DMZ",
                    Description = "Demilitarized zone",
                    IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("10.0.1.1"), IPAddress.Parse("10.0.1.1"))]
                }
            ]);
        ComplianceZoneService service = new(apiConnection, new SimulatedGlobalConfig { ComplianceDesignatedZoneMatrixId = 12 });

        List<ComplianceDesignatedZoneResponse> result = await service.ResolveZonesForObjectsAsync(new GetZonesForDraftObjectsRequest
        {
            Objects =
            [
                new GetZonesForDraftObjectsRequest.DraftObjectRequest
                {
                    Name = "Root Group",
                    Type = "group",
                    Members =
                    [
                        new GetZonesForDraftObjectsRequest.DraftObjectRequest
                        {
                            Name = "Sub Group",
                            Type = "group",
                            Members =
                            [
                                new GetZonesForDraftObjectsRequest.DraftObjectRequest
                                {
                                    Name = "Backend Host",
                                    Type = "host",
                                    IpStart = "10.0.0.1",
                                    IpEnd = "10.0.0.1"
                                },
                                new GetZonesForDraftObjectsRequest.DraftObjectRequest
                                {
                                    Name = "Backend Host Duplicate",
                                    Type = "network",
                                    IpStart = "10.0.0.1",
                                    IpEnd = "10.0.0.1"
                                },
                                new GetZonesForDraftObjectsRequest.DraftObjectRequest
                                {
                                    Name = "DMZ Host",
                                    Type = "ip_range",
                                    IpStart = "10.0.1.1",
                                    IpEnd = "10.0.1.1"
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

    private sealed class ComplianceZoneServiceApiConn : SimulatedApiConnection
    {
        private readonly ConfigItem[] configItems;
        private readonly List<ComplianceCriterion> matrices;
        private readonly List<ComplianceNetworkZone> zones;

        public int MatrixQueryCount { get; private set; }
        public int NetworkZoneQueryCount { get; private set; }

        public ComplianceZoneServiceApiConn(ConfigItem[] configItems, List<ComplianceCriterion> matrices, List<ComplianceNetworkZone> zones)
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

            if (typeof(QueryResponseType) == typeof(List<ComplianceNetworkZone>) && query == ComplianceQueries.getNetworkZonesForMatrix)
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
}
