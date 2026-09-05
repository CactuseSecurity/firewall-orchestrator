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
    private const int Ipv4HostPrefixLength = 32;
    private const int Ipv6HostPrefixLength = 128;
    private const int BitsPerByte = 8;
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
            errorMessage = $"{context} {validationError}";
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

        if (ipStart.IsIPv4MappedToIPv6 || ipEnd.IsIPv4MappedToIPv6)
        {
            return (false, errorFactory("contains an IPv4-mapped IPv6 value. Use the dotted IPv4 form instead."));
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

    /// <summary>
    /// Accepts a range bound with no mask or with the host mask of its address family and strips the mask.
    /// Every broader mask is rejected and pointed at 'ipNetwork'.
    /// </summary>
    private static bool TryValidateIpRangeBound(
        string ipAddressValue,
        string fieldName,
        out string normalizedIpAddress,
        out string? errorMessage)
    {
        int maskSeparatorIndex = ipAddressValue.IndexOf('/');
        if (maskSeparatorIndex < 0)
        {
            normalizedIpAddress = ipAddressValue;
            errorMessage = null;
            return true;
        }

        string address = ipAddressValue[..maskSeparatorIndex];
        string mask = ipAddressValue[(maskSeparatorIndex + 1)..];
        if (!IPAddress.TryParse(address, out IPAddress? parsedAddress))
        {
            // The address itself is invalid; let the range validation report the value rather than the mask.
            normalizedIpAddress = address;
            errorMessage = null;
            return true;
        }

        if (parsedAddress.IsIPv4MappedToIPv6)
        {
            normalizedIpAddress = string.Empty;
            errorMessage = $"has an IPv4-mapped IPv6 value in '{fieldName}'. Use the dotted IPv4 form instead.";
            return false;
        }

        int hostPrefixLength = GetHostPrefixLength(parsedAddress.AddressFamily);
        if (!int.TryParse(mask, out int prefixLength) || prefixLength != hostPrefixLength)
        {
            normalizedIpAddress = string.Empty;
            errorMessage = $"has unsupported netmask '/{mask}' in '{fieldName}'. Only '/{hostPrefixLength}' is allowed; use 'ipNetwork' for networks.";
            return false;
        }

        normalizedIpAddress = address;
        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Parses a CIDR network exactly once and returns the inclusive bounds of the addressed block.
    /// Networks carrying host bits are rejected so that no request is silently widened.
    /// </summary>
    private static bool TryNormalizeIpNetwork(string ipNetwork, out string normalizedIpStart, out string normalizedIpEnd, out string? errorMessage)
    {
        normalizedIpStart = string.Empty;
        normalizedIpEnd = string.Empty;

        int maskSeparatorIndex = ipNetwork.IndexOf('/');
        if (maskSeparatorIndex <= 0 || maskSeparatorIndex != ipNetwork.LastIndexOf('/'))
        {
            errorMessage = "requires a valid CIDR network in 'ipNetwork'.";
            return false;
        }

        if (!IPAddress.TryParse(ipNetwork[..maskSeparatorIndex], out IPAddress? parsedAddress)
            || !int.TryParse(ipNetwork[(maskSeparatorIndex + 1)..], out int prefixLength))
        {
            errorMessage = "has an invalid 'ipNetwork' value.";
            return false;
        }

        if (parsedAddress.IsIPv4MappedToIPv6)
        {
            errorMessage = "has an IPv4-mapped IPv6 value in 'ipNetwork'. Use the dotted IPv4 form instead.";
            return false;
        }

        int hostPrefixLength = GetHostPrefixLength(parsedAddress.AddressFamily);
        if (prefixLength < 0 || prefixLength > hostPrefixLength)
        {
            errorMessage = "has an invalid CIDR prefix in 'ipNetwork'.";
            return false;
        }

        (IPAddress networkAddress, IPAddress lastAddress) = GetNetworkBounds(parsedAddress, prefixLength);
        if (!networkAddress.Equals(parsedAddress))
        {
            errorMessage = $"must not set host bits in 'ipNetwork'. Use '{networkAddress}/{prefixLength}' to evaluate that network.";
            return false;
        }

        normalizedIpStart = networkAddress.ToString();
        normalizedIpEnd = lastAddress.ToString();
        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Computes the first and last address of a CIDR block from an already parsed address and prefix length.
    /// </summary>
    private static (IPAddress NetworkAddress, IPAddress LastAddress) GetNetworkBounds(IPAddress address, int prefixLength)
    {
        byte[] networkBytes = address.GetAddressBytes();
        byte[] lastBytes = address.GetAddressBytes();

        for (int byteIndex = 0; byteIndex < networkBytes.Length; byteIndex++)
        {
            int significantBits = Math.Clamp(prefixLength - (byteIndex * BitsPerByte), 0, BitsPerByte);
            byte mask = significantBits == 0 ? (byte)0 : (byte)(byte.MaxValue << (BitsPerByte - significantBits));
            networkBytes[byteIndex] &= mask;
            lastBytes[byteIndex] |= (byte)~mask;
        }

        return (new IPAddress(networkBytes), new IPAddress(lastBytes));
    }

    /// <summary>
    /// Returns the prefix length that addresses a single host in the given address family.
    /// </summary>
    private static int GetHostPrefixLength(AddressFamily addressFamily)
    {
        return addressFamily == AddressFamily.InterNetwork ? Ipv4HostPrefixLength : Ipv6HostPrefixLength;
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
