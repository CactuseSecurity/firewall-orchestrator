using System.Text.RegularExpressions;

namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// HTTP method and path of a firewall write REST call, stored as one setting value in the form "METHOD path",
/// e.g. "POST add-host" or "PUT /api/v2/cmdb/firewall/address/@@OBJECT_NAME@@". The path may contain placeholders.
/// The path is always relative to the management: absolute urls would send the vendor credentials to another host.
/// </summary>
public sealed partial record FirewallWriteEndpoint(string Method, string Path)
{
    private static readonly List<string> kAllowedMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];
    private const char kSeparator = ' ';
    private const string kNetworkPathPrefix = "//";

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]*:")]
    private static partial Regex SchemePattern();

    /// <summary>HTTP methods an endpoint setting may use.</summary>
    public static IReadOnlyList<string> AllowedMethods => kAllowedMethods;

    /// <summary>
    /// Parses a stored endpoint value. Throws an <see cref="ArgumentException"/> if the method is not supported or the path is missing.
    /// </summary>
    public static FirewallWriteEndpoint Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string trimmed = value.Trim();
        int separatorIndex = trimmed.IndexOf(kSeparator);
        if (separatorIndex <= 0)
        {
            throw new ArgumentException($"Endpoint '{value}' must have the form 'METHOD path'.", nameof(value));
        }

        string method = trimmed[..separatorIndex].ToUpperInvariant();
        string path = trimmed[(separatorIndex + 1)..].Trim();
        if (!kAllowedMethods.Contains(method))
        {
            throw new ArgumentException($"Endpoint '{value}' uses the unsupported HTTP method '{method}'.", nameof(value));
        }
        if (path.Length == 0 || path.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"Endpoint '{value}' must have one path without blanks.", nameof(value));
        }
        if (SchemePattern().IsMatch(path) || path.StartsWith(kNetworkPathPrefix, StringComparison.Ordinal) || path.Contains('\\'))
        {
            throw new ArgumentException($"Endpoint '{value}' must have a path relative to the management, not an absolute url.", nameof(value));
        }
        return new FirewallWriteEndpoint(method, path);
    }

    /// <summary>Whether the value is empty (shipped default) or a valid endpoint.</summary>
    public static bool IsValidSetting(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        try
        {
            Parse(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Splits a stored value into method and path for editing them separately, without validating it. A first word that
    /// is no allowed method is kept in the path, so the value is not lost.
    /// </summary>
    public static (string Method, string Path) SplitSetting(string value)
    {
        string trimmed = value.Trim();
        int separatorIndex = trimmed.IndexOf(kSeparator);
        string firstWord = separatorIndex < 0 ? trimmed : trimmed[..separatorIndex];
        string method = firstWord.ToUpperInvariant();
        if (!kAllowedMethods.Contains(method))
        {
            return ("", trimmed);
        }
        return (method, separatorIndex < 0 ? "" : trimmed[(separatorIndex + 1)..].Trim());
    }

    /// <summary>Combines method and path to the stored value; empty if both are empty.</summary>
    public static string CombineSetting(string method, string path) =>
        $"{method.Trim().ToUpperInvariant()}{kSeparator}{path.Trim()}".Trim();

    /// <summary>Returns the stored form "METHOD path".</summary>
    public override string ToString() => $"{Method}{kSeparator}{Path}";
}
