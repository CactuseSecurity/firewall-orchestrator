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
    private const int MaximumObjectDepth = 32;
    private const int MaximumObjectCount = 4096;
    private const int MaximumIpRangeCount = 2048;

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

        ValidationStatistics validationStatistics = new();
        if (!TryValidateObjects(request.Objects, "objects", 1, validationStatistics, out errorResult))
        {
            return false;
        }

        errorResult = null;
        return true;
    }

    private static bool TryValidateObjects(
        IEnumerable<ResolveZonesForObjectsRequest.ObjectRequest> objects,
        string collectionName,
        int depth,
        ValidationStatistics validationStatistics,
        out ActionResult? errorResult)
    {
        int index = 0;
        foreach (ResolveZonesForObjectsRequest.ObjectRequest? node in objects)
        {
            if (node is null)
            {
                errorResult = new BadRequestObjectResult($"'{collectionName}' cannot contain null entries.");
                return false;
            }

            validationStatistics.ObjectCount++;
            if (validationStatistics.ObjectCount > MaximumObjectCount)
            {
                errorResult = new BadRequestObjectResult($"'{EndpointName}' accepts at most {MaximumObjectCount} objects per request.");
                return false;
            }

            if (!TryValidateObject(node, $"{collectionName} entry at index {index}", depth, validationStatistics, out errorResult))
            {
                return false;
            }

            index++;
        }

        errorResult = null;
        return true;
    }

    private static bool TryValidateObject(
        ResolveZonesForObjectsRequest.ObjectRequest node,
        string context,
        int depth,
        ValidationStatistics validationStatistics,
        out ActionResult? errorResult)
    {
        switch (node)
        {
            case ResolveZonesForObjectsRequest.GroupObjectRequest group:
                return TryValidateGroup(group, context, depth, validationStatistics, out errorResult);
            case ResolveZonesForObjectsRequest.LeafObjectRequest leaf:
                return TryValidateLeaf(leaf, context, validationStatistics, out errorResult);
            default:
                errorResult = new BadRequestObjectResult($"'{context}' has an unsupported object node type.");
                return false;
        }
    }

    private static bool TryValidateGroup(
        ResolveZonesForObjectsRequest.GroupObjectRequest group,
        string context,
        int depth,
        ValidationStatistics validationStatistics,
        out ActionResult? errorResult)
    {
        if (group.AdditionalData is { Count: > 0 })
        {
            errorResult = RequestValidationMessageBuilder.BuildAllowedKeysError(context, GroupKeys);
            return false;
        }

        if (depth >= MaximumObjectDepth)
        {
            errorResult = new BadRequestObjectResult($"'{EndpointName}' supports at most {MaximumObjectDepth} nested object levels.");
            return false;
        }

        if (group.Members is null || group.Members.Count == 0)
        {
            errorResult = new BadRequestObjectResult($"'{context}' must contain at least one member.");
            return false;
        }

        return TryValidateObjects(group.Members, $"{context}.members", depth + 1, validationStatistics, out errorResult);
    }

    private static bool TryValidateLeaf(
        ResolveZonesForObjectsRequest.LeafObjectRequest leaf,
        string context,
        ValidationStatistics validationStatistics,
        out ActionResult? errorResult)
    {
        if (leaf.AdditionalData is { Count: > 0 })
        {
            errorResult = RequestValidationMessageBuilder.BuildAllowedKeysError(context, LeafKeys);
            return false;
        }

        if (string.IsNullOrWhiteSpace(leaf.Type)
            && string.IsNullOrWhiteSpace(leaf.IpStart)
            && string.IsNullOrWhiteSpace(leaf.IpEnd))
        {
            errorResult = new BadRequestObjectResult($"'{context}' must define either non-empty 'members' or the leaf fields 'type', 'ipStart', and 'ipEnd'.");
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

        if (!FlowComplianceRequestValidator.TryValidateAndNormalizeIpRange(
            leaf.IpStart,
            leaf.IpEnd,
            context,
            out string normalizedIpStart,
            out string normalizedIpEnd,
            out string? ipRangeError))
        {
            errorResult = new BadRequestObjectResult(ipRangeError);
            return false;
        }

        leaf.IpStart = normalizedIpStart;
        leaf.IpEnd = normalizedIpEnd;
        if (string.Equals(leaf.Type, ObjectType.Host, StringComparison.OrdinalIgnoreCase)
            && !IPAddress.Parse(leaf.IpStart).Equals(IPAddress.Parse(leaf.IpEnd)))
        {
            errorResult = new BadRequestObjectResult($"'{context}' entries of type '{ObjectType.Host}' must use the same 'ipStart' and 'ipEnd'.");
            return false;
        }

        validationStatistics.RangeCount++;
        if (validationStatistics.RangeCount > MaximumIpRangeCount)
        {
            errorResult = new BadRequestObjectResult($"'{EndpointName}' accepts at most {MaximumIpRangeCount} IP ranges per request.");
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

    private sealed class ValidationStatistics
    {
        public int ObjectCount { get; set; }

        public int RangeCount { get; set; }
    }
}
