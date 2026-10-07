using Microsoft.AspNetCore.Mvc;

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

    private static readonly RequestKeyDefinition[] AddressKeys =
    [
        new("ipHost", "One maskless IPv4 or IPv6 address."),
        new("ipNetwork", "One canonical IPv4 or IPv6 CIDR network."),
        new("ipRange", "Two maskless addresses defining an inclusive range.")
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

        if (!TryValidateItemList(request.Source, "source", AddressKeys, TryValidateAddress, out errorResult))
        {
            return false;
        }

        if (!TryValidateItemList(request.Destination, "destination", AddressKeys, TryValidateAddress, out errorResult))
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

    private static (bool IsValid, string? ErrorMessage) TryValidateAddress(GetFlowComplianceStateRequest.IpRangeRequest address, string collectionName, int itemIndex)
    {
        string context = $"'{collectionName}' entry at index {itemIndex}";
        bool isValid = AddressInputNormalizer.TryValidateAndNormalize(
            address,
            context,
            out NormalizedAddressBounds normalizedBounds,
            out string? errorMessage);
        if (isValid)
        {
            address.NormalizedIpStart = normalizedBounds.IpStart;
            address.NormalizedIpEnd = normalizedBounds.IpEnd;
        }

        return (isValid, errorMessage);
    }

    private static (bool IsValid, string? ErrorMessage) TryValidateServiceRange(GetFlowComplianceStateRequest.ServiceRangeRequest serviceRange, string collectionName, int itemIndex)
    {
        return ValidateServiceRange(serviceRange.PortStart, serviceRange.PortEnd, collectionName, itemIndex);
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
