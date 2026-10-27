namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// HTTP method and path of a firewall write REST call, stored as one setting value in the form "METHOD path",
/// e.g. "POST add-host" or "PUT /api/v2/cmdb/firewall/address/@@OBJECT_NAME@@". The path may contain placeholders.
/// </summary>
public sealed record FirewallWriteEndpoint(string Method, string Path)
{
    private static readonly List<string> kAllowedMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];
    private const char kSeparator = ' ';

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

    /// <summary>Returns the stored form "METHOD path".</summary>
    public override string ToString() => $"{Method}{kSeparator}{Path}";
}
