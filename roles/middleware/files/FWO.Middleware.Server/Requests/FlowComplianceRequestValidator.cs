using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the FlowComplianceRequestValidator type.
/// </summary>
public static class FlowComplianceRequestValidator
{
    private const int MinimumPort = 0;
    private const int MaximumPort = 65535;
    private const string GetPolicyIdsEndpointName = "getPolicyIds";
    private const string GetFlowComplianceStateEndpointName = "getFlowComplianceState";

    private static readonly RequestRootValidationSchema PolicyIdsRootSchema = new(
        GetPolicyIdsEndpointName,
        []);

    private static readonly RequestRootValidationSchema FlowComplianceRootSchema = new(
        GetFlowComplianceStateEndpointName,
        [
            new RequestKeyDefinition("source", "Source IP ranges to evaluate."),
            new RequestKeyDefinition("destination", "Destination IP ranges to evaluate."),
            new RequestKeyDefinition("service", "Service ports and protocols to evaluate."),
            new RequestKeyDefinition("policies", "Policy ids to evaluate.")
        ]);

    private static readonly RequestKeyDefinition[] IpRangeKeys =
    [
        new("ipStart", "Start IP address of the range."),
        new("ipEnd", "End IP address of the range.")
    ];

    private static readonly RequestKeyDefinition[] ServiceRangeKeys =
    [
        new("portStart", "Start port of the service range."),
        new("portEnd", "End port of the service range."),
        new("protocol", "Protocol name or id of the service range.")
    ];

    /// <summary>
    /// Performs the TryValidatePolicyIds operation.
    /// </summary>
    public static bool TryValidatePolicyIds(GetPolicyIdsRequest request, out ActionResult? errorResult)
    {
        return RequestRootValidator.TryValidate(request, PolicyIdsRootSchema, out errorResult);
    }

    /// <summary>
    /// Performs the TryValidateFlowComplianceState operation.
    /// </summary>
    public static bool TryValidateFlowComplianceState(GetFlowComplianceStateRequest request, out ActionResult? errorResult)
    {
        if (!RequestRootValidator.TryValidate(request, FlowComplianceRootSchema, out errorResult))
        {
            return false;
        }

        if (!TryValidateItemList(request.Source, "source", IpRangeKeys, TryValidateIpRange, out errorResult))
        {
            return false;
        }

        if (!TryValidateItemList(request.Destination, "destination", IpRangeKeys, TryValidateIpRange, out errorResult))
        {
            return false;
        }

        if (!TryValidateItemList(request.Service, "service", ServiceRangeKeys, TryValidateServiceRange, out errorResult))
        {
            return false;
        }

        if (!TryValidatePolicies(request.Policies, out errorResult))
        {
            return false;
        }

        errorResult = null;
        return true;
    }

    private static bool TryValidateItemList<TItem>(
        IEnumerable<TItem> items,
        string collectionName,
        IReadOnlyList<RequestKeyDefinition> allowedKeys,
        Func<TItem, string, int, (bool IsValid, string? ErrorMessage)> semanticValidator,
        out ActionResult? errorResult)
        where TItem : IRequestWithAdditionalData
    {
        int index = 0;
        foreach (TItem? item in items)
        {
            if (item is null)
            {
                errorResult = new BadRequestObjectResult($"'{collectionName}' cannot contain null entries.");
                return false;
            }

            if (!TryValidateNestedItem(item, collectionName, allowedKeys, semanticValidator, index, out errorResult))
            {
                return false;
            }

            index++;
        }

        errorResult = null;
        return true;
    }

