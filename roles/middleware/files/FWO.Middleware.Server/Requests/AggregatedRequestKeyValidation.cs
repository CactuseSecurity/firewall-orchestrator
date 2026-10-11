using FWO.Middleware.Server.Responses;
using System.Text.Json;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Shared building blocks of the validators that report every key-level error of a request together
/// in one <see cref="RequestValidationErrorResponse"/>.
/// </summary>
public static class AggregatedRequestKeyValidation
{
    /// <summary>
    /// Builds the help text listing the valid keys of one request object.
    /// </summary>
    /// <param name="allowedKeys">Keys the object accepts.</param>
    /// <returns>Help text naming every allowed key and its description.</returns>
    public static string DescribeKeys(IReadOnlyList<RequestKeyDefinition> allowedKeys)
    {
        return string.Join(" ", allowedKeys.Select(key => $"'{key.JsonName}': {key.Description}"));
    }

    /// <summary>
    /// Adds one error per unsupported key captured in the extension data of a request object.
    /// </summary>
    /// <param name="additionalData">Keys captured as extension data, or null when there were none.</param>
    /// <param name="path">JSON path of the request object; empty for the request root.</param>
    /// <param name="allowedKeys">Keys the object accepts, named in the error message.</param>
    /// <param name="result">Error collection the errors are added to.</param>
    /// <remarks>Keys are reported in ordinal order so the same request always yields the same errors.</remarks>
    public static void AddUnsupportedKeyErrors(Dictionary<string, JsonElement>? additionalData,
        string path, IReadOnlyList<RequestKeyDefinition> allowedKeys, RequestValidationErrorResponse result)
    {
        if (additionalData is not { Count: > 0 })
        {
            return;
        }

        string keyHelp = DescribeKeys(allowedKeys);
        string container = string.IsNullOrEmpty(path) ? "the request root" : $"'{path}'";
        foreach (string unsupportedKey in additionalData.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            result.Errors.Add(BuildError(BuildPath(path, unsupportedKey),
                $"'{unsupportedKey}' is not supported by {container}. Valid keys: {keyHelp}"));
        }
    }

    /// <summary>
    /// Builds one validation error.
    /// </summary>
    /// <param name="path">JSON path of the key the error belongs to.</param>
    /// <param name="message">Caller-facing error message.</param>
    /// <returns>The error attributed to the supplied path.</returns>
    public static RequestValidationError BuildError(string path, string message)
    {
        return new RequestValidationError { Path = path, Message = message };
    }

    private static string BuildPath(string parentPath, string key)
    {
        return string.IsNullOrEmpty(parentPath) ? key : $"{parentPath}.{key}";
    }
}
