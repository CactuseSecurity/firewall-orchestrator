using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Sockets;

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

    /// <summary>
    /// Validates a single IP range using the same semantics as flow compliance requests.
    /// </summary>
    public static bool TryValidateIpRange(string ipStart, string ipEnd, string collectionName, int itemIndex, out string? errorMessage)
    {
        bool isValid = TryValidateAndNormalizeIpRange(ipStart, ipEnd, collectionName, itemIndex, out _, out _, out string? validationError);
        errorMessage = validationError;
        return isValid;
    }

    /// <summary>
    /// Validates a single IPv4 range using the same semantics as flow compliance requests.
    /// </summary>
    public static bool TryValidateIpRange(string ipStart, string ipEnd, string context, out string? errorMessage)
    {
        bool isValid = TryValidateAndNormalizeIpRange(ipStart, ipEnd, context, out _, out _, out string? validationError);
        errorMessage = validationError;
        return isValid;
    }

    /// <summary>
    /// Validates a single IP range and returns bounds with optional CIDR/netmask suffixes removed.
    /// </summary>
    public static bool TryValidateAndNormalizeIpRange(
        string ipStart,
        string ipEnd,
        string collectionName,
        int itemIndex,
        out string normalizedIpStart,
        out string normalizedIpEnd,
        out string? errorMessage)
    {
        (bool isValid, string? validationError) = ValidateIpRange(
            ipStart,
            ipEnd,
            detail => $"'{collectionName}' entry at index {itemIndex} {detail}",
            out normalizedIpStart,
            out normalizedIpEnd);
        errorMessage = validationError;
        return isValid;
    }

    /// <summary>
    /// Validates a single IP range and returns bounds with optional CIDR/netmask suffixes removed.
    /// </summary>
    public static bool TryValidateAndNormalizeIpRange(
        string ipStart,
        string ipEnd,
        string context,
        out string normalizedIpStart,
        out string normalizedIpEnd,
        out string? errorMessage)
    {
        (bool isValid, string? validationError) = ValidateIpRange(
            ipStart,
            ipEnd,
            detail => $"'{context}' {detail}",
            out normalizedIpStart,
            out normalizedIpEnd);
        errorMessage = validationError;
        return isValid;
    }

    /// <summary>
    /// Validates a single service range using the same semantics as flow compliance requests.
    /// </summary>
    public static bool TryValidateServiceRange(int portStart, int portEnd, string collectionName, int itemIndex, out string? errorMessage)
    {
        (bool isValid, string? validationError) = ValidateServiceRange(portStart, portEnd, collectionName, itemIndex);
        errorMessage = validationError;
        return isValid;
    }

    /// <summary>
    /// Removes an optional CIDR/netmask suffix from an IP address value.
    /// </summary>
    public static string RemoveCidrMask(string ipAddress)
    {
        ArgumentNullException.ThrowIfNull(ipAddress);

        int maskSeparatorIndex = ipAddress.IndexOf('/');
        return maskSeparatorIndex < 0 ? ipAddress : ipAddress[..maskSeparatorIndex];
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
            errorResult = RequestValidationMessageBuilder.BuildAllowedKeysError($"{collectionName} entry at index {itemIndex}", allowedKeys);
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
        (bool isValid, string? errorMessage) = ValidateIpRange(
            ipRange.IpStart,
            ipRange.IpEnd,
            detail => $"'{collectionName}' entry at index {itemIndex} {detail}",
            out string normalizedIpStart,
            out string normalizedIpEnd);
        if (isValid)
        {
            ipRange.IpStart = normalizedIpStart;
            ipRange.IpEnd = normalizedIpEnd;
        }

        return (isValid, errorMessage);
    }

    private static (bool IsValid, string? ErrorMessage) TryValidateServiceRange(GetFlowComplianceStateRequest.ServiceRangeRequest serviceRange, string collectionName, int itemIndex)
    {
        return ValidateServiceRange(serviceRange.PortStart, serviceRange.PortEnd, collectionName, itemIndex);
    }

    private static (bool IsValid, string? ErrorMessage) ValidateIpRange(
        string ipStartValue,
        string ipEndValue,
        Func<string, string> errorFactory,
        out string normalizedIpStart,
        out string normalizedIpEnd)
    {
        normalizedIpStart = RemoveCidrMask(ipStartValue);
        normalizedIpEnd = RemoveCidrMask(ipEndValue);

        if (!IPAddress.TryParse(normalizedIpStart, out IPAddress? ipStart))
        {
            return (false, errorFactory("has an invalid 'ipStart' value."));
        }

        if (!IPAddress.TryParse(normalizedIpEnd, out IPAddress? ipEnd))
        {
            return (false, errorFactory("has an invalid 'ipEnd' value."));
        }

        if (ipStart.AddressFamily != ipEnd.AddressFamily)
        {
            return (false, errorFactory("must use the same address family for 'ipStart' and 'ipEnd'."));
        }

        if (ipStart.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return (false, errorFactory("does not support IPv6 addresses. Only IPv4 values are allowed for 'ipStart' and 'ipEnd'."));
        }

        if (CompareIpAddresses(ipStart, ipEnd) > 0)
        {
            return (false, errorFactory("must satisfy 'ipStart' <= 'ipEnd'."));
        }

        return (true, null);
    }

    private static (bool IsValid, string? ErrorMessage) ValidateServiceRange(int portStart, int portEnd, string collectionName, int itemIndex)
    {
        if (portStart < MinimumPort || portStart > MaximumPort)
        {
            return (false, $"'{collectionName}' entry at index {itemIndex} has an invalid 'portStart' value. Allowed range is {MinimumPort}-{MaximumPort}.");
        }

        if (portEnd < MinimumPort || portEnd > MaximumPort)
        {
            return (false, $"'{collectionName}' entry at index {itemIndex} has an invalid 'portEnd' value. Allowed range is {MinimumPort}-{MaximumPort}.");
        }

        if (portStart > portEnd)
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
