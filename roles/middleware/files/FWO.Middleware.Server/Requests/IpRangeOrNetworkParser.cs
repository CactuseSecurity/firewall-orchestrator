using NetTools;
using System.Net;
using System.Net.Sockets;
using FWO.Basics;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Parses an <see cref="IpRangeOrNetworkRequest"/> into an <see cref="IPAddressRange"/> using the rules of the
/// request contract: either an inclusive range (<c>ipStart</c> and <c>ipEnd</c>) or a CIDR network
/// (<c>ipNetwork</c>), never both. Shared by request validators, which report the error, and services, which
/// use the range, so that both apply the same rules.
/// </summary>
/// <remarks>
/// Error messages start with a verb and carry no subject; the caller prefixes the request path of the
/// object, for example <c>'sources[2]'</c>.
/// </remarks>
public static class IpRangeOrNetworkParser
{
    private const int kIpv4HostPrefixLength = 32;
    private const int kIpv6HostPrefixLength = 128;
    private const int kBitsPerByte = 8;
    private const int kIpv4CompatiblePrefixByteCount = 12;
    private const uint kIpv6LoopbackSuffix = 1;

    /// <summary>
    /// Parses one IP address block of a request.
    /// </summary>
    /// <param name="ipBlock">The address block as supplied by the caller.</param>
    /// <param name="range">The inclusive address range of the block; null when parsing failed.</param>
    /// <param name="error">Why the block is invalid, without a subject; null when parsing succeeded.</param>
    /// <returns>True when the block is a valid range or network.</returns>
    public static bool TryParse(IpRangeOrNetworkRequest ipBlock, out IPAddressRange? range, out string? error)
    {
        bool hasStart = !string.IsNullOrWhiteSpace(ipBlock.IpStart);
        bool hasEnd = !string.IsNullOrWhiteSpace(ipBlock.IpEnd);
        bool hasNetwork = !string.IsNullOrWhiteSpace(ipBlock.IpNetwork);

        return (hasStart, hasEnd, hasNetwork) switch
        {
            (true, true, false)  => TryParseRange(ipBlock.IpStart, ipBlock.IpEnd, out range, out error),
            (false, false, true) => TryParseNetwork(ipBlock.IpNetwork, out range, out error),
            (false, false, false) => Fail("requires either 'ipStart' and 'ipEnd' or 'ipNetwork'.", out range, out error),
            (_, _, true)          => Fail("must not combine 'ipNetwork' with 'ipStart' or 'ipEnd'.", out range, out error),
            _                     => Fail("requires both 'ipStart' and 'ipEnd'.", out range, out error)
        };
    }

    /// <summary>
    /// Parses an inclusive range given by its first and last address.
    /// </summary>
    private static bool TryParseRange(string? start, string? end, out IPAddressRange? range, out string? error)
    {
        (bool isValid, error) = ValidateIpRange(start ?? "", end ?? "", out string normalizedStart, out string normalizedEnd);
        range = isValid ? new IPAddressRange(IPAddress.Parse(normalizedStart), IPAddress.Parse(normalizedEnd)) : null;
        return isValid;
    }

    /// <summary>
    /// Parses a network given in CIDR notation into the range of all its addresses.
    /// </summary>
    private static bool TryParseNetwork(string? network, out IPAddressRange? range, out string? error)
    {
        bool isValid = TryNormalizeIpNetwork(network ?? "", out string normalizedStart, out string normalizedEnd, out error);
        range = isValid ? new IPAddressRange(IPAddress.Parse(normalizedStart), IPAddress.Parse(normalizedEnd)) : null;
        return isValid;
    }

    /// <summary>
    /// Reports a failed parse with the given reason.
    /// </summary>
    private static bool Fail(string message, out IPAddressRange? range, out string? error)
    {
        range = null;
        error = message;
        return false;
    }

    /// <summary>
    /// Validates both range bounds, strips an allowed host mask from each and checks the resulting pair.
    /// </summary>
    private static (bool IsValid, string? ErrorMessage) ValidateIpRange(
        string ipStartValue,
        string ipEndValue,
        out string normalizedIpStart,
        out string normalizedIpEnd)
    {
        if (!TryValidateIpRangeBound(ipStartValue, "ipStart", out normalizedIpStart, out string? ipStartMaskError))
        {
            normalizedIpEnd = string.Empty;
            return (false, ipStartMaskError);
        }

        if (!TryValidateIpRangeBound(ipEndValue, "ipEnd", out normalizedIpEnd, out string? ipEndMaskError))
        {
            return (false, ipEndMaskError);
        }

        return ValidateNormalizedIpRange(normalizedIpStart, normalizedIpEnd);
    }

