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
using System.Text.Json;
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

        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());

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

        FlowComplianceController controller = new(new FlowComplianceService(apiConnection, new SimulatedGlobalConfig()));

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

        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(BuildComplianceRequest());

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Policy.Id, Is.EqualTo(7));
            Assert.That(result[0].Policy.Name, Is.EqualTo("Matrix and Service Policy"));
            Assert.That(result[0].Violations, Has.Count.EqualTo(2));
            Assert.That(result[0].Violations.Select(v => v.Type), Is.EquivalentTo(new[] { "Matrix", "ForbiddenService" }));
            Assert.That(apiConnection.CountQueries(ConfigQueries.getLanguages), Is.EqualTo(0));
            Assert.That(apiConnection.CountQueries(ConfigQueries.getTextsPerLanguage), Is.EqualTo(0));
            Assert.That(apiConnection.SentQueries, Does.Contain(ComplianceQueries.getPoliciesByIds));
            Assert.That(apiConnection.SentQueries, Does.Contain(NetworkZoneQueries.getNetworkZonesForMatrix));
            Assert.That(apiConnection.SentQueries, Does.Contain(DeviceQueries.getManagementNames));
        });
    }

    [Test]
    public async Task GetFlowComplianceStateAsync_PreservesPolicyNameWhenFlowIsCompliant()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);

        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(BuildCompliantMatrixOnlyRequest());

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Policy.Id, Is.EqualTo(8));
            Assert.That(result[0].Policy.Name, Is.EqualTo("Matrix Only Policy"));
            Assert.That(result[0].Violations, Is.Empty);
        });
    }

    [Test]
    public async Task GetFlowComplianceStateAsync_DoesNotReuseViolationsForUnknownPolicies()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);

        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());

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

    /// <summary>
    /// Verifies a request without policies returns no result and sends no query.
    /// </summary>
    [Test]
    public async Task GetFlowComplianceStateAsync_NullOrEmptyPolicies_ReturnsEmptyWithoutQueries()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());
        GetFlowComplianceStateRequest nullPolicies = BuildComplianceRequest();
        nullPolicies.Policies = null;
        GetFlowComplianceStateRequest emptyPolicies = BuildComplianceRequest();
        emptyPolicies.Policies = [];

        List<FlowComplianceStateResponse> nullResult = await service.GetFlowComplianceStateAsync(nullPolicies);
        List<FlowComplianceStateResponse> emptyResult = await service.GetFlowComplianceStateAsync(emptyPolicies);

        Assert.Multiple(() =>
        {
            Assert.That(nullResult, Is.Empty);
            Assert.That(emptyResult, Is.Empty);
            Assert.That(apiConnection.SentQueries, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies a duplicated policy id is checked and returned once.
    /// </summary>
    [Test]
    public async Task GetFlowComplianceStateAsync_DuplicatePolicyIds_AreCheckedOnce()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);
        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());
        GetFlowComplianceStateRequest request = BuildComplianceRequest();
        request.Policies = [8, 8];

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(request);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Policy.Id, Is.EqualTo(8));
    }

    /// <summary>
    /// Verifies null address and service lists are treated as empty instead of failing.
    /// </summary>
    [Test]
    public async Task GetFlowComplianceStateAsync_NullFlowLists_AreTreatedAsEmpty()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);
        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());
        GetFlowComplianceStateRequest request = new() { Source = null, Destination = null, Service = null, Policies = [8] };

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Policy.Id, Is.EqualTo(8));
        });
    }

    /// <summary>
    /// Verifies the service normalizes a network itself: host bits are cleared and the whole network is checked.
    /// </summary>
    [Test]
    public async Task GetFlowComplianceStateAsync_SourceNetwork_IsCheckedAsItsWholeRange()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);
        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());
        GetFlowComplianceStateRequest request = BuildMinimumCidrLengthRequest("10.0.0.1", "10.0.0.1");
        request.Source = [new AddressInputRequest { IpNetwork = "10.1.2.3/8" }];

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(request);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Violations, Has.Count.EqualTo(1));
            Assert.That(result[0].Violations[0].Type, Is.EqualTo(nameof(CriterionType.MinimumCIDRLength)));
        });
    }

    /// <summary>
    /// Verifies a single host passes a minimum CIDR length criterion.
    /// </summary>
    [Test]
    public async Task GetFlowComplianceStateAsync_SourceHost_HasNoCidrLengthViolation()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);
        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());
        GetFlowComplianceStateRequest request = BuildMinimumCidrLengthRequest("10.0.0.1", "10.0.0.1");
        request.Source = [new AddressInputRequest { IpHost = "10.0.0.1" }];

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(request);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Violations, Is.Empty);
    }

    /// <summary>
    /// Verifies a null address entry that bypassed the validator surfaces as a programming error naming its path.
    /// </summary>
    [Test]
    public void GetFlowComplianceStateAsync_NullAddressEntry_ThrowsInvalidOperation()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);
        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());
        GetFlowComplianceStateRequest request = BuildComplianceRequest();
        request.Source = [null];

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetFlowComplianceStateAsync(request));

        Assert.That(exception!.Message, Does.Contain("'source[0]'"));
    }

    /// <summary>
    /// Verifies an invalid address that bypassed the validator surfaces as a programming error naming its path.
    /// </summary>
    [Test]
    public void GetFlowComplianceStateAsync_InvalidAddress_ThrowsInvalidOperation()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);
        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());
        GetFlowComplianceStateRequest request = BuildComplianceRequest();
        request.Destination = [new AddressInputRequest { IpHost = "banana" }];

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetFlowComplianceStateAsync(request));

        Assert.That(exception!.Message, Does.Contain("'destination[0]'"));
    }

    /// <summary>
    /// Verifies a null service entry that bypassed the validator surfaces as a programming error naming its path.
    /// </summary>
    [Test]
    public void GetFlowComplianceStateAsync_NullServiceEntry_ThrowsInvalidOperation()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);
        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());
        GetFlowComplianceStateRequest request = BuildComplianceRequest();
        request.Service = [null];

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetFlowComplianceStateAsync(request));

        Assert.That(exception!.Message, Does.Contain("'service[0]'"));
    }

    /// <summary>
    /// Verifies an invalid request is rejected with the aggregated error contract before the service runs.
    /// </summary>
    [Test]
    public async Task GetFlowComplianceState_InvalidRequest_ReturnsAggregatedBadRequest()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        FlowComplianceController controller = new(new FlowComplianceService(apiConnection, new SimulatedGlobalConfig()));
        GetFlowComplianceStateRequest request = BuildComplianceRequest();
        request.Policies = [0];

        ActionResult<List<FlowComplianceStateResponse>> result = await controller.GetFlowComplianceState(request);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        RequestValidationErrorResponse? errors = ((BadRequestObjectResult)result.Result!).Value as RequestValidationErrorResponse;
        Assert.Multiple(() =>
        {
            Assert.That(errors, Is.Not.Null);
            Assert.That(errors!.Errors.Single().Path, Is.EqualTo("policies[0]"));
            Assert.That(apiConnection.SentQueries, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies a valid request reaches the service and returns its result.
    /// </summary>
    [Test]
    public async Task GetFlowComplianceState_ValidRequest_ReturnsOk()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);
        FlowComplianceController controller = new(new FlowComplianceService(apiConnection, new SimulatedGlobalConfig()));

        ActionResult<List<FlowComplianceStateResponse>> result = await controller.GetFlowComplianceState(BuildCompliantMatrixOnlyRequest());

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        List<FlowComplianceStateResponse>? response = ((OkObjectResult)result.Result!).Value as List<FlowComplianceStateResponse>;
        Assert.That(response, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Verifies an unknown key in the policy id request is rejected with the aggregated error contract.
    /// </summary>
    [Test]
    public async Task GetPolicyIds_UnknownKey_ReturnsAggregatedBadRequest()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        FlowComplianceController controller = new(new FlowComplianceService(apiConnection, new SimulatedGlobalConfig()));
        GetPolicyIdsRequest request = JsonSerializer.Deserialize<GetPolicyIdsRequest>("""{"limit":10}""")!;

        ActionResult<GetPolicyIdsResponse> result = await controller.GetPolicyIds(request);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        RequestValidationErrorResponse? errors = ((BadRequestObjectResult)result.Result!).Value as RequestValidationErrorResponse;
        Assert.Multiple(() =>
        {
            Assert.That(errors, Is.Not.Null);
            Assert.That(errors!.Errors.Single().Path, Is.EqualTo("limit"));
            Assert.That(apiConnection.SentQueries, Is.Empty);
        });
    }

    /// <summary>
    /// SEC-25: the number of policy queries must not grow with the number of requested policies.
    /// </summary>
    [Test]
    public async Task GetFlowComplianceStateAsync_LoadsAllPoliciesWithOneQuery()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);
        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());
        GetFlowComplianceStateRequest request = BuildComplianceRequest();
        request.Policies = Enumerable.Range(1, FlowComplianceRequestValidationSchema.kMaxPolicies).ToList();

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(FlowComplianceRequestValidationSchema.kMaxPolicies));
            Assert.That(apiConnection.CountQueries(ComplianceQueries.getPoliciesByIds), Is.EqualTo(1));
            Assert.That(apiConnection.CountQueries(ComplianceQueries.getPolicyById), Is.Zero);
            Assert.That(result.Single(response => response.Policy.Id == 7).Policy.Name, Is.EqualTo("Matrix and Service Policy"));
            Assert.That(result.Single(response => response.Policy.Id == 1).Policy.Name, Is.Empty);
        });
    }

    [Test]
    public async Task GetFlowComplianceStateAsync_ReusesSharedComplianceDataAcrossPolicies()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);

        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());

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
            Assert.That(apiConnection.CountQueries(NetworkZoneQueries.getNetworkZonesForMatrix), Is.EqualTo(1));
            Assert.That(apiConnection.CountQueries(ComplianceQueries.getPoliciesByIds), Is.EqualTo(1));
            Assert.That(apiConnection.CountQueries(ComplianceQueries.getPolicyById), Is.Zero);
        });
    }

    [Test]
    public async Task GetFlowComplianceStateAsync_Ipv6FlowUnderIpv4OnlyCriterion_ReportsNotAssessable()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);

        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(
            BuildMinimumCidrLengthRequest("2001:db8::", "2001:db8::"));

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Policy.Id, Is.EqualTo(9));
            Assert.That(result[0].Violations, Has.Count.EqualTo(1));
            Assert.That(result[0].Violations[0].Type, Is.EqualTo(nameof(ComplianceViolationType.NotAssessable)));
        });
    }

    [Test]
    public async Task GetFlowComplianceStateAsync_MultipleUnassignableObjectsReportEachObject()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);
        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());
        GetFlowComplianceStateRequest request = BuildCompliantMatrixOnlyRequest();
        request.Source =
        [
            new AddressInputRequest { IpRange = ["2001:db8::1", "2001:db8::1" ]},
            new AddressInputRequest { IpRange = ["2001:db8:1::1", "2001:db8:1::1" ]}
        ];

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Violations, Has.Count.EqualTo(2));
            Assert.That(result[0].Violations.All(violation => violation.Type == nameof(ComplianceViolationType.NotAssessable)), Is.True);
        });
    }

    [Test]
    public async Task GetFlowComplianceStateAsync_BroadIpv4FlowUnderIpv4OnlyCriterion_ReportsCidrLengthViolation()
    {
        FlowComplianceServiceApiConn apiConnection = new();
        ConfigureComplianceFixture(apiConnection);

        FlowComplianceService service = new(apiConnection, new SimulatedGlobalConfig());

        List<FlowComplianceStateResponse> result = await service.GetFlowComplianceStateAsync(
            BuildMinimumCidrLengthRequest("10.0.0.0", "10.255.255.255"));

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Violations, Has.Count.EqualTo(1));
            Assert.That(result[0].Violations[0].Type, Is.EqualTo(nameof(CriterionType.MinimumCIDRLength)));
        });
    }

    private static GetFlowComplianceStateRequest BuildMinimumCidrLengthRequest(string sourceIpStart, string sourceIpEnd)
    {
        return new GetFlowComplianceStateRequest
        {
            Source =
            [
                new AddressInputRequest
                {
                    IpRange = [sourceIpStart, sourceIpEnd]
                }
            ],
            Destination =
            [
                new AddressInputRequest
                {
                    IpRange = ["193.0.0.1", "193.0.0.1"]
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
            Policies = [9]
        };
    }

    private static CompliancePolicy BuildMinimumCidrLengthPolicy()
    {
        return new CompliancePolicy
        {
            Id = 9,
            Name = "Minimum CIDR Length Policy",
            Criteria =
            [
                new ComplianceCriterionWrapper
                {
                    Content = new ComplianceCriterion
                    {
                        Id = 103,
                        Name = "Minimum CIDR Length",
                        CriterionType = nameof(CriterionType.MinimumCIDRLength),
                        Content = "24"
                    }
                }
            ]
        };
    }

    private static void ConfigureComplianceFixture(FlowComplianceServiceApiConn apiConnection)
    {
        apiConnection.Languages = [new Language { Name = "English", CultureInfo = "en-US" }];
        apiConnection.TextsByLanguage["English"] = BuildEnglishTexts();
        apiConnection.PoliciesById[7] = BuildPolicy();
        apiConnection.PoliciesById[8] = BuildMatrixOnlyPolicy();
        apiConnection.PoliciesById[9] = BuildMinimumCidrLengthPolicy();
        apiConnection.Managements = [new Management { Id = 1, Uid = "mgmt-1" }];
        apiConnection.NetworkZones = BuildNetworkZones();
    }

    private static GetFlowComplianceStateRequest BuildComplianceRequest()
    {
        return new GetFlowComplianceStateRequest
        {
            Source =
            [
                new AddressInputRequest
                {
                    IpRange = ["128.0.0.1", "128.0.0.1"]
                }
            ],
            Destination =
            [
                new AddressInputRequest
                {
                    IpRange = ["193.0.0.1", "193.0.0.1"]
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

    private static GetFlowComplianceStateRequest BuildCompliantMatrixOnlyRequest()
    {
        return new GetFlowComplianceStateRequest
        {
            Source =
            [
                new AddressInputRequest
                {
                    IpRange = ["128.0.0.1", "128.0.0.1"]
                }
            ],
            Destination =
            [
                new AddressInputRequest
                {
                    IpRange = ["128.0.0.1", "128.0.0.1"]
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
            Policies = [8]
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

    private static List<NetworkZone> BuildNetworkZones()
    {
        List<NetworkZone> networkZones =
        [
            new NetworkZone
            {
                Id = 1,
                CriterionId = 101,
                Name = "Zone A",
                IPRanges = [new NetTools.IPAddressRange(IPAddress.Parse("128.0.0.1"), IPAddress.Parse("128.0.0.1"))],
                AllowedCommunicationDestinations = []
            },
            new NetworkZone
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
        public List<NetworkZone> NetworkZones { get; set; } = [];

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

            if (typeof(QueryResponseType) == typeof(List<CompliancePolicy>) && query == ComplianceQueries.getPoliciesByIds)
            {
                List<int> policyIds = variables?.GetType().GetProperty("ids")?.GetValue(variables) as List<int> ?? [];
                return Task.FromResult((QueryResponseType)(object)policyIds
                    .Where(PoliciesById.ContainsKey)
                    .Select(policyId => PoliciesById[policyId])
                    .ToList());
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

            if (typeof(QueryResponseType) == typeof(List<NetworkZone>) && query == NetworkZoneQueries.getNetworkZonesForMatrix)
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