    private static bool TryValidateNestedItem<TItem>(
        TItem item,
        string collectionName,
        IReadOnlyList<RequestKeyDefinition> allowedKeys,
        Func<TItem, string, int, (bool IsValid, string? ErrorMessage)> semanticValidator,
        int itemIndex,
        out ActionResult? errorResult)
        where TItem : IRequestWithAdditionalData
    {
        if (item.AdditionalData is { Count: > 0 })
        {
            string allowedShapes = string.Join(" or ", allowedKeys.Select(key => $"{{ \"{key.JsonName}\": ... }}"));
            string keyHelp = string.Join(" ", allowedKeys.Select(key => $"'{key.JsonName}': {key.Description}"));

            errorResult = new BadRequestObjectResult(
                $"'{collectionName}' only accepts {allowedShapes}. Valid keys: {keyHelp}");
            return false;
        }

        switch (item)
        {
            case GetFlowComplianceStateRequest.IpRangeRequest ipRange
                when string.IsNullOrWhiteSpace(ipRange.IpStart) || string.IsNullOrWhiteSpace(ipRange.IpEnd):
                errorResult = new BadRequestObjectResult($"'{collectionName}' entries require non-empty 'ipStart' and 'ipEnd'.");
                return false;
            case GetFlowComplianceStateRequest.ServiceRangeRequest serviceRange
                when string.IsNullOrWhiteSpace(serviceRange.Protocol):
                errorResult = new BadRequestObjectResult($"'{collectionName}' entries require non-empty 'protocol'.");
                return false;
        }

        (bool isValid, string? errorMessage) = semanticValidator(item, collectionName, itemIndex);
        if (!isValid)
        {
            errorResult = new BadRequestObjectResult(errorMessage);
            return false;
        }

        errorResult = null;
        return true;
    }

    private static (bool IsValid, string? ErrorMessage) TryValidateIpRange(GetFlowComplianceStateRequest.IpRangeRequest ipRange, string collectionName, int itemIndex)
    {
        if (!IPAddress.TryParse(ipRange.IpStart, out IPAddress? ipStart))
        {
            return (false, $"'{collectionName}' entry at index {itemIndex} has an invalid 'ipStart' value.");
        }

        if (!IPAddress.TryParse(ipRange.IpEnd, out IPAddress? ipEnd))
        {
            return (false, $"'{collectionName}' entry at index {itemIndex} has an invalid 'ipEnd' value.");
        }

        if (ipStart.AddressFamily != ipEnd.AddressFamily)
        {
            return (false, $"'{collectionName}' entry at index {itemIndex} must use the same address family for 'ipStart' and 'ipEnd'.");
        }

        if (CompareIpAddresses(ipStart, ipEnd) > 0)
        {
            return (false, $"'{collectionName}' entry at index {itemIndex} must satisfy 'ipStart' <= 'ipEnd'.");
        }

        return (true, null);
    }

    private static (bool IsValid, string? ErrorMessage) TryValidateServiceRange(GetFlowComplianceStateRequest.ServiceRangeRequest serviceRange, string collectionName, int itemIndex)
    {
        if (serviceRange.PortStart < MinimumPort || serviceRange.PortStart > MaximumPort)
        {
            return (false, $"'{collectionName}' entry at index {itemIndex} has an invalid 'portStart' value. Allowed range is {MinimumPort}-{MaximumPort}.");
        }

        if (serviceRange.PortEnd < MinimumPort || serviceRange.PortEnd > MaximumPort)
        {
            return (false, $"'{collectionName}' entry at index {itemIndex} has an invalid 'portEnd' value. Allowed range is {MinimumPort}-{MaximumPort}.");
        }

        if (serviceRange.PortStart > serviceRange.PortEnd)
        {
            return (false, $"'{collectionName}' entry at index {itemIndex} must satisfy 'portStart' <= 'portEnd'.");
        }

        return (true, null);
    }

    private static int CompareIpAddresses(IPAddress left, IPAddress right)
    {
        byte[] leftBytes = left.GetAddressBytes();
        byte[] rightBytes = right.GetAddressBytes();

        for (int i = 0; i < leftBytes.Length; i++)
        {
            int compare = leftBytes[i].CompareTo(rightBytes[i]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return 0;
    }

    private static bool TryValidatePolicies(IEnumerable<int> policies, out ActionResult? errorResult)
    {
        int index = 0;
        foreach (int policyId in policies)
        {
            if (policyId <= 0)
            {
                errorResult = new BadRequestObjectResult($"'policies' entries must be positive integers. Invalid value at index {index}.");
                return false;
            }

            index++;
        }

        errorResult = null;
        return true;
    }
}
