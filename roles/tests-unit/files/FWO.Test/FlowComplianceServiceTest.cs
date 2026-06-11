using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class FlowComplianceServiceTest
{
    [Test]
    public async Task GetPolicyIdsAsync_ReturnsActivePolicies()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        apiConnection.Policies =
        [
            new CompliancePolicy { Id = 2, Name = "Beta" },
            new CompliancePolicy { Id = 1, Name = "Alpha" }
        ];

        FlowComplianceService service = new(apiConnection);

        List<PolicyIdResponse> result = await service.GetPolicyIdsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Id, Is.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("Alpha"));
            Assert.That(result[1].Id, Is.EqualTo(2));
            Assert.That(result[1].Name, Is.EqualTo("Beta"));
        });
    }

    [Test]
    public async Task GetPolicyIds_ReturnsWrappedPoliciesResponse()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        apiConnection.Policies =
        [
            new CompliancePolicy { Id = 2, Name = "Beta" },
            new CompliancePolicy { Id = 1, Name = "Alpha" }
        ];

        FlowComplianceController controller = new(new FlowComplianceService(apiConnection));

        ActionResult<GetPolicyIdsResponse> result = await controller.GetPolicyIds(new GetPolicyIdsRequest());

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());

        OkObjectResult okResult = (OkObjectResult)result.Result!;
        Assert.That(okResult.Value, Is.TypeOf<GetPolicyIdsResponse>());

        GetPolicyIdsResponse response = (GetPolicyIdsResponse)okResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(response.Policies, Has.Count.EqualTo(2));
            Assert.That(response.Policies[0].Id, Is.EqualTo(1));
            Assert.That(response.Policies[0].Name, Is.EqualTo("Alpha"));
            Assert.That(response.Policies[1].Id, Is.EqualTo(2));
            Assert.That(response.Policies[1].Name, Is.EqualTo("Beta"));
        });
    }

    [Test]
    public async Task GetFlowComplianceStateAsync_ReturnsViolationsPerPolicy()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);

        FlowComplianceService service = new(apiConnection);

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(BuildComplianceRequest());

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Policy.Id, Is.EqualTo(7));
            Assert.That(result[0].Policy.Name, Is.EqualTo("Matrix and Service Policy"));
            Assert.That(result[0].Violations, Has.Count.EqualTo(2));
            Assert.That(result[0].Violations.Select(v => v.Type), Is.EquivalentTo(new[] { "Matrix", "ForbiddenService" }));
            Assert.That(apiConnection.SentQueries, Does.Contain(ConfigQueries.getLanguages));
            Assert.That(apiConnection.SentQueries, Does.Contain(ConfigQueries.getTextsPerLanguage));
            Assert.That(apiConnection.SentQueries, Does.Contain(ComplianceQueries.getPolicyById));
            Assert.That(apiConnection.SentQueries, Does.Contain(ComplianceQueries.getNetworkZonesForMatrix));
            Assert.That(apiConnection.SentQueries, Does.Contain(DeviceQueries.getManagementNames));
        });
    }

    [Test]
    public async Task GetFlowComplianceStateAsync_DoesNotReuseViolationsForUnknownPolicies()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);

        FlowComplianceService service = new(apiConnection);

        GetFlowComplianceStateRequest request = BuildComplianceRequest();
        request.Policies = [7, 999999];

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(request);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Policy.Id, Is.EqualTo(7));
            Assert.That(result[0].Violations, Has.Count.EqualTo(2));
            Assert.That(result[1].Policy.Id, Is.EqualTo(999999));
            Assert.That(result[1].Policy.Name, Is.EqualTo(string.Empty));
            Assert.That(result[1].Violations, Is.Empty);
        });
    }

    [Test]
    public async Task GetFlowComplianceStateAsync_ReusesSharedComplianceDataAcrossPolicies()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);

        FlowComplianceService service = new(apiConnection);

        GetFlowComplianceStateRequest request = BuildComplianceRequest();
        request.Policies = [7, 8];

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(request);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Policy.Id, Is.EqualTo(7));
            Assert.That(result[0].Violations.Select(v => v.Type), Is.EquivalentTo(new[] { "Matrix", "ForbiddenService" }));
            Assert.That(result[1].Policy.Id, Is.EqualTo(8));
            Assert.That(result[1].Violations.Select(v => v.Type), Is.EquivalentTo(new[] { "Matrix" }));
            Assert.That(apiConnection.CountQueries(DeviceQueries.getManagementNames), Is.EqualTo(1));
            Assert.That(apiConnection.CountQueries(ComplianceQueries.getNetworkZonesForMatrix), Is.EqualTo(1));
            Assert.That(apiConnection.CountQueries(ComplianceQueries.getPolicyById), Is.EqualTo(2));
        });
    }

    private static void ConfigureComplianceFixture(FlowComplianceServiceApiConn apiConnection)
    {
        apiConnection.Languages = [new Language { Name = "English", CultureInfo = "en-US" }];
        apiConnection.TextsByLanguage["English"] = BuildEnglishTexts();
        apiConnection.PoliciesById[7] = BuildPolicy();
        apiConnection.PoliciesById[8] = BuildMatrixOnlyPolicy();
        apiConnection.Managements = [new Management { Id = 1, Uid = "mgmt-1" }];
        apiConnection.NetworkZones = BuildNetworkZones();
    }

    private static GetFlowComplianceStateRequest BuildComplianceRequest()
    {
        return new GetFlowComplianceStateRequest
        {
            Source =
            [
                new GetFlowComplianceStateRequest.IpRangeRequest
                {
                    IpStart = "128.0.0.1",
                    IpEnd = "128.0.0.1"
                }
            ],
            Destination =
            [
                new GetFlowComplianceStateRequest.IpRangeRequest
                {
                    IpStart = "193.0.0.1",
                    IpEnd = "193.0.0.1"
                }
            ],
            Service =
            [
                new GetFlowComplianceStateRequest.ServiceRangeRequest
                {
                    PortStart = 443,
                    PortEnd = 443,
                    Protocol = "TCP"
                }
            ],
            Policies = [7]
        };
    }

    private static CompliancePolicy BuildPolicy()
    {
        return new CompliancePolicy
        {
            Id = 7,
            Name = "Matrix and Service Policy",
            Criteria =
            [
                new ComplianceCriterionWrapper
                {
                    Content = new ComplianceCriterion
                    {
                        Id = 101,
                        Name = "Matrix A",
                        CriterionType = nameof(CriterionType.Matrix)
                    }
                },
                new ComplianceCriterionWrapper
                {
                    Content = new ComplianceCriterion
                    {
                        Id = 102,
                        Name = "Forbidden Service",
                        CriterionType = nameof(CriterionType.ForbiddenService),
                        Content = "443/TCP"
                    }
                }
            ]
        };
    }

    private static CompliancePolicy BuildMatrixOnlyPolicy()
    {
        return new CompliancePolicy
        {
            Id = 8,
            Name = "Matrix Only Policy",
            Criteria =
            [
                new ComplianceCriterionWrapper
                {
                    Content = new ComplianceCriterion
                    {
                        Id = 101,
                        Name = "Matrix A",
                        CriterionType = nameof(CriterionType.Matrix)
                    }
                }
            ]
        };
    }

    private static List<ComplianceNetworkZone> BuildNetworkZones()
    {
        List<ComplianceNetworkZone> networkZones =
        [
            new ComplianceNetworkZone
            {
                Id = 1,
                CriterionId = 101,
                Name = "Zone A",
                IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("128.0.0.1"), IPAddress.Parse("128.0.0.1"))],
                AllowedCommunicationDestinations = []
            },
            new ComplianceNetworkZone
            {
                Id = 2,
                CriterionId = 101,
                Name = "Zone B",
                IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("193.0.0.1"), IPAddress.Parse("193.0.0.1"))],
                AllowedCommunicationDestinations = []
            }
        ];
        networkZones[0].AllowedCommunicationDestinations = [networkZones[0]];
        networkZones[1].AllowedCommunicationDestinations = [networkZones[1]];
        return networkZones;
    }

    private static List<UiText> BuildEnglishTexts()
    {
        return
        [
            new UiText { Id = "H5839", Txt = "Matrix violation", Language = "English" },
            new UiText { Id = "H5840", Txt = "Restricted Service", Language = "English" },
            new UiText { Id = "H5841", Txt = "Assessability issue", Language = "English" },
            new UiText { Id = "assess_broadcast", Txt = "Network objects in source or destination with 255.255.255.255/32", Language = "English" },
            new UiText { Id = "assess_host_address", Txt = "Network objects in source or destination with 0.0.0.0/32", Language = "English" },
            new UiText { Id = "assess_all_ips", Txt = "Network objects in source or destination with 0.0.0.0/0 or ::/0", Language = "English" },
            new UiText { Id = "assess_ip_null", Txt = "Network objects in source or destination without IP", Language = "English" },
            new UiText { Id = "minimum_cidr_length_violation", Txt = "Minimum CIDR length violation", Language = "English" },
            new UiText { Id = "zone_object_source_violation", Txt = "Zone object source violation", Language = "English" },
            new UiText { Id = "zone_object_destination_violation", Txt = "Zone object destination violation", Language = "English" },
            new UiText { Id = "bidirectional_duplicate_violation", Txt = "Bidirectional duplicate violation", Language = "English" },
            new UiText { Id = "criterion_ipv6_not_supported", Txt = "IPv6 not supported", Language = "English" }
        ];
    }

    private sealed class FlowComplianceServiceApiConn : SimulatedApiConnection
    {
        public List<string> SentQueries { get; } = [];
        public List<Language> Languages { get; set; } = [];
        public Dictionary<string, List<UiText>> TextsByLanguage { get; set; } = new();
        public List<CompliancePolicy> Policies { get; set; } = [];
        public Dictionary<int, CompliancePolicy> PoliciesById { get; } = new();
        public List<Management> Managements { get; set; } = [];
        public List<ComplianceNetworkZone> NetworkZones { get; set; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            SentQueries.Add(query);

            if (typeof(QueryResponseType) == typeof(ConfigItem[]) && query == ConfigQueries.getConfigItemsByUser)
            {
                return Task.FromResult((QueryResponseType)(object)Array.Empty<ConfigItem>());
            }

            if (typeof(QueryResponseType) == typeof(Language[]) && query == ConfigQueries.getLanguages)
            {
                return Task.FromResult((QueryResponseType)(object)Languages.ToArray());
            }

            if (typeof(QueryResponseType) == typeof(List<UiText>) && query == ConfigQueries.getTextsPerLanguage)
            {
                string language = GetVariableValue(variables, "language") ?? "en";
                return Task.FromResult((QueryResponseType)(object)(TextsByLanguage.TryGetValue(language, out List<UiText>? texts) ? texts : []));
            }

            if (typeof(QueryResponseType) == typeof(List<UiText>) && query == ConfigQueries.getCustomTextsPerLanguage)
            {
                return Task.FromResult((QueryResponseType)(object)new List<UiText>());
            }

            if (typeof(QueryResponseType) == typeof(List<CompliancePolicy>) && query == ComplianceQueries.getPolicies)
            {
                return Task.FromResult((QueryResponseType)(object)Policies);
            }

            if (typeof(QueryResponseType) == typeof(CompliancePolicy) && query == ComplianceQueries.getPolicyById)
            {
                int policyId = int.TryParse(GetVariableValue(variables, "id"), out int id) ? id : 0;
                return Task.FromResult((QueryResponseType)(object)(PoliciesById.TryGetValue(policyId, out CompliancePolicy? policy) ? policy : new CompliancePolicy()));
            }

            if (typeof(QueryResponseType) == typeof(List<Management>) && query == DeviceQueries.getManagementNames)
            {
                return Task.FromResult((QueryResponseType)(object)Managements);
            }

            if (typeof(QueryResponseType) == typeof(List<ComplianceNetworkZone>) && query == ComplianceQueries.getNetworkZonesForMatrix)
            {
                return Task.FromResult((QueryResponseType)(object)NetworkZones);
            }

            throw new NotImplementedException($"Unsupported response type {typeof(QueryResponseType).Name} for query {query}");
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

        public int CountQueries(string query)
        {
            return SentQueries.Count(sentQuery => sentQuery == query);
        }

        private static string? GetVariableValue(object? variables, string propertyName)
        {
            if (variables == null)
            {
                return null;
            }

            return variables.GetType().GetProperty(propertyName)?.GetValue(variables)?.ToString();
        }
    }
}
