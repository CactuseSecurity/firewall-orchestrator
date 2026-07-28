using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Builds consistent request validation messages for nested request items.
/// </summary>
internal static class RequestValidationMessageBuilder
{
    /// <summary>
    /// Creates a bad-request result for an item with unsupported keys.
    /// </summary>
    public static BadRequestObjectResult BuildAllowedKeysError(string context, IReadOnlyList<RequestKeyDefinition> allowedKeys)
    {
        string allowedShapes = string.Join(" or ", allowedKeys.Select(key => $"{{ \"{key.JsonName}\": ... }}"));
        string keyHelp = string.Join(" ", allowedKeys.Select(key => $"'{key.JsonName}': {key.Description}"));
        return new BadRequestObjectResult($"'{context}' only accepts keys {allowedShapes}. Valid keys: {keyHelp}");
    }
}
