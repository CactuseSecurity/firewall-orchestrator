using System.Text;
using System.Text.Json;
using FWO.Data.DeviceSpecificSettings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FWO.Services.DeviceSpecificSettings;

/// <summary>
/// Reads and writes <see cref="DeviceSpecificSettingsPackage"/> files and converts setting values between the package
/// (System.Text.Json) and the API layer (Newtonsoft JSON).
/// </summary>
public static class DeviceSpecificSettingsPackageJson
{
    /// <summary>File name suffix of an exported package.</summary>
    public const string kFileSuffix = ".fwo-dss.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions CompactOptions = new();

    /// <summary>Writes a package as indented JSON.</summary>
    public static string Serialize(DeviceSpecificSettingsPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return System.Text.Json.JsonSerializer.Serialize(package, WriteOptions);
    }

    /// <summary>Size in bytes of the package written as file by <see cref="Serialize"/>.</summary>
    public static long FileSize(DeviceSpecificSettingsPackage package) => Encoding.UTF8.GetByteCount(Serialize(package));

    /// <summary>
    /// Whether the package written as file stays within <see cref="DeviceSpecificSettingsPackage.kMaxPackageBytes"/>, so
    /// that it can be read again by <see cref="Deserialize"/> and sent to the import.
    /// </summary>
    public static bool FitsSizeLimit(DeviceSpecificSettingsPackage package) =>
        FileSize(package) <= DeviceSpecificSettingsPackage.kMaxPackageBytes;

    /// <summary>Reads a package of at most <see cref="DeviceSpecificSettingsPackage.kMaxPackageBytes"/> bytes.</summary>
    /// <exception cref="InvalidDataException">The text is too large, no JSON, or not shaped like a package.</exception>
    public static DeviceSpecificSettingsPackage Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > DeviceSpecificSettingsPackage.kMaxPackageBytes)
        {
            throw new InvalidDataException(
                $"A device specific settings package must not be larger than {DeviceSpecificSettingsPackage.kMaxPackageBytes} bytes.");
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<DeviceSpecificSettingsPackage>(json, CompactOptions)
                ?? throw new InvalidDataException("The device specific settings package is empty.");
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new InvalidDataException($"The device specific settings package is no valid package: {exception.Message}", exception);
        }
    }

    /// <summary>Converts a package value to the JSON representation of the API layer, keeping strings that look like dates as strings.</summary>
    public static JToken ToJToken(JsonElement element)
    {
        using JsonTextReader reader = new(new StringReader(element.GetRawText()))
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal
        };
        return JToken.ReadFrom(reader);
    }

    /// <summary>Converts a value of the API layer to a package value.</summary>
    public static JsonElement ToJsonElement(JToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        using JsonDocument document = JsonDocument.Parse(token.ToString(Formatting.None));
        return document.RootElement.Clone();
    }

    /// <summary>Converts a package to JSON for storing it in a jsonb column.</summary>
    public static JToken ToStorage(DeviceSpecificSettingsPackage package)
    {
        using JsonTextReader reader = new(new StringReader(System.Text.Json.JsonSerializer.Serialize(package, CompactOptions)))
        {
            DateParseHandling = DateParseHandling.None
        };
        return JToken.ReadFrom(reader);
    }

    /// <summary>Reads a package stored in a jsonb column.</summary>
    /// <exception cref="InvalidDataException">The stored JSON is not shaped like a package.</exception>
    public static DeviceSpecificSettingsPackage FromStorage(JToken content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Deserialize(content.ToString(Formatting.None));
    }
}
