using FWO.Basics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates requests for draft zone resolution.
/// </summary>
public static class GetZonesForDraftObjectsRequestValidator
{
    private const string EndpointName = "resolveZonesForObjects";

    private static readonly RequestRootValidationSchema RootSchema = new(
        EndpointName,
        [
            new RequestKeyDefinition("objects", "Draft flow network objects to resolve.")
        ]);

    private static readonly RequestKeyDefinition[] ObjectKeys =
    [
        new("name", "Display name of the draft object."),
        new("type", "Object type of the draft object."),
        new("ipStart", "Start IP address or range value of the draft object."),
        new("ipEnd", "End IP address or range value of the draft object."),
        new("members", "Nested draft members for draft groups.")
    ];

    /// <summary>
    /// Validates the draft object zone request.
    /// </summary>
    public static bool TryValidate(GetZonesForDraftObjectsRequest request, out ActionResult? errorResult)
    {
        if (request is null)
        {
            errorResult = new BadRequestObjectResult("Request body is required.");
            return false;
        }

        if (!RequestRootValidator.TryValidate(request, RootSchema, out errorResult))
        {
            return false;
        }

        if (!TryValidateObjects(request.Objects ?? [], "objects", out errorResult))
        {
            return false;
        }

        errorResult = null;
        return true;
    }

    private static bool TryValidateObjects(IEnumerable<GetZonesForDraftObjectsRequest.DraftObjectRequest> objects, string collectionName, out ActionResult? errorResult)
    {
        int index = 0;
        foreach (GetZonesForDraftObjectsRequest.DraftObjectRequest? draftObject in objects)
        {
            if (draftObject is null)
            {
                errorResult = new BadRequestObjectResult($"'{collectionName}' cannot contain null entries.");
                return false;
            }

            if (!TryValidateObject(draftObject, $"{collectionName} entry at index {index}", out errorResult))
            {
                return false;
            }

            index++;
        }

        errorResult = null;
        return true;
    }

    private static bool TryValidateObject(GetZonesForDraftObjectsRequest.DraftObjectRequest draftObject, string context, out ActionResult? errorResult)
    {
        if (draftObject.AdditionalData is { Count: > 0 })
        {
            string allowedShapes = string.Join(" or ", ObjectKeys.Select(key => $"{{ \"{key.JsonName}\": ... }}"));
            string keyHelp = string.Join(" ", ObjectKeys.Select(key => $"'{key.JsonName}': {key.Description}"));
            errorResult = new BadRequestObjectResult($"'{context}' only accepts {allowedShapes}. Valid keys: {keyHelp}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(draftObject.Type))
        {
            errorResult = new BadRequestObjectResult($"'{context}' requires a non-empty 'type'.");
            return false;
        }

        if (IsGroupType(draftObject.Type))
        {
            if (!string.IsNullOrWhiteSpace(draftObject.IpStart) || !string.IsNullOrWhiteSpace(draftObject.IpEnd))
            {
                errorResult = new BadRequestObjectResult($"'{context}' entries of type '{ObjectType.Group}' must not define 'ipStart' or 'ipEnd'.");
                return false;
            }

            return TryValidateObjects(draftObject.Members ?? [], $"{context}.members", out errorResult);
        }

        if (!IsLeafType(draftObject.Type))
        {
            errorResult = new BadRequestObjectResult(
                $"'{context}' has an unsupported 'type' value. Allowed values are '{ObjectType.Group}', '{ObjectType.Host}', '{ObjectType.Network}', and '{ObjectType.IPRange}'.");
            return false;
        }

        if (draftObject.Members is { Count: > 0 })
        {
            errorResult = new BadRequestObjectResult($"'{context}' entries of type '{draftObject.Type}' must not define 'members'.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(draftObject.IpStart) || string.IsNullOrWhiteSpace(draftObject.IpEnd))
        {
            errorResult = new BadRequestObjectResult($"'{context}' entries require non-empty 'ipStart' and 'ipEnd'.");
            return false;
        }

        if (!TryValidateIpRange(draftObject.IpStart, draftObject.IpEnd, context, out errorResult))
        {
            return false;
        }

        if (string.Equals(draftObject.Type, ObjectType.Host, StringComparison.OrdinalIgnoreCase)
            && CompareIpAddresses(IPAddress.Parse(draftObject.IpStart), IPAddress.Parse(draftObject.IpEnd)) != 0)
        {
            errorResult = new BadRequestObjectResult($"'{context}' entries of type '{ObjectType.Host}' must use the same 'ipStart' and 'ipEnd'.");
            return false;
        }

        errorResult = null;
        return true;
    }

    private static bool TryValidateIpRange(string ipStartValue, string ipEndValue, string context, out ActionResult? errorResult)
    {
        if (!IPAddress.TryParse(ipStartValue, out IPAddress? ipStart))
        {
            errorResult = new BadRequestObjectResult($"'{context}' has an invalid 'ipStart' value.");
            return false;
        }

        if (!IPAddress.TryParse(ipEndValue, out IPAddress? ipEnd))
        {
            errorResult = new BadRequestObjectResult($"'{context}' has an invalid 'ipEnd' value.");
            return false;
        }

        if (ipStart.AddressFamily != ipEnd.AddressFamily)
        {
            errorResult = new BadRequestObjectResult($"'{context}' must use the same address family for 'ipStart' and 'ipEnd'.");
            return false;
        }

        if (CompareIpAddresses(ipStart, ipEnd) > 0)
        {
            errorResult = new BadRequestObjectResult($"'{context}' must satisfy 'ipStart' <= 'ipEnd'.");
            return false;
        }

        errorResult = null;
        return true;
    }

    private static bool IsGroupType(string objectType)
    {
        return string.Equals(objectType, ObjectType.Group, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLeafType(string objectType)
    {
        return string.Equals(objectType, ObjectType.Host, StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectType, ObjectType.Network, StringComparison.OrdinalIgnoreCase)
            || string.Equals(objectType, ObjectType.IPRange, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareIpAddresses(IPAddress left, IPAddress right)
    {
        byte[] leftBytes = left.GetAddressBytes();
        byte[] rightBytes = right.GetAddressBytes();

        for (int index = 0; index < leftBytes.Length; index++)
        {
            int compare = leftBytes[index].CompareTo(rightBytes[index]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return 0;
    }
}