    /// <summary>
    /// Checks two mask-free range bounds: both parse, neither re-encodes IPv4 as IPv6, both share an address
    /// family, and the start is not greater than the end.
    /// </summary>
    private static (bool IsValid, string? ErrorMessage) ValidateNormalizedIpRange(
        string normalizedIpStart,
        string normalizedIpEnd)
    {
        if (!IPAddress.TryParse(normalizedIpStart, out IPAddress? ipStart))
        {
            return (false, "has an invalid 'ipStart' value.");
        }

        if (!IPAddress.TryParse(normalizedIpEnd, out IPAddress? ipEnd))
        {
            return (false, "has an invalid 'ipEnd' value.");
        }

        if (IsIpv4EncodedAsIpv6(ipStart))
        {
            return (false, BuildIpv4EncodedAsIpv6Error(ipStart, "ipStart"));
        }

        if (IsIpv4EncodedAsIpv6(ipEnd))
        {
            return (false, BuildIpv4EncodedAsIpv6Error(ipEnd, "ipEnd"));
        }

        if (ipStart.AddressFamily != ipEnd.AddressFamily)
        {
            return (false, "must use the same address family for 'ipStart' and 'ipEnd'.");
        }

        if (IpOperations.CompareIpValues(ipStart, ipEnd) > 0)
        {
            return (false, "must satisfy 'ipStart' <= 'ipEnd'.");
        }

        return (true, null);
    }

    /// <summary>
    /// True for IPv6 values that only re-encode an IPv4 address: the IPv4-mapped form and the deprecated
    /// IPv4-compatible form of RFC 4291 section 2.5.5.1. Both are classified as IPv6 downstream and could
    /// therefore never match an IPv4 zone or range, so they are rejected instead of silently evaluated.
    /// </summary>
    /// <param name="address">Address to classify.</param>
    /// <returns>True when the address is an IPv4 address written in IPv6 notation.</returns>

    private static bool IsIpv4EncodedAsIpv6(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            return true;
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        byte[] addressBytes = address.GetAddressBytes();
        for (int index = 0; index < kIpv4CompatiblePrefixByteCount; index++)
        {
            if (addressBytes[index] != 0)
            {
                return false;
            }
        }

        uint embeddedIpv4 = 0;
        for (int index = kIpv4CompatiblePrefixByteCount; index < addressBytes.Length; index++)
        {
            embeddedIpv4 = (embeddedIpv4 << kBitsPerByte) | addressBytes[index];
        }

        // The unspecified address and the IPv6 loopback share the leading zero bytes but denote no IPv4 address.
        return embeddedIpv4 > kIpv6LoopbackSuffix;
    }

    /// <summary>
    /// Builds the rejection message for an IPv6 value that only re-encodes an IPv4 address.
    /// </summary>
    /// <param name="address">The rejected address.</param>
    /// <param name="fieldName">Name of the request field carrying the address.</param>
    /// <returns>The error message naming the notation that was used.</returns>
    private static string BuildIpv4EncodedAsIpv6Error(IPAddress address, string fieldName)
    {
        string notation = address.IsIPv4MappedToIPv6 ? "IPv4-mapped" : "IPv4-compatible";
        return $"has an {notation} IPv6 value in '{fieldName}'. Use the dotted IPv4 form instead.";
    }

