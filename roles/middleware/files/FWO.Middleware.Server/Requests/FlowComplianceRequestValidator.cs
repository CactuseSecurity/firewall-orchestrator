using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Requests;

public static class FlowComplianceRequestValidator
{
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

    public static bool TryValidatePolicyIds(GetPolicyIdsRequest request, out ActionResult? errorResult)
    {
        return RequestRootValidator.TryValidate(request, PolicyIdsRootSchema, out errorResult);
    }

    public static bool TryValidateFlowComplianceState(GetFlowComplianceStateRequest request, out ActionResult? errorResult)
    {
        if (!RequestRootValidator.TryValidate(request, FlowComplianceRootSchema, out errorResult))
        {
            return false;
        }

        if (!TryValidateItemList(request.Source, "source", IpRangeKeys, out errorResult))
        {
            return false;
        }

        if (!TryValidateItemList(request.Destination, "destination", IpRangeKeys, out errorResult))
        {
            return false;
        }

        if (!TryValidateItemList(request.Service, "service", ServiceRangeKeys, out errorResult))
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
        out ActionResult? errorResult)
        where TItem : IRequestWithAdditionalData
    {
        foreach (TItem? item in items)
        {
            if (item is null)
            {
                errorResult = new BadRequestObjectResult($"'{collectionName}' cannot contain null entries.");
                return false;
            }

            if (!TryValidateNestedItem(item, collectionName, allowedKeys, out errorResult))
            {
                return false;
            }
        }

        errorResult = null;
        return true;
    }

    private static bool TryValidateNestedItem<TItem>(
        TItem item,
        string collectionName,
        IReadOnlyList<RequestKeyDefinition> allowedKeys,
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

        errorResult = null;
        return true;
    }
}
