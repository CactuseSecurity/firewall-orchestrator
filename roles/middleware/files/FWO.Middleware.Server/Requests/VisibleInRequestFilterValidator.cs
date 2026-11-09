using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the VisibleInRequestFilterValidator type.
/// </summary>
public static class VisibleInRequestFilterValidator
{
    /// <summary>
    /// Performs the TryValidate operation.
    /// </summary>
    public static bool TryValidate(IVisibleInRequestFilterRequest request, RequestFilterValidationSchema schema, out ActionResult? errorResult)
    {
        if (request.AdditionalData is { Count: > 0 })
        {
            errorResult = BuildError(schema);
            return false;
        }

        if (request.Filter is null)
        {
            errorResult = null;
            return true;
        }

        if (request.Filter.AdditionalData is { Count: > 0 })
        {
            errorResult = BuildError(schema);
            return false;
        }

        errorResult = null;
        return true;
    }

    private static BadRequestObjectResult BuildError(RequestFilterValidationSchema schema)
    {
        string allowedShapes = string.Join(" or ", new[]
        {
            "{}",
            "{ \"filter\": {} }"
        }.Concat(schema.AllowedKeys.Select(key => $"{{ \"filter\": {{ \"{key.JsonName}\": ... }} }}")));

        string keyHelp = string.Join(" ", schema.AllowedKeys.Select(key => $"'{key.JsonName}': {key.Description}"));

        return new BadRequestObjectResult(
            $"{schema.EndpointName} only accepts {allowedShapes}. Valid filter keys: {keyHelp}");
    }
}
