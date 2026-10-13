using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates a <see cref="ResolvePathDevicesRequest"/> completely and reports every detected
/// key-level error in one result.
/// </summary>
public static class ResolvePathDevicesRequestValidator
{
    /// <summary>
    /// Validates the whole request without stopping at the first error.
    /// </summary>
    /// <param name="request">Request to validate, or null when no body was supplied.</param>
    /// <returns>All detected errors; empty when the request is valid.</returns>
    public static RequestValidationErrorResponse Validate(ResolvePathDevicesRequest? request)
    {
        RequestValidationErrorResponse result = new();
        if (request == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(ResolvePathDevicesRequestValidationSchema.kRootPath,
                $"{ResolvePathDevicesRequestValidationSchema.kEndpointName} requires a request body. Valid root keys: " +
                ResolvePathDevicesRequestValidationSchema.DescribeKeys(ResolvePathDevicesRequestValidationSchema.RootKeys)));
            return result;
        }
        
        ValidateNoNullInputs(request, result);
        ValidateIpInputs(request, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, ResolvePathDevicesRequestValidationSchema.kRootPath,
            ResolvePathDevicesRequestValidationSchema.RootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.AdditionalData, ResolvePathDevicesRequestValidationSchema.kOptionsPath,
            ResolvePathDevicesRequestValidationSchema.OptionsKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.Filter?.AdditionalData, ResolvePathDevicesRequestValidationSchema.kFilterPath,
            ResolvePathDevicesRequestValidationSchema.FilterKeys, result);
        return result;
    }

    private static void ValidateIpInputs(ResolvePathDevicesRequest request, RequestValidationErrorResponse result)
    {
        ValidateIpBlockList(request.Sources, ResolvePathDevicesRequestValidationSchema.kSourcesPath, result);
        ValidateIpBlockList(request.Destinations, ResolvePathDevicesRequestValidationSchema.kDestinationsPath, result);
    }

    private static void ValidateIpBlockList(List<IpRangeOrNetworkRequest?>? ipBlocks, string path, RequestValidationErrorResponse result)
    {
        if (ipBlocks == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path, $"'{path}' is required."));
            return;
        }
        for (int index = 0; index < ipBlocks.Count; index++)
        {
            IpRangeOrNetworkRequestValidator.Validate(ipBlocks[index], $"{path}[{index}]", result);
        }
    }
}
