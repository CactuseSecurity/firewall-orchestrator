using FWO.Basics;
using System.Net;
using System.Net.Sockets;

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
    /// Gets or sets one canonical IPv4 or IPv6 CIDR network.
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
    private const int BitsPerByte = 8;
    private const int Ipv4CompatiblePrefixByteCount = 12;
    private const uint Ipv6LoopbackSuffix = 1;

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
        if (!TryParseAddress(value, "ipHost", out IPAddress? address, out string? detail))
        {
            errorMessage = $"{context} {detail}";
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
        int separatorIndex = value.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex != value.LastIndexOf('/'))
        {
            errorMessage = $"{context} requires a canonical CIDR value in 'ipNetwork'.";
            return false;
        }

        string prefixText = value[(separatorIndex + 1)..];
        if (prefixText.Length == 0 || prefixText.Any(character => !char.IsAsciiDigit(character)))
        {
            errorMessage = $"{context} has an invalid CIDR prefix in 'ipNetwork'.";
            return false;
        }

        if (!TryParseAddress(value[..separatorIndex], "ipNetwork", out IPAddress? address, out string? detail))
        {
            errorMessage = $"{context} {detail}";
            return false;
        }

        if (!value.TryParseIpAddressAndPrefix(out _, out int? prefixLength) || !prefixLength.HasValue)
        {
            if (int.TryParse(prefixText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsedPrefix)
                && IsValidPrefix(address, parsedPrefix))
            {
                (IPAddress networkAddress, _) = GetNetworkBounds(address, parsedPrefix);
                errorMessage = $"{context} must not set host bits in 'ipNetwork'. Use '{networkAddress}/{parsedPrefix}' to evaluate that network.";
                return false;
            }

            errorMessage = $"{context} has an invalid CIDR prefix in 'ipNetwork'.";
            return false;
        }

        IpOperations.TryGetNetworkRange(address, prefixLength.Value, out (IPAddress start, IPAddress end) range);
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

        if (!TryParseAddress(values[0], "ipRange[0]", out IPAddress? start, out string? startDetail))
        {
            errorMessage = $"{context} {startDetail}";
            return false;
        }

        if (!TryParseAddress(values[1], "ipRange[1]", out IPAddress? end, out string? endDetail))
        {
            errorMessage = $"{context} {endDetail}";
            return false;
        }

        if (start.AddressFamily != end.AddressFamily)
        {
            errorMessage = $"{context} must use the same address family for both 'ipRange' entries.";
            return false;
        }

        if (CompareAddresses(start, end) > 0)
        {
            errorMessage = $"{context} must order 'ipRange' from its lower to its upper address.";
            return false;
        }

        normalizedBounds = new(start.ToString(), end.ToString());
        errorMessage = null;
        return true;
    }

    private static bool TryParseAddress(string? value, string fieldName, out IPAddress address, out string? errorMessage)
    {
        address = IPAddress.None;
        if (!IpOperations.TryParseIpAddress(value, out IPAddress? parsedAddress))
        {
            errorMessage = $"has an invalid '{fieldName}' value.";
            return false;
        }

        if (IsIpv4EncodedAsIpv6(parsedAddress))
        {
            string notation = parsedAddress.IsIPv4MappedToIPv6 ? "IPv4-mapped" : "IPv4-compatible";
            errorMessage = $"has an {notation} IPv6 value in '{fieldName}'. Use the dotted IPv4 form instead.";
            return false;
        }

        address = parsedAddress;
        errorMessage = null;
        return true;
    }

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

        byte[] bytes = address.GetAddressBytes();
        if (bytes.Take(Ipv4CompatiblePrefixByteCount).Any(value => value != 0))
        {
            return false;
        }

        uint embeddedIpv4 = 0;
        foreach (byte value in bytes.Skip(Ipv4CompatiblePrefixByteCount))
        {
            embeddedIpv4 = (embeddedIpv4 << BitsPerByte) | value;
        }

        return embeddedIpv4 > Ipv6LoopbackSuffix;
    }

    private static bool IsValidPrefix(IPAddress address, int prefixLength)
    {
        int maximumPrefix = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        return prefixLength >= 0 && prefixLength <= maximumPrefix;
    }

    private static (IPAddress NetworkAddress, IPAddress LastAddress) GetNetworkBounds(IPAddress address, int prefixLength)
    {
        byte[] networkBytes = address.GetAddressBytes();
        byte[] lastBytes = address.GetAddressBytes();
        for (int byteIndex = 0; byteIndex < networkBytes.Length; byteIndex++)
        {
            int significantBits = Math.Clamp(prefixLength - byteIndex * BitsPerByte, 0, BitsPerByte);
            byte mask = significantBits == 0 ? (byte)0 : (byte)(byte.MaxValue << (BitsPerByte - significantBits));
            networkBytes[byteIndex] &= mask;
            lastBytes[byteIndex] |= (byte)~mask;
        }

        return (new IPAddress(networkBytes), new IPAddress(lastBytes));
    }

    private static int CompareAddresses(IPAddress left, IPAddress right)
    {
        return left.GetAddressBytes().AsSpan().SequenceCompareTo(right.GetAddressBytes());
    }
}
