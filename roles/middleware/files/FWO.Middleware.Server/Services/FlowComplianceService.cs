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
    private const int InvalidPolicyId = 0;

    private readonly ApiConnection apiConnection;
    private readonly GlobalConfig globalConfig;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    public FlowComplianceService(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        this.apiConnection = apiConnection;
        this.globalConfig = globalConfig;
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
        List<int> policyIds = (request.Policies ?? []).Distinct().ToList();
        if (policyIds.Count == 0)
        {
            return [];
        }

        UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, false);

        Rule rule = BuildSyntheticRule(request);
        List<FlowComplianceStateResponse> results = [];
        List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames) ?? [];
        Dictionary<int, List<NetworkZone>> networkZonesByCriterion = [];
        Dictionary<int, CompliancePolicy> policiesById = await LoadPoliciesAsync(policyIds);

        foreach (int policyId in policyIds)
        {
            CompliancePolicy? policy = policiesById.GetValueOrDefault(policyId);
            ComplianceCheck complianceCheck = new(userConfig, apiConnection);

            if (policy is not { Id: > InvalidPolicyId } || policy.Criteria.Count == 0)
            {
                results.Add(ToResponse(policyId, policy, complianceCheck));
                continue;
            }

            await EnsureNetworkZonesLoadedAsync(policy, networkZonesByCriterion);
            await complianceCheck.AreRulesCompliant(policy, [rule], managements, networkZonesByCriterion);
            results.Add(ToResponse(policyId, policy, complianceCheck));
        }

        return results;
    }

    /// <summary>
    /// Loads all requested policies with one query instead of one query per policy.
    /// </summary>
    private async Task<Dictionary<int, CompliancePolicy>> LoadPoliciesAsync(List<int> policyIds)
    {
        List<CompliancePolicy> policies = await apiConnection.SendQueryAsync<List<CompliancePolicy>>(
            ComplianceQueries.getPoliciesByIds, new { ids = policyIds }) ?? [];
        return policies
            .Where(policy => policy.Id > InvalidPolicyId)
            .GroupBy(policy => policy.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private async Task EnsureNetworkZonesLoadedAsync(CompliancePolicy policy, Dictionary<int, List<NetworkZone>> networkZonesByCriterion)
    {
        foreach (int matrixId in policy.Criteria
                     .Where(wrapper => wrapper.Content.CriterionType == nameof(CriterionType.Matrix) && wrapper.Content.Id > 0)
                     .Select(wrapper => wrapper.Content.Id)
                     .Distinct())
        {
            if (networkZonesByCriterion.ContainsKey(matrixId))
            {
                continue;
            }

            networkZonesByCriterion[matrixId] = await apiConnection.SendQueryAsync<List<NetworkZone>>(
                NetworkZoneQueries.getNetworkZonesForMatrix,
                new { criterionId = matrixId }) ?? [];
        }
    }

    /// <summary>
    /// Builds the one rule that represents the requested flow: all sources, destinations and services together.
    /// </summary>
    private static Rule BuildSyntheticRule(GetFlowComplianceStateRequest request)
    {
        return new Rule
        {
            Id = 1,
            Uid = "flow-compliance-state",
            MgmtId = 0,
            Action = "accept",
            Froms = ToNetworkLocations(request.Source, FlowComplianceRequestValidationSchema.kSourcePath),
            Tos = ToNetworkLocations(request.Destination, FlowComplianceRequestValidationSchema.kDestinationPath),
            Services = ToServiceWrappers(request.Service)
        };
    }

    /// <summary>
    /// Converts the validated address blocks of one request list into rule locations named <c>{path}-1</c>, <c>{path}-2</c>, ...
    /// </summary>
    private static NetworkLocation[] ToNetworkLocations(List<AddressInputRequest?>? addresses, string path)
    {
        addresses ??= [];
        List<NetworkLocation> locations = [];
        for (int index = 0; index < addresses.Count; index++)
        {
            NetworkObject networkObject = ToNetworkObject(addresses[index], $"{path}[{index}]", $"{path}-{index + 1}");
            locations.Add(new NetworkLocation(new NetworkUser(), networkObject));
        }
        return [.. locations];
    }

    /// <summary>
    /// Converts the validated service entries into rule services named <c>service-1</c>, <c>service-2</c>, ...
    /// </summary>
    private static ServiceWrapper[] ToServiceWrappers(List<GetFlowComplianceStateRequest.ServiceRangeRequest?>? services)
    {
        services ??= [];
        List<ServiceWrapper> wrappers = [];
        for (int index = 0; index < services.Count; index++)
        {
            string path = $"{FlowComplianceRequestValidationSchema.kServicePath}[{index}]";
            GetFlowComplianceStateRequest.ServiceRangeRequest service = services[index]
                ?? throw new InvalidOperationException($"'{path}' passed validation but is null.");
            wrappers.Add(new ServiceWrapper { Content = ToNetworkService(service, $"service-{index + 1}") });
        }
        return [.. wrappers];
    }

    /// <summary>
    /// Converts one validated address block into the network object of the synthetic rule.
    /// </summary>
    /// <param name="address">The address block; the validator has already rejected null entries and invalid addresses.</param>
    /// <param name="path">Request path of the block, for example <c>source[0]</c>, used in the exception message.</param>
    /// <param name="name">Name of the network object, for example <c>source-1</c>.</param>
    /// <exception cref="InvalidOperationException">The block passed validation but is null or cannot be normalized.</exception>
    private static NetworkObject ToNetworkObject(AddressInputRequest? address, string path, string name)
    {
        if (address == null)
        {
            throw new InvalidOperationException($"'{path}' passed validation but is null.");
        }
        if (!AddressInputNormalizer.TryValidateAndNormalize(address, $"'{path}'", out NormalizedAddressBounds bounds, out string? error))
        {
            throw new InvalidOperationException($"'{path}' passed validation but could not be converted: {error}");
        }

        return new NetworkObject
        {
            Name = name,
            IP = bounds.IpStart,
            IpEnd = bounds.IpEnd,
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

    private static FlowComplianceStateResponse ToResponse(int policyId, CompliancePolicy? policy, ComplianceCheck complianceCheck)
    {
        bool policyResolved = policy is { Id: > 0 };
        return new FlowComplianceStateResponse
        {
            Policy = new FlowComplianceStateResponse.CompliancePolicyResponse
            {
                Id = policyResolved ? policy!.Id : policyId,
                Name = policyResolved ? policy!.Name : string.Empty
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
