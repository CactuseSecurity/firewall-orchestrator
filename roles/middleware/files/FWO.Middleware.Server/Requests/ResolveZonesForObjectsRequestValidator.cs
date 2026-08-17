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

    private static readonly RequestValidationSchema RootSchema = RequestValidationSchema
        .Endpoint(EndpointName)
        .ObjectRoot()
        .RequiredList("objects");

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
        if (!RequestValidator.TryValidate(request, RootSchema, out errorResult))
        {
            return false;
        }

        if (request.Objects is null || request.Objects.Count == 0)
        {
            errorResult = BuildValidationError("objects", "'objects' must contain at least one entry.");
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
                errorResult = BuildValidationError(
                    RequestFieldPath.Indexed(collectionName, index),
                    $"'{collectionName}' cannot contain null entries.");
                return false;
            }

            validationStatistics.ObjectCount++;
            if (validationStatistics.ObjectCount > MaximumObjectCount)
            {
                errorResult = BuildValidationError(collectionName, $"'{EndpointName}' accepts at most {MaximumObjectCount} objects per request.");
                return false;
            }

            if (!TryValidateObject(node, RequestFieldPath.Indexed(collectionName, index), depth, validationStatistics, out errorResult))
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
                errorResult = BuildValidationError(context, $"'{context}' has an unsupported object node type.");
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
            errorResult = BuildUnknownKeyError(context, group.AdditionalData.Keys);
            return false;
        }

        if (depth >= MaximumObjectDepth)
        {
            errorResult = BuildValidationError(context, $"'{EndpointName}' supports at most {MaximumObjectDepth} nested object levels.");
            return false;
        }

        if (group.Members is null || group.Members.Count == 0)
        {
            errorResult = BuildValidationError(RequestFieldPath.Child(context, "members"), $"'{context}' must contain at least one member.");
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
            errorResult = BuildUnknownKeyError(context, leaf.AdditionalData.Keys);
            return false;
        }

        if (string.IsNullOrWhiteSpace(leaf.Type)
            && string.IsNullOrWhiteSpace(leaf.IpStart)
            && string.IsNullOrWhiteSpace(leaf.IpEnd))
        {
            errorResult = BuildValidationError(context, $"'{context}' must define either non-empty 'members' or the leaf fields 'type', 'ipStart', and 'ipEnd'.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(leaf.Type))
        {
            errorResult = BuildValidationError(RequestFieldPath.Child(context, "type"), $"'{context}' requires a non-empty 'type'.");
            return false;
        }

        if (!IsLeafType(leaf.Type))
        {
            errorResult = BuildValidationError(
                RequestFieldPath.Child(context, "type"),
                $"'{context}' has an unsupported 'type' value. Allowed values are '{ObjectType.Host}', '{ObjectType.Network}', and '{ObjectType.IPRange}'.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(leaf.IpStart) || string.IsNullOrWhiteSpace(leaf.IpEnd))
        {
            errorResult = BuildValidationError(context, $"'{context}' requires non-empty 'ipStart' and 'ipEnd'.");
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
            errorResult = BuildValidationError(context, ipRangeError!);
            return false;
        }

        leaf.IpStart = normalizedIpStart;
        leaf.IpEnd = normalizedIpEnd;
        if (string.Equals(leaf.Type, ObjectType.Host, StringComparison.OrdinalIgnoreCase)
            && !IPAddress.Parse(leaf.IpStart).Equals(IPAddress.Parse(leaf.IpEnd)))
        {
            errorResult = BuildValidationError(context, $"'{context}' entries of type '{ObjectType.Host}' must use the same 'ipStart' and 'ipEnd'.");
            return false;
        }

        validationStatistics.RangeCount++;
        if (validationStatistics.RangeCount > MaximumIpRangeCount)
        {
            errorResult = BuildValidationError(context, $"'{EndpointName}' accepts at most {MaximumIpRangeCount} IP ranges per request.");
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

    private static BadRequestObjectResult BuildUnknownKeyError(string context, IEnumerable<string> unknownKeys)
    {
        RequestValidationErrors errors = new();
        foreach (string unknownKey in unknownKeys)
        {
            errors.AddUnknownField(RequestFieldPath.Child(context, unknownKey));
        }
        return RequestValidationProblemDetailsFactory.BadRequest(errors);
    }

    private static BadRequestObjectResult BuildValidationError(string fieldPath, string message)
    {
        RequestValidationErrors errors = new();
        errors.Add(fieldPath, message);
        return RequestValidationProblemDetailsFactory.BadRequest(errors);
    }
}
