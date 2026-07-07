using FWO.Basics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates requests for zone resolution.
/// </summary>
public static class ResolveZonesForObjectsRequestValidator
{
    private const string EndpointName = "resolveZonesForObjects";

    private static readonly RequestRootValidationSchema RootSchema = new(
        EndpointName,
        [
            new RequestKeyDefinition("objects", "Objects to resolve.")
        ]);

    private static readonly RequestKeyDefinition[] LeafKeys =
    [
        new("name", "Display name of the object."),
        new("type", "Object type of the object."),
        new("ipStart", "Start IP address or range value of the object."),
        new("ipEnd", "End IP address or range value of the object.")
    ];

    private static readonly RequestKeyDefinition[] GroupKeys =
    [
        new("name", "Display name of the object."),
        new("members", "Nested members of the group.")
    ];

    /// <summary>
    /// Validates the zone resolution request.
    /// </summary>
    public static bool TryValidate(ResolveZonesForObjectsRequest request, out ActionResult? errorResult)
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

        if (request.Objects is null || request.Objects.Count == 0)
        {
            errorResult = new BadRequestObjectResult("'objects' must contain at least one entry.");
            return false;
        }

        if (!TryValidateObjects(request.Objects ?? [], "objects", out errorResult))
        {
            return false;
        }

        errorResult = null;
        return true;
    }

    private static bool TryValidateObjects(IEnumerable<ResolveZonesForObjectsRequest.ObjectRequest> objects, string collectionName, out ActionResult? errorResult)
    {
        int index = 0;
        foreach (ResolveZonesForObjectsRequest.ObjectRequest? node in objects)
        {
            if (node is null)
            {
                errorResult = new BadRequestObjectResult($"'{collectionName}' cannot contain null entries.");
                return false;
            }

            if (!TryValidateObject(node, $"{collectionName} entry at index {index}", out errorResult))
            {
                return false;
            }

            index++;
        }

        errorResult = null;
        return true;
    }

    private static bool TryValidateObject(ResolveZonesForObjectsRequest.ObjectRequest node, string context, out ActionResult? errorResult)
    {
        switch (node)
        {
            case ResolveZonesForObjectsRequest.GroupObjectRequest group:
                return TryValidateGroup(group, context, out errorResult);
            case ResolveZonesForObjectsRequest.LeafObjectRequest leaf:
                return TryValidateLeaf(leaf, context, out errorResult);
            default:
                errorResult = new BadRequestObjectResult($"'{context}' has an unsupported object node type.");
                return false;
        }
    }

    private static bool TryValidateGroup(ResolveZonesForObjectsRequest.GroupObjectRequest group, string context, out ActionResult? errorResult)
    {
        if (group.AdditionalData is { Count: > 0 })
        {
            string allowedShapes = string.Join(" or ", GroupKeys.Select(key => $"{{ \"{key.JsonName}\": ... }}"));
            string keyHelp = string.Join(" ", GroupKeys.Select(key => $"'{key.JsonName}': {key.Description}"));
            errorResult = new BadRequestObjectResult($"'{context}' only accepts {allowedShapes}. Valid keys: {keyHelp}");
            return false;
        }

        if (group.Members is null || group.Members.Count == 0)
        {
            errorResult = new BadRequestObjectResult($"'{context}' must contain at least one member.");
            return false;
        }

        return TryValidateObjects(group.Members, $"{context}.members", out errorResult);
    }

    private static bool TryValidateLeaf(ResolveZonesForObjectsRequest.LeafObjectRequest leaf, string context, out ActionResult? errorResult)
    {
        if (leaf.AdditionalData is { Count: > 0 })
        {
            string allowedShapes = string.Join(" or ", LeafKeys.Select(key => $"{{ \"{key.JsonName}\": ... }}"));
            string keyHelp = string.Join(" ", LeafKeys.Select(key => $"'{key.JsonName}': {key.Description}"));
            errorResult = new BadRequestObjectResult($"'{context}' only accepts {allowedShapes}. Valid keys: {keyHelp}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(leaf.Type))
        {
            errorResult = new BadRequestObjectResult($"'{context}' requires a non-empty 'type'.");
            return false;
        }

        if (!IsLeafType(leaf.Type))
        {
            errorResult = new BadRequestObjectResult(
                $"'{context}' has an unsupported 'type' value. Allowed values are '{ObjectType.Host}', '{ObjectType.Network}', and '{ObjectType.IPRange}'.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(leaf.IpStart) || string.IsNullOrWhiteSpace(leaf.IpEnd))
        {
            errorResult = new BadRequestObjectResult($"'{context}' requires non-empty 'ipStart' and 'ipEnd'.");
            return false;
        }

        if (!TryValidateIpRange(leaf.IpStart, leaf.IpEnd, context, out errorResult))
        {
            return false;
        }

        if (string.Equals(leaf.Type, ObjectType.Host, StringComparison.OrdinalIgnoreCase)
            && CompareIpAddresses(IPAddress.Parse(leaf.IpStart), IPAddress.Parse(leaf.IpEnd)) != 0)
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
