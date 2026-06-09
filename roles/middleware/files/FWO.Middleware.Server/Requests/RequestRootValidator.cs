using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Requests;

public static class RequestRootValidator
{
    public static bool TryValidate(IRequestWithRootAdditionalData request, RequestRootValidationSchema schema, out ActionResult? errorResult)
    {
        if (request.AdditionalData is not { Count: > 0 })
        {
            errorResult = null;
            return true;
        }

        errorResult = BuildError(schema);
        return false;
    }

    private static BadRequestObjectResult BuildError(RequestRootValidationSchema schema)
    {
        string allowedShapes = string.Join(" or ", new[]
        {
            "{}",
            "{ \"filter\": {} }"
        }.Concat(schema.AllowedKeys.Select(key => $"{{ \"{key.JsonName}\": ... }}")));

        string keyHelp = string.Join(" ", schema.AllowedKeys.Select(key => $"'{key.JsonName}': {key.Description}"));

        return new BadRequestObjectResult(
            $"{schema.EndpointName} only accepts {allowedShapes}. Valid root keys: {keyHelp}");
    }
}
