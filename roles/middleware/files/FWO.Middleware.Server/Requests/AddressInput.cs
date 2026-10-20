using FWO.Basics;
using System.Net;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Defines the three mutually exclusive address representations accepted by REST requests.
/// </summary>
public interface IAddressInput
{
    /// <summary>
    /// Gets or sets one maskless IPv4 or IPv6 address.
    /// </summary>
    string IpHost { get; set; }

    /// <summary>
    /// Gets or sets one IPv4 or IPv6 CIDR network.
    /// </summary>
    string IpNetwork { get; set; }

    /// <summary>
    /// Gets or sets an inclusive range containing exactly two maskless addresses.
    /// </summary>
    List<string>? IpRange { get; set; }
}

/// <summary>
/// Holds normalized inclusive bounds without adding them to the serialized request contract.
/// </summary>
internal readonly record struct NormalizedAddressBounds(string IpStart, string IpEnd);

/// <summary>
/// Validates REST address inputs and normalizes them to inclusive start and end addresses.
/// </summary>
internal static class AddressInputNormalizer
{
    /// <summary>
    /// Validates exactly one address representation and returns normalized inclusive bounds.
    /// </summary>
    internal static bool TryValidateAndNormalize(
        IAddressInput input,
        string context,
        out NormalizedAddressBounds normalizedBounds,
        out string? errorMessage)
    {
        normalizedBounds = new(string.Empty, string.Empty);
        bool hasHost = !string.IsNullOrEmpty(input.IpHost);
        bool hasNetwork = !string.IsNullOrEmpty(input.IpNetwork);
        bool hasRange = input.IpRange is { Count: > 0 };
        if ((hasHost ? 1 : 0) + (hasNetwork ? 1 : 0) + (hasRange ? 1 : 0) != 1)
        {
            errorMessage = $"{context} must define exactly one non-empty 'ipHost', 'ipNetwork', or 'ipRange'.";
            return false;
        }

        if (hasHost)
        {
            return TryNormalizeHost(input.IpHost, context, out normalizedBounds, out errorMessage);
        }

        if (hasNetwork)
        {
            return TryNormalizeNetwork(input.IpNetwork, context, out normalizedBounds, out errorMessage);
        }

        return TryNormalizeRange(input.IpRange, context, out normalizedBounds, out errorMessage);
    }

    private static bool TryNormalizeHost(
        string value,
        string context,
        out NormalizedAddressBounds normalizedBounds,
        out string? errorMessage)
    {
        normalizedBounds = new(string.Empty, string.Empty);
        if (!TryParseMasklessAddress(value, out IPAddress address))
        {
            errorMessage = $"{context} has an invalid 'ipHost' value.";
            return false;
        }

        string normalizedAddress = address.ToString();
        normalizedBounds = new(normalizedAddress, normalizedAddress);
        errorMessage = null;
        return true;
    }

    private static bool TryNormalizeNetwork(
        string value,
        string context,
        out NormalizedAddressBounds normalizedBounds,
        out string? errorMessage)
    {
        normalizedBounds = new(string.Empty, string.Empty);
        if (!value.Contains('/')
            || !value.TryParseIPString<(IPAddress start, IPAddress end)>(
                out (IPAddress start, IPAddress end) range,
                strictv4Parse: true))
        {
            errorMessage = $"{context} has an invalid 'ipNetwork' value.";
            return false;
        }

        normalizedBounds = new(range.start.ToString(), range.end.ToString());
        errorMessage = null;
        return true;
    }

    private static bool TryNormalizeRange(
        List<string>? values,
        string context,
        out NormalizedAddressBounds normalizedBounds,
        out string? errorMessage)
    {
        normalizedBounds = new(string.Empty, string.Empty);
        if (values is not { Count: 2 })
        {
            errorMessage = $"{context} requires exactly two entries in 'ipRange'.";
            return false;
        }

        if (!TryParseMasklessAddress(values[0], out IPAddress start))
        {
            errorMessage = $"{context} has an invalid 'ipRange[0]' value.";
            return false;
        }

        if (!TryParseMasklessAddress(values[1], out IPAddress end))
        {
            errorMessage = $"{context} has an invalid 'ipRange[1]' value.";
            return false;
        }

        if (IpOperations.CompareIpFamilies(start, end) != 0)
        {
            errorMessage = $"{context} must use the same address family for both 'ipRange' entries.";
            return false;
        }

        if (IpOperations.CompareIpValues(start, end) > 0)
        {
            errorMessage = $"{context} must order 'ipRange' from its lower to its upper address.";
            return false;
        }

        normalizedBounds = new(start.ToString(), end.ToString());
        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Parses one maskless address through the shared FWO parser.
    /// </summary>
    private static bool TryParseMasklessAddress(string? value, out IPAddress address)
    {
        address = IPAddress.None;
        if (string.IsNullOrEmpty(value)
            || value.Contains('/')
            || value.Contains('-')
            || !value.TryParseIPString<(IPAddress start, IPAddress end)>(
                out (IPAddress start, IPAddress end) range,
                strictv4Parse: true)
            || !range.start.Equals(range.end))
        {
            return false;
        }

        address = range.start;
        return true;
    }
}
