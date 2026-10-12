using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates a <see cref="GetImplTaskTargetsRequest"/> completely and reports every detected key-level error in
/// one result.
/// </summary>
public static class GetImplTaskTargetsRequestValidator
{
    /// <summary>
    /// Validates the whole request without stopping at the first error.
    /// </summary>
    /// <param name="request">Request to validate, or null when no body was supplied.</param>
    /// <returns>All detected errors; empty when the request is valid.</returns>
    public static RequestValidationErrorResponse Validate(GetImplTaskTargetsRequest? request)
    {
        RequestValidationErrorResponse result = new();
        if (request == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetImplTaskTargetsValidationSchema.kRootPath,
                $"{GetImplTaskTargetsValidationSchema.kEndpointName} requires a request body. Valid root keys: " +
                GetImplTaskTargetsValidationSchema.DescribeKeys(GetImplTaskTargetsValidationSchema.RootKeys)));
            return result;
        }

        ValidateManagementId(request, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, GetImplTaskTargetsValidationSchema.kRootPath,
            GetImplTaskTargetsValidationSchema.RootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.AdditionalData, GetImplTaskTargetsValidationSchema.kOptionsPath,
            GetImplTaskTargetsValidationSchema.OptionsKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.Filter?.AdditionalData, GetImplTaskTargetsValidationSchema.kFilterPath,
            GetImplTaskTargetsValidationSchema.FilterKeys, result);
        return result;
    }

    private static void ValidateManagementId(GetImplTaskTargetsRequest request, RequestValidationErrorResponse result)
    {
        if (request.ManagementId == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetImplTaskTargetsValidationSchema.kManagementIdPath,
                $"'{GetImplTaskTargetsValidationSchema.kManagementIdPath}' is required."));
        }
        else if (request.ManagementId <= 0)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetImplTaskTargetsValidationSchema.kManagementIdPath,
                $"'{GetImplTaskTargetsValidationSchema.kManagementIdPath}' must be greater than 0."));
        }
    }
}
