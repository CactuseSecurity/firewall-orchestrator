using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates filters accepted by Flow Catalog group endpoints.
/// </summary>
public static class FlowCatalogGroupFilterValidator
{
    /// <summary>
    /// Checks that the request and its group filter only carry supported keys.
    /// </summary>
    public static bool TryValidate(
        IFlowCatalogGroupFilterRequest request,
        RequestFilterValidationSchema schema,
        out ActionResult? errorResult)
    {
        if (request.AdditionalData is { Count: > 0 }
            || request.Filter?.AdditionalData is { Count: > 0 })
        {
            errorResult = BuildError(schema);
            return false;
        }

        errorResult = null;
        return true;
    }

    /// <summary>
    /// Builds the endpoint-specific error returned for unsupported request keys.
    /// </summary>
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
