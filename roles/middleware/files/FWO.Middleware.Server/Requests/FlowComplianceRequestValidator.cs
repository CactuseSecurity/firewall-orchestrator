using FWO.Basics;
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
    private const string AllowedIpMask = "32";
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
        new("ipEnd", "End IP address of the range."),
        new("ipNetwork", "CIDR network to evaluate instead of ipStart and ipEnd.")
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
    /// Validates a single IPv4 or IPv6 range using the same semantics as flow compliance requests.
    /// </summary>
    public static bool TryValidateIpRange(string ipStart, string ipEnd, string context, out string? errorMessage)
    {
        bool isValid = TryValidateAndNormalizeIpRange(ipStart, ipEnd, context, out _, out _, out string? validationError);
        errorMessage = validationError;
        return isValid;
    }

    /// <summary>
    /// Validates a single IPv4 or IPv6 range and returns its inclusive bounds.
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
    /// Validates an IPv4 or IPv6 CIDR network and returns its inclusive range bounds.
    /// </summary>
    public static bool TryValidateAndNormalizeIpNetwork(
        string ipNetwork,
        string context,
        out string normalizedIpStart,
        out string normalizedIpEnd,
        out string? errorMessage)
    {
        if (!TryNormalizeIpNetwork(ipNetwork, out normalizedIpStart, out normalizedIpEnd, out string? validationError))
        {
            errorMessage = $"'{context}' {validationError}";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Validates a single IPv4 or IPv6 range and returns its inclusive bounds.
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
    /// Removes an optional /32 CIDR suffix from an IP address value.
    /// </summary>
    public static string RemoveCidrMask(string ipAddress)
    {
        ArgumentNullException.ThrowIfNull(ipAddress);

        if (!TryRemoveAllowedHostMask(ipAddress, "ipAddress", out string normalizedIpAddress, out string? errorMessage))
        {
            throw new ArgumentException(errorMessage, nameof(ipAddress));
        }

        return normalizedIpAddress;
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
        bool hasIpNetwork = !string.IsNullOrWhiteSpace(ipRange.IpNetwork);
        bool hasIpRange = !string.IsNullOrWhiteSpace(ipRange.IpStart) || !string.IsNullOrWhiteSpace(ipRange.IpEnd);
        string context = $"'{collectionName}' entry at index {itemIndex}";
        if (hasIpNetwork && hasIpRange)
        {
            return (false, $"{context} must define either 'ipNetwork' or 'ipStart' and 'ipEnd', not both.");
        }

        if (hasIpNetwork)
        {
            bool isNetworkValid = TryValidateAndNormalizeIpNetwork(
                ipRange.IpNetwork,
                context,
                out string normalizedNetworkStart,
                out string normalizedNetworkEnd,
                out string? networkError);
            if (isNetworkValid)
            {
                ipRange.IpStart = normalizedNetworkStart;
                ipRange.IpEnd = normalizedNetworkEnd;
            }

            return (isNetworkValid, networkError);
        }

        if (string.IsNullOrWhiteSpace(ipRange.IpStart) || string.IsNullOrWhiteSpace(ipRange.IpEnd))
        {
            return (false, $"{context} requires non-empty 'ipStart' and 'ipEnd', or a non-empty 'ipNetwork'.");
        }

        (bool isValid, string? errorMessage) = ValidateIpRange(
            ipRange.IpStart,
            ipRange.IpEnd,
            detail => $"{context} {detail}",
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
        if (!TryValidateIpRangeBound(ipStartValue, "ipStart", out normalizedIpStart, out string? ipStartMaskError))
        {
            normalizedIpEnd = string.Empty;
            return (false, errorFactory(ipStartMaskError!));
        }

        if (!TryValidateIpRangeBound(ipEndValue, "ipEnd", out normalizedIpEnd, out string? ipEndMaskError))
        {
            return (false, errorFactory(ipEndMaskError!));
        }

        return ValidateNormalizedIpRange(normalizedIpStart, normalizedIpEnd, errorFactory);
    }

    private static (bool IsValid, string? ErrorMessage) ValidateNormalizedIpRange(
        string normalizedIpStart,
        string normalizedIpEnd,
        Func<string, string> errorFactory)
    {
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

        if (CompareIpAddresses(ipStart, ipEnd) > 0)
        {
            return (false, errorFactory("must satisfy 'ipStart' <= 'ipEnd'."));
        }

        return (true, null);
    }

    private static bool TryValidateIpRangeBound(
        string ipAddressValue,
        string fieldName,
        out string normalizedIpAddress,
        out string? errorMessage)
    {
        if (ipAddressValue.IndexOf('/') < 0)
        {
            normalizedIpAddress = ipAddressValue;
            errorMessage = null;
            return true;
        }

        normalizedIpAddress = string.Empty;
        errorMessage = $"must not use CIDR notation in '{fieldName}'. Use 'ipNetwork' for networks.";
        return false;
    }

    private static bool TryNormalizeIpNetwork(string ipNetwork, out string normalizedIpStart, out string normalizedIpEnd, out string? errorMessage)
    {
        int maskSeparatorIndex = ipNetwork.IndexOf('/');
        if (maskSeparatorIndex <= 0 || maskSeparatorIndex != ipNetwork.LastIndexOf('/'))
        {
            normalizedIpStart = string.Empty;
            normalizedIpEnd = string.Empty;
            errorMessage = "requires a valid CIDR network in 'ipNetwork'.";
            return false;
        }

        string address = ipNetwork[..maskSeparatorIndex];
        string prefix = ipNetwork[(maskSeparatorIndex + 1)..];
        if (!IPAddress.TryParse(address, out IPAddress? parsedAddress) || !int.TryParse(prefix, out int prefixLength))
        {
            normalizedIpStart = string.Empty;
            normalizedIpEnd = string.Empty;
            errorMessage = "has an invalid 'ipNetwork' value.";
            return false;
        }

        int maximumPrefixLength = parsedAddress.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength is < 0 or > 128 || prefixLength > maximumPrefixLength)
        {
            normalizedIpStart = string.Empty;
            normalizedIpEnd = string.Empty;
            errorMessage = "has an invalid CIDR prefix in 'ipNetwork'.";
            return false;
        }

        (IPAddress rangeStart, IPAddress rangeEnd) = ipNetwork.CidrToRange();
        normalizedIpStart = rangeStart.ToString();
        normalizedIpEnd = rangeEnd.ToString();
        errorMessage = null;
        return true;
    }

    private static bool TryRemoveAllowedHostMask(string ipAddress, string fieldName, out string normalizedIpAddress, out string? errorMessage)
    {
        int maskSeparatorIndex = ipAddress.IndexOf('/');
        if (maskSeparatorIndex < 0)
        {
            normalizedIpAddress = ipAddress;
            errorMessage = null;
            return true;
        }

        string mask = ipAddress[(maskSeparatorIndex + 1)..];
        if (mask != AllowedIpMask)
        {
            normalizedIpAddress = string.Empty;
            errorMessage = $"has unsupported netmask '/{mask}' in '{fieldName}'. Only '/{AllowedIpMask}' is allowed.";
            return false;
        }

        normalizedIpAddress = ipAddress[..maskSeparatorIndex];
        errorMessage = null;
        return true;
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
