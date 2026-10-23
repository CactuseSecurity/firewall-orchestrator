using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates one <see cref="IpRangeOrNetworkRequest"/> of a request and reports every error under its request path.
/// The address rules themselves live in <see cref="IpRangeOrNetworkParser"/>.
/// </summary>
public static class IpRangeOrNetworkRequestValidator
{
    private static readonly List<RequestKeyDefinition> kKeys =
    [
        new RequestKeyDefinition("ipStart", "First address of an inclusive range. Must be given together with 'ipEnd'; mutually exclusive with 'ipNetwork'."),
        new RequestKeyDefinition("ipEnd", "Last address of an inclusive range. Must be given together with 'ipStart'; mutually exclusive with 'ipNetwork'."),
        new RequestKeyDefinition("ipNetwork", "Network in CIDR notation without host bits, e.g. '10.1.0.0/24'. Mutually exclusive with 'ipStart' and 'ipEnd'.")
    ];

    /// <summary>
    /// Gets the keys an IP address block accepts.
    /// </summary>
    public static IReadOnlyList<RequestKeyDefinition> Keys => kKeys;

    /// <summary>
    /// Adds every error of one IP address block to the result.
    /// </summary>
    /// <param name="ipBlock">The block to validate; null when the caller sent null as list element.</param>
    /// <param name="path">Request path of the block, for example <c>sources[2]</c>.</param>
    /// <param name="result">Error collection the errors are added to.</param>
    public static void Validate(IpRangeOrNetworkRequest? ipBlock, string path, RequestValidationErrorResponse result)
    {
        if (ipBlock == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path,
                $"'{path}' must be an object. Valid keys: {AggregatedRequestKeyValidation.DescribeKeys(kKeys)}"));
            return;
        }

        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(ipBlock.AdditionalData, path, kKeys, result);
        // The return IPAddressRange is not used in the validation
        if (!IpRangeOrNetworkParser.TryParse(ipBlock, out _, out string? error))
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path, $"'{path}' {error}"));
        }
    }
}