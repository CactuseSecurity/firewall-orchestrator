using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json.Serialization;

namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// A stored version of the device specific settings (table <c>device_specific_config_snapshot</c>): a named
/// <see cref="DeviceSpecificSettingsPackage"/> that can be applied later through the import.
/// </summary>
public class DeviceSpecificSettingsSnapshotSummary
{
    /// <summary>Largest length of a snapshot name.</summary>
    public const int kMaxNameLength = 200;

    /// <summary>Largest length of a snapshot description.</summary>
    public const int kMaxDescriptionLength = 2000;

    /// <summary>Database ID.</summary>
    [JsonProperty("id"), JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>Unique name, at most <see cref="kMaxNameLength"/> characters.</summary>
    [JsonProperty("name"), JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Optional description, at most <see cref="kMaxDescriptionLength"/> characters.</summary>
    [JsonProperty("description"), JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>FWO version of the installation that wrote the stored package.</summary>
    [JsonProperty("fwo_version"), JsonPropertyName("fwo_version")]
    public string FwoVersion { get; set; } = "";

    /// <summary>Time the snapshot was stored.</summary>
    [JsonProperty("created"), JsonPropertyName("created")]
    public DateTimeOffset Created { get; set; }
}

/// <summary>A stored snapshot including its package as read from the database.</summary>
public sealed class DeviceSpecificSettingsSnapshotData : DeviceSpecificSettingsSnapshotSummary
{
    /// <summary>The stored package as JSON.</summary>
    [JsonProperty("content"), System.Text.Json.Serialization.JsonIgnore]
    public JToken Content { get; set; } = JValue.CreateNull();
}
