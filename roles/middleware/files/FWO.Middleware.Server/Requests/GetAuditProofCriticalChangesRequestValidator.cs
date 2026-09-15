using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates a <see cref="GetAuditProofCriticalChangesRequest"/> completely and reports every detected
/// key-level error in one result.
/// </summary>
public static class GetAuditProofCriticalChangesRequestValidator
{
    /// <summary>
    /// Validates the whole request without stopping at the first error.
    /// </summary>
    /// <param name="request">Request to validate, or null when no body was supplied.</param>
    /// <returns>All detected errors; empty when the request is valid.</returns>
    public static RequestValidationErrorResponse Validate(GetAuditProofCriticalChangesRequest? request)
    {
        RequestValidationErrorResponse result = new();
        if (request == null)
        {
            result.Errors.Add(BuildError(GetAuditProofCriticalChangesValidationSchema.kRootPath,
                $"{GetAuditProofCriticalChangesValidationSchema.kEndpointName} requires a request body. Valid root keys: " +
                GetAuditProofCriticalChangesValidationSchema.DescribeKeys(GetAuditProofCriticalChangesValidationSchema.RootKeys)));
            return result;
        }

        ValidateTicketId(request, result);
        AddUnsupportedKeyErrors(request.AdditionalData, GetAuditProofCriticalChangesValidationSchema.kRootPath,
            GetAuditProofCriticalChangesValidationSchema.RootKeys, result);
        AddUnsupportedKeyErrors(request.Options.AdditionalData, GetAuditProofCriticalChangesValidationSchema.kOptionsPath,
            GetAuditProofCriticalChangesValidationSchema.OptionsKeys, result);
        AddUnsupportedKeyErrors(request.Options.Filter?.AdditionalData, GetAuditProofCriticalChangesValidationSchema.kFilterPath,
            GetAuditProofCriticalChangesValidationSchema.FilterKeys, result);
        return result;
    }

    private static void ValidateTicketId(GetAuditProofCriticalChangesRequest request, RequestValidationErrorResponse result)
    {
        if (request.TicketId == null)
        {
            result.Errors.Add(BuildError("ticketId", "'ticketId' is required."));
        }
        else if (request.TicketId <= 0)
        {
            result.Errors.Add(BuildError("ticketId", "'ticketId' must be greater than 0."));
        }
    }

    private static void AddUnsupportedKeyErrors(Dictionary<string, System.Text.Json.JsonElement>? additionalData,
        string path, IReadOnlyList<RequestKeyDefinition> allowedKeys, RequestValidationErrorResponse result)
    {
        if (additionalData is not { Count: > 0 })
        {
            return;
        }

        string keyHelp = GetAuditProofCriticalChangesValidationSchema.DescribeKeys(allowedKeys);
        string container = string.IsNullOrEmpty(path) ? "the request root" : $"'{path}'";
        foreach (string unsupportedKey in additionalData.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            result.Errors.Add(BuildError(BuildPath(path, unsupportedKey),
                $"'{unsupportedKey}' is not supported by {container}. Valid keys: {keyHelp}"));
        }
    }

    private static string BuildPath(string parentPath, string key)
    {
        return string.IsNullOrEmpty(parentPath) ? key : $"{parentPath}.{key}";
    }

    private static RequestValidationError BuildError(string path, string message)
    {
        return new RequestValidationError { Path = path, Message = message };
    }
}
