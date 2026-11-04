using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates one <see cref="AddressInputRequest"/> of a request and reports every error under its request path.
/// The address rules themselves live in <see cref="AddressInputNormalizer"/>.
/// </summary>
public static class AddressInputRequestValidator
{
    private static readonly List<RequestKeyDefinition> kKeys =
    [
        new RequestKeyDefinition("ipHost",
            "IPv4 or IPv6 address without a mask, for example '10.1.1.5'. Mutually exclusive with 'ipNetwork' and 'ipRange'."),
        new RequestKeyDefinition("ipNetwork",
            "IPv4 or IPv6 network in CIDR notation, for example '10.1.0.0/24'. Host bits are cleared, so '10.1.1.5/24' covers '10.1.1.0' to '10.1.1.255'. Mutually exclusive with 'ipHost' and 'ipRange'."),
        new RequestKeyDefinition("ipRange",
            "Inclusive range as exactly two addresses without a mask, lower bound first, both of the same address family, for example ['10.1.1.10', '10.1.1.20']. Mutually exclusive with 'ipHost' and 'ipNetwork'.")
    ];

    /// <summary>
    /// Gets the keys an address block accepts, with the description reported in validation errors.
    /// </summary>
    public static IReadOnlyList<RequestKeyDefinition> Keys => kKeys;

    /// <summary>
    /// Adds every error of one address block to the result: a null entry, unsupported keys, and the address rules of <see cref="AddressInputNormalizer"/>.
    /// </summary>
    /// <param name="address">The block to validate; null when the caller sent null as a list element.</param>
    /// <param name="path">Request path of the block, for example <c>sources[2]</c>.</param>
    /// <param name="result">Error collection the errors are added to.</param>
    public static void Validate(AddressInputRequest? address, string path, RequestValidationErrorResponse result)
    {
        if (address == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path,
                $"'{path}' must be an object with exactly one of the following keys: {AggregatedRequestKeyValidation.DescribeKeys(kKeys)}"));
            return;
        }

        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(address.AdditionalData, path, kKeys, result);
        // Only the verdict is needed here; the service normalizes again and builds the ranges.
        if (!AddressInputNormalizer.TryValidateAndNormalize(address, $"'{path}'", out _, out string? error))
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path, error!));
        }
    }
}
