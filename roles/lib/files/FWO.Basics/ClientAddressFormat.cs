using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace FWO.Basics;

/// <summary>
/// Text form of a client address as it is passed from the UI to the middleware and written to the logs.
/// </summary>
public static class ClientAddressFormat
{
    /// <summary>
    /// Longest accepted address text: an IPv6 address with an embedded IPv4 address.
    /// </summary>
    public const int kMaxLength = 45;

    /// <summary>
    /// Text logged in place of an address that is not known.
    /// </summary>
    public const string kUnknown = "unknown";

    /// <summary>
    /// Maps an IPv4-mapped IPv6 address to IPv4 and drops the scope id, so one client has one notation.
    /// </summary>
    /// <param name="address">Address to normalize.</param>
    /// <returns>The normalized address.</returns>
    public static IPAddress Normalize(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4();
        }

        bool scoped = address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0;
        return scoped ? new IPAddress(address.GetAddressBytes()) : address;
    }

    /// <summary>
    /// Returns the canonical text of an address, as accepted by <see cref="TryParse"/>.
    /// </summary>
    /// <param name="address">Address to format; null if it is not known.</param>
    /// <returns>The address text, or an empty string for an unknown address.</returns>
    public static string ToText(IPAddress? address)
    {
        return address == null ? "" : Normalize(address).ToString();
    }

    /// <summary>
    /// Returns the address text for a log message.
    /// </summary>
    /// <param name="address">Address to describe; null if it is not known.</param>
    /// <returns>The address text, or <see cref="kUnknown"/>.</returns>
    public static string Describe(IPAddress? address)
    {
        return address == null ? kUnknown : ToText(address);
    }

    /// <summary>
    /// Parses exactly one IPv4 or IPv6 address in its canonical notation. Lists, ports, brackets, scope ids,
    /// surrounding white space and shortened or zero-padded notations are refused.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="address">The normalized address if the text is valid; otherwise null.</param>
    /// <returns>True if the text is a single canonical address.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out IPAddress? address)
    {
        address = null;
        if (string.IsNullOrEmpty(text) || text.Length > kMaxLength || !IPAddress.TryParse(text, out IPAddress? parsed))
        {
            return false;
        }

        // the parser also accepts notations such as "1" or "127.1"; only the notation it writes itself is accepted
        bool canonical = string.Equals(parsed.ToString(), text, StringComparison.OrdinalIgnoreCase);
        bool scoped = parsed.AddressFamily == AddressFamily.InterNetworkV6 && parsed.ScopeId != 0;
        if (!canonical || scoped)
        {
            return false;
        }

        address = Normalize(parsed);
        return true;
    }
}
