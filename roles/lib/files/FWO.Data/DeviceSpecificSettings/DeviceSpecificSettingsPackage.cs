using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// Portable export of the device specific settings: one entry per persisted level with its overrides. Levels are
/// addressed by natural keys (device type name and version, management name, management and gateway name) instead of
/// database IDs, which differ between installations. Settings referencing other objects hold the object's name.
/// Secrets are never part of a package. Unknown properties are rejected, so that a misspelt name is not ignored.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class DeviceSpecificSettingsPackage
{
    /// <summary>Format identifier of a device specific settings package.</summary>
    public const string kFormat = "fwo-device-specific-settings";

    /// <summary>Format version written by this release.</summary>
    public const int kCurrentVersion = 1;

    /// <summary>Largest number of levels a package may contain.</summary>
    public const int kMaxNodes = 10000;

    /// <summary>Largest size of a package file in bytes (1 MiB).</summary>
    public const long kMaxPackageBytes = 1024 * 1024;

    /// <summary>Format identifier, always <see cref="kFormat"/>.</summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = kFormat;

    /// <summary>Format version; this release reads version <see cref="kCurrentVersion"/>.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = kCurrentVersion;

    /// <summary>FWO version of the installation that wrote the package; informational only.</summary>
    [JsonPropertyName("fwo_version")]
    public string FwoVersion { get; set; } = "";

    /// <summary>Time the package was written; informational only.</summary>
    [JsonPropertyName("exported_at")]
    public DateTimeOffset? ExportedAt { get; set; }

    /// <summary>The levels with their overrides, at most <see cref="kMaxNodes"/>.</summary>
    [JsonPropertyName("nodes")]
    public List<DeviceSpecificSettingsPackageNode> Nodes { get; set; } = [];
}

/// <summary>
/// One level of a <see cref="DeviceSpecificSettingsPackage"/> and its overrides. Which name fields are required
/// depends on the level: none for <c>global</c>, device type name and version for <c>device_type</c>, the management name
/// for <c>management</c>, management and gateway name for <c>gateway</c>.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class DeviceSpecificSettingsPackageNode
{
    /// <summary>Level of the entry: <c>global</c>, <c>device_type</c>, <c>management</c> or <c>gateway</c>.</summary>
    [JsonPropertyName("level")]
    public string Level { get; set; } = "";

    /// <summary>Name of the device type; only for level <c>device_type</c>.</summary>
    [JsonPropertyName("device_type_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceTypeName { get; set; }

    /// <summary>Version of the device type; only for level <c>device_type</c>.</summary>
    [JsonPropertyName("device_type_version"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceTypeVersion { get; set; }

    /// <summary>Name of the management; for levels <c>management</c> and <c>gateway</c>.</summary>
    [JsonPropertyName("management_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ManagementName { get; set; }

    /// <summary>Name of the gateway within its management; only for level <c>gateway</c>.</summary>
    [JsonPropertyName("gateway_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GatewayName { get; set; }

    /// <summary>
    /// Overrides of the level by setting key, with the values as stored in the database, except for settings that
    /// reference other objects, which hold the object's name (e.g. <c>scCredential</c> the credential name).
    /// </summary>
    [JsonPropertyName("values")]
    public Dictionary<string, JsonElement> Values { get; set; } = [];
}
