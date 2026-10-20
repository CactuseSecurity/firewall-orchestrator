using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates a <see cref="GetAutoPlanSettingsRequest"/> completely and reports every detected key-level error in
/// one result.
/// </summary>
public static class GetAutoPlanSettingsRequestValidator
{
    /// <summary>
    /// Validates the whole request without stopping at the first error.
    /// </summary>
    /// <param name="request">Request to validate, or null when no body was supplied.</param>
    /// <returns>All detected errors; empty when the request is valid.</returns>
    public static RequestValidationErrorResponse Validate(GetAutoPlanSettingsRequest? request)
    {
        RequestValidationErrorResponse result = new();
        if (request == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetAutoPlanSettingsValidationSchema.kRootPath,
                $"{GetAutoPlanSettingsValidationSchema.kEndpointName} requires a request body. Valid root keys: " +
                GetAutoPlanSettingsValidationSchema.DescribeKeys(GetAutoPlanSettingsValidationSchema.RootKeys)));
            return result;
        }

        ValidateIds(request.DeviceIds, GetAutoPlanSettingsValidationSchema.kDeviceIdsPath, result);
        ValidateIds(request.ManagementIds, GetAutoPlanSettingsValidationSchema.kManagementIdsPath, result);
        ValidatePaging(request.Options, result);
        ValidateScopeType(request.Options.Filter, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, GetAutoPlanSettingsValidationSchema.kRootPath,
            GetAutoPlanSettingsValidationSchema.RootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.AdditionalData, GetAutoPlanSettingsValidationSchema.kOptionsPath,
            GetAutoPlanSettingsValidationSchema.OptionsKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.Filter?.AdditionalData, GetAutoPlanSettingsValidationSchema.kFilterPath,
            GetAutoPlanSettingsValidationSchema.FilterKeys, result);
        return result;
    }

    private static void ValidateIds(List<int> ids, string path, RequestValidationErrorResponse result)
    {
        if (ids.Count > GetAutoPlanSettingsRequest.kMaxIds)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path,
                $"'{path}' must not contain more than {GetAutoPlanSettingsRequest.kMaxIds} ids."));
        }
        for (int index = 0; index < ids.Count; index++)
        {
            if (ids[index] <= 0)
            {
                string elementPath = $"{path}[{index}]";
                result.Errors.Add(AggregatedRequestKeyValidation.BuildError(elementPath, $"'{elementPath}' must be greater than 0."));
            }
        }
    }

    private static void ValidatePaging(GetAutoPlanSettingsOptions options, RequestValidationErrorResponse result)
    {
        if (options.Limit is < 1 or > GetAutoPlanSettingsOptions.kMaxLimit)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetAutoPlanSettingsValidationSchema.kLimitPath,
                $"'{GetAutoPlanSettingsValidationSchema.kLimitPath}' must be between 1 and {GetAutoPlanSettingsOptions.kMaxLimit} when supplied."));
        }
        if (options.Offset is < 0)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetAutoPlanSettingsValidationSchema.kOffsetPath,
                $"'{GetAutoPlanSettingsValidationSchema.kOffsetPath}' must not be negative when supplied."));
        }
    }

    private static void ValidateScopeType(AutoPlanSettingsFilter? filter, RequestValidationErrorResponse result)
    {
        if (filter?.ScopeType != null && !GetAutoPlanSettingsValidationSchema.ScopeTypes.Contains(filter.ScopeType))
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetAutoPlanSettingsValidationSchema.kFilterScopeTypePath,
                $"'{GetAutoPlanSettingsValidationSchema.kFilterScopeTypePath}' must be one of {string.Join(", ", GetAutoPlanSettingsValidationSchema.ScopeTypes)}."));
        }
    }
}