    /// <summary>
    /// Accepts a range bound with no mask or with the host mask of its address family and strips the mask.
    /// Every broader mask is rejected and pointed at 'ipNetwork'.
    /// </summary>
    private static bool TryValidateIpRangeBound(
        string ipAddressValue,
        string fieldName,
        out string normalizedIpAddress,
        out string? errorMessage)
    {
        int maskSeparatorIndex = ipAddressValue.IndexOf('/');
        if (maskSeparatorIndex < 0)
        {
            normalizedIpAddress = ipAddressValue;
            errorMessage = null;
            return true;
        }

        string address = ipAddressValue[..maskSeparatorIndex];
        string mask = ipAddressValue[(maskSeparatorIndex + 1)..];
        if (!IPAddress.TryParse(address, out IPAddress? parsedAddress))
        {
            // The address itself is invalid; let the range validation report the value rather than the mask.
            normalizedIpAddress = address;
            errorMessage = null;
            return true;
        }

        if (IsIpv4EncodedAsIpv6(parsedAddress))
        {
            normalizedIpAddress = string.Empty;
            errorMessage = BuildIpv4EncodedAsIpv6Error(parsedAddress, fieldName);
            return false;
        }

        int hostPrefixLength = GetHostPrefixLength(parsedAddress.AddressFamily);
        if (!int.TryParse(mask, out int prefixLength) || prefixLength != hostPrefixLength)
        {
            normalizedIpAddress = string.Empty;
            errorMessage = $"has unsupported netmask '/{mask}' in '{fieldName}'. Only '/{hostPrefixLength}' is allowed; use 'ipNetwork' for networks.";
            return false;
        }

        normalizedIpAddress = address;
        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Parses a CIDR network and returns the inclusive bounds of the addressed block.
    /// Networks carrying host bits are rejected so that no request is silently widened.
    /// </summary>
    private static bool TryNormalizeIpNetwork(string ipNetwork, out string normalizedIpStart, out string normalizedIpEnd, out string? errorMessage)
    {
        normalizedIpStart = string.Empty;
        normalizedIpEnd = string.Empty;

        int maskSeparatorIndex = ipNetwork.IndexOf('/');
        if (maskSeparatorIndex <= 0 || maskSeparatorIndex != ipNetwork.LastIndexOf('/'))
        {
            errorMessage = "requires a valid CIDR network in 'ipNetwork'.";
            return false;
        }

        if (!IPAddress.TryParse(ipNetwork[..maskSeparatorIndex], out IPAddress? parsedAddress)
            || !int.TryParse(ipNetwork[(maskSeparatorIndex + 1)..], out int prefixLength))
        {
            errorMessage = "has an invalid 'ipNetwork' value.";
            return false;
        }

        if (IsIpv4EncodedAsIpv6(parsedAddress))
        {
            errorMessage = BuildIpv4EncodedAsIpv6Error(parsedAddress, "ipNetwork");
            return false;
        }

        int hostPrefixLength = GetHostPrefixLength(parsedAddress.AddressFamily);
        if (prefixLength < 0 || prefixLength > hostPrefixLength)
        {
            errorMessage = "has an invalid CIDR prefix in 'ipNetwork'.";
            return false;
        }

        (IPAddress networkAddress, IPAddress lastAddress) = GetNetworkBounds(parsedAddress, prefixLength);
        if (!networkAddress.Equals(parsedAddress))
        {
            errorMessage = $"must not set host bits in 'ipNetwork'. Use '{networkAddress}/{prefixLength}' to evaluate that network.";
            return false;
        }

        normalizedIpStart = networkAddress.ToString();
        normalizedIpEnd = lastAddress.ToString();
        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Computes the first and last address of a CIDR block from an already parsed address and prefix length.
    /// </summary>
    private static (IPAddress NetworkAddress, IPAddress LastAddress) GetNetworkBounds(IPAddress address, int prefixLength)
    {
        byte[] networkBytes = address.GetAddressBytes();
        byte[] lastBytes = address.GetAddressBytes();

        for (int byteIndex = 0; byteIndex < networkBytes.Length; byteIndex++)
        {
            int significantBits = Math.Clamp(prefixLength - (byteIndex * kBitsPerByte), 0, kBitsPerByte);
            byte mask = significantBits == 0 ? (byte)0 : (byte)(byte.MaxValue << (kBitsPerByte - significantBits));
            networkBytes[byteIndex] &= mask;
            lastBytes[byteIndex] |= (byte)~mask;
        }

        return (new IPAddress(networkBytes), new IPAddress(lastBytes));
    }

    /// <summary>
    /// Returns the prefix length that addresses a single host in the given address family.
    /// </summary>
    private static int GetHostPrefixLength(AddressFamily addressFamily)
    {
        return addressFamily == AddressFamily.InterNetwork ? kIpv4HostPrefixLength : kIpv6HostPrefixLength;
    }
}
