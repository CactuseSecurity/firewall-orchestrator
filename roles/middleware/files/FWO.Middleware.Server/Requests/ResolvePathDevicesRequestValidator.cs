using FWO.Middleware.Server.Responses;
using FWO.Data;
using FWO.Basics;

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
    /// <param name="configuredAlgorithmId">
    /// Id of the path analysis algorithm configured as default, used when the request names none.
    /// A configured algorithm of none rejects such a request.
    /// </param>
    /// <param name="designatedZoneMatrixId">
    /// Id of the zone matrix configured as default, used when the network zone tree algorithm runs and the
    /// request names no matrix. A value of 0 or below means none is configured and rejects such a request.
    /// </param>
    /// <returns>All detected errors; empty when the request is valid.</returns>
    /// <remarks>
    /// Only the configured values themselves are checked here. Whether the designated zone matrix still
    /// exists and holds zones can only be answered by the API and is checked when the analysis runs.
    /// </remarks>
    public static RequestValidationErrorResponse Validate(
        ResolvePathDevicesRequest? request, long configuredAlgorithmId, int designatedZoneMatrixId)
    {
        RequestValidationErrorResponse result = new();
        if (request == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(ResolvePathDevicesRequestValidationSchema.kRootPath,
                $"{ResolvePathDevicesRequestValidationSchema.kEndpointName} requires a request body. Valid root keys: " +
                ResolvePathDevicesRequestValidationSchema.DescribeKeys(ResolvePathDevicesRequestValidationSchema.RootKeys)));
            return result;
        }
        
        ValidateIpInputs(request, result);
        ValidateOptions(request.Options, configuredAlgorithmId, designatedZoneMatrixId, result);
        ValidateFilter(request.Options.Filter, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, ResolvePathDevicesRequestValidationSchema.kRootPath,
            ResolvePathDevicesRequestValidationSchema.RootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.AdditionalData, ResolvePathDevicesRequestValidationSchema.kOptionsPath,
            ResolvePathDevicesRequestValidationSchema.OptionsKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.Filter?.AdditionalData, ResolvePathDevicesRequestValidationSchema.kFilterPath,
            ResolvePathDevicesRequestValidationSchema.FilterKeys, result);
        return result;
    }

    /// <summary>
    /// Builds the error reported for a well-formed matrix id that names no usable zone matrix.
    /// </summary>
    /// <param name="matrixId">Matrix id the caller supplied.</param>
    /// <returns>An error response attributing the failure to the <c>options.matrixId</c> key.</returns>
    /// <remarks>
    /// Whether the matrix exists and holds zones can only be answered by the API, so this error is raised
    /// when the analysis runs rather than during validation. It is reported in the same shape so that a
    /// caller parses one error contract for every rejected request of this endpoint.
    /// </remarks>
    public static RequestValidationErrorResponse BuildUnknownMatrixError(int matrixId)
    {
        RequestValidationErrorResponse result = new();
        result.Errors.Add(AggregatedRequestKeyValidation.BuildError(ResolvePathDevicesRequestValidationSchema.kMatrixIdPath,
            ResolvePathDevicesRequestValidationSchema.DescribeUnknownMatrix(matrixId)));
        return result;
    }

    /// <summary>
    /// Validates the algorithm and matrix options, including the configured defaults they fall back to.
    /// </summary>
    private static void ValidateOptions(
        ResolvePathDevicesOptions options, long configuredAlgorithmId, int designatedZoneMatrixId, RequestValidationErrorResponse result)
    {
        ValidateAlgorithm(options.Algorithm, configuredAlgorithmId, result);
        ValidateMatrixId(options.MatrixId, options.Algorithm, configuredAlgorithmId, designatedZoneMatrixId, result);
    }

    /// <summary>
    /// Rejects an unsupported algorithm name, and a missing one when the configured default is none.
    /// </summary>
    private static void ValidateAlgorithm(string? algorithm, long configuredAlgorithmId, RequestValidationErrorResponse result)
    {
        string path = ResolvePathDevicesRequestValidationSchema.kAlgorithmPath;
        string allowedNames = string.Join(", ", PathAnalysisAlgorithmNames.Requestable.Select(name => $"'{name}'"));

        if (algorithm == null && configuredAlgorithmId == GlobalConst.kPathAnalysisAlgorithmNone)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path,
                $"No default path analysis algorithm is configured. Specify '{path}' with one of: {allowedNames}."));
        }
        else if (algorithm != null && !PathAnalysisAlgorithmNames.Requestable.Contains(algorithm, StringComparer.OrdinalIgnoreCase))
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path,
                $"'{path}' has the unsupported value '{algorithm}'. Allowed values: {allowedNames}."));
        }
    }

    /// <summary>
    /// Rejects a matrix id that is not positive or not combined with the network zone tree algorithm, and a
    /// missing one when that algorithm runs without a configured designated zone matrix.
    /// </summary>
    private static void ValidateMatrixId(int? matrixId, string? algorithm, long configuredAlgorithmId,
        int designatedZoneMatrixId, RequestValidationErrorResponse result)
    {
        string path = ResolvePathDevicesRequestValidationSchema.kMatrixIdPath;
        if (matrixId == null)
        {
            bool runsNetworkZoneTree = algorithm == null
                ? configuredAlgorithmId == GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree
                : string.Equals(algorithm, PathAnalysisAlgorithmNames.kNetworkZoneTree, StringComparison.OrdinalIgnoreCase);
            if (runsNetworkZoneTree && designatedZoneMatrixId <= 0)
            {
                result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path,
                    $"No default zone matrix is configured. Specify '{path}'."));
            }
            return;
        }

        if (matrixId <= 0)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path, $"'{path}' must be greater than 0."));
        }

        if (!string.Equals(algorithm, PathAnalysisAlgorithmNames.kNetworkZoneTree, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path,
                $"'{path}' is only valid together with '{ResolvePathDevicesRequestValidationSchema.kAlgorithmPath}' " +
                $"set to '{PathAnalysisAlgorithmNames.kNetworkZoneTree}'."));
        }
    }

    /// <summary>
    /// Validates the source and destination lists.
    /// </summary>
    private static void ValidateIpInputs(ResolvePathDevicesRequest request, RequestValidationErrorResponse result)
    {
        ValidateIpBlockList(request.Sources, ResolvePathDevicesRequestValidationSchema.kSourcesPath, result);
        ValidateIpBlockList(request.Destinations, ResolvePathDevicesRequestValidationSchema.kDestinationsPath, result);
    }

    /// <summary>
    /// Rejects a missing, empty or oversized list and validates every entry under its own request path.
    /// </summary>
    private static void ValidateIpBlockList(List<IpRangeOrNetworkRequest?>? ipBlocks, string path, RequestValidationErrorResponse result)
    {
        if (ipBlocks == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path, $"'{path}' is required."));
            return;
        }
        if (ipBlocks.Count == 0)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path, $"'{path}' must not be empty."));
        }
        if  (ipBlocks.Count > ResolvePathDevicesRequestValidationSchema.kMaximumIpBlocksPerList)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(
                path, $"'{path}' must not exceed {ResolvePathDevicesRequestValidationSchema.kMaximumIpBlocksPerList} entries."));
        }
        for (int index = 0; index < ipBlocks.Count; index++)
        {
            IpRangeOrNetworkRequestValidator.Validate(ipBlocks[index], $"{path}[{index}]", result);
        }
    }

    /// <summary>
    /// Rejects filter values that can never match a device: an id that is not positive and a name that is
    /// empty or whitespace. Omitted or null filter keys apply no restriction and are not checked.
    /// </summary>
    private static void ValidateFilter(ResolvePathDevicesFilter? filter, RequestValidationErrorResponse result)
    {
        if (filter == null)
        {
            return;
        }
        if (filter.Id <= 0)
        {
            string path = ResolvePathDevicesRequestValidationSchema.kFilterIdPath;
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path, $"'{path}' must be greater than 0."));
        }
        if (filter.Name != null && string.IsNullOrWhiteSpace(filter.Name))
        {
            string path = ResolvePathDevicesRequestValidationSchema.kFilterNamePath;
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path,
                $"'{path}' must not be empty. Omit it or use null to apply no restriction."));
        }
    }
}
