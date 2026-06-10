using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Represents the FlowComplianceService type.
/// </summary>
public sealed class FlowComplianceService
{
    private readonly ApiConnection apiConnection;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    public FlowComplianceService(ApiConnection apiConnection)
    {
        this.apiConnection = apiConnection;
    }

    /// <summary>
    /// Performs the GetPolicyIdsAsync operation.
    /// </summary>
    public async Task<List<PolicyIdResponse>> GetPolicyIdsAsync()
    {
        List<CompliancePolicy> policies = await apiConnection.SendQueryAsync<List<CompliancePolicy>>(ComplianceQueries.getPolicies) ?? [];
        return policies
            .OrderBy(policy => policy.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(policy => policy.Id)
            .Select(policy => new PolicyIdResponse
            {
                Id = policy.Id,
                Name = policy.Name
            })
            .ToList();
    }

    /// <summary>
    /// Performs the GetFlowComplianceStateAsync operation.
    /// </summary>
    public async Task<List<FlowComplianceStateResponse>> GetFlowComplianceStateAsync(GetFlowComplianceStateRequest request)
    {
        if (request.Policies.Count == 0)
        {
            return [];
        }

        GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection);
        UserConfig userConfig = new(globalConfig, false);

        Rule rule = BuildSyntheticRule(request);
        List<FlowComplianceStateResponse> results = [];
        foreach (int policyId in request.Policies.Distinct())
        {
            ComplianceCheck complianceCheck = new(userConfig, apiConnection);
            await complianceCheck.AreRulesCompliant([policyId], [rule]);
            results.Add(ToResponse(policyId, complianceCheck));
        }

        return results;
    }

    private static Rule BuildSyntheticRule(GetFlowComplianceStateRequest request)
    {
        Rule rule = new()
        {
            Id = 1,
            Uid = "flow-compliance-state",
            MgmtId = 0,
            Action = "accept",
            Froms = request.Source.Select((source, index) => new NetworkLocation(new NetworkUser(), ToNetworkObject(source, $"source-{index + 1}"))).ToArray(),
            Tos = request.Destination.Select((destination, index) => new NetworkLocation(new NetworkUser(), ToNetworkObject(destination, $"destination-{index + 1}"))).ToArray(),
            Services = request.Service.Select((service, index) => new ServiceWrapper { Content = ToNetworkService(service, $"service-{index + 1}") }).ToArray()
        };

        return rule;
    }

    private static NetworkObject ToNetworkObject(GetFlowComplianceStateRequest.IpRangeRequest request, string namePrefix)
    {
        return new NetworkObject
        {
            Name = namePrefix,
            IP = request.IpStart,
            IpEnd = request.IpEnd,
            Type = new NetworkObjectType { Name = ObjectType.IPRange }
        };
    }

    private static NetworkService ToNetworkService(GetFlowComplianceStateRequest.ServiceRangeRequest request, string namePrefix)
    {
        bool protocolIsNumeric = int.TryParse(request.Protocol, out int protocolId);

        return new NetworkService
        {
            Name = namePrefix,
            DestinationPort = request.PortStart,
            DestinationPortEnd = request.PortEnd,
            ProtoId = protocolIsNumeric ? protocolId : null,
            Protocol = new NetworkProtocol
            {
                Id = protocolIsNumeric ? protocolId : 0,
                Name = request.Protocol
            },
            Type = new NetworkServiceType { Name = ServiceType.SimpleService }
        };
    }

    private static FlowComplianceStateResponse ToResponse(int policyId, ComplianceCheck complianceCheck)
    {
        bool policyResolved = complianceCheck.Policy is { Id: > 0 };
        return new FlowComplianceStateResponse
        {
            Policy = new FlowComplianceStateResponse.CompliancePolicyResponse
            {
                Id = policyResolved ? complianceCheck.Policy!.Id : policyId,
                Name = policyResolved ? complianceCheck.Policy!.Name : string.Empty
            },
            Violations = complianceCheck.CurrentViolationsInCheck
                .Select(violation => new FlowComplianceStateResponse.ComplianceViolationResponse
                {
                    Id = violation.Id,
                    Type = MapViolationType(violation, complianceCheck.Policy)
                })
                .ToList()
        };
    }

    private static string MapViolationType(ComplianceViolation violation, CompliancePolicy? policy)
    {
        if (violation.Type != ComplianceViolationType.None)
        {
            return violation.Type.ToString();
        }

        string? criterionType = policy?.Criteria
            .FirstOrDefault(wrapper => wrapper.Content.Id == violation.CriterionId)
            ?.Content.CriterionType
            ?? violation.Criterion?.CriterionType;

        return criterionType switch
        {
            nameof(CriterionType.Matrix) => "Matrix",
            nameof(CriterionType.Assessability) => "Assessability",
            nameof(CriterionType.ForbiddenService) => "ForbiddenService",
            nameof(CriterionType.MinimumCIDRLength) => nameof(CriterionType.MinimumCIDRLength),
            nameof(CriterionType.ForbidZonesAsSource) => nameof(CriterionType.ForbidZonesAsSource),
            nameof(CriterionType.ForbidZonesAsDestination) => nameof(CriterionType.ForbidZonesAsDestination),
            nameof(CriterionType.ForbidBidirectionalDuplicate) => nameof(CriterionType.ForbidBidirectionalDuplicate),
            _ => string.Empty
        };
    }
}
