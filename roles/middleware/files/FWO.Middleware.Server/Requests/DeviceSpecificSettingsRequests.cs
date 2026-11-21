using System.Text.Json;
using System.Text.Json.Serialization;
using FWO.Data.DeviceSpecificSettings;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Request of <c>POST /api/DeviceSpecificSettings/Import</c>. The descriptions of all keys are kept in
/// <see cref="DeviceSpecificSettingsValidationSchema"/>.
/// </summary>
public sealed class ImportDeviceSpecificSettingsRequest : IRequestWithRootAdditionalData
{
    private ImportDeviceSpecificSettingsOptions options = new();

    /// <summary>Required device specific settings package as returned by <c>GET /api/DeviceSpecificSettings</c>.</summary>
    [JsonPropertyName("package")]
    public DeviceSpecificSettingsPackage? Package { get; set; }

    /// <summary>Optional import options. Defaults to an empty object; an explicit null is treated like the default.</summary>
    [JsonPropertyName("options")]
    public ImportDeviceSpecificSettingsOptions Options
    {
        get => options;
        set => options = value ?? new ImportDeviceSpecificSettingsOptions();
    }

    /// <summary>Unsupported root keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>Options of the device specific settings import.</summary>
public sealed class ImportDeviceSpecificSettingsOptions : IRequestWithAdditionalData
{
    /// <summary>
    /// <c>merge</c> only sets the values of the package; <c>replace</c> makes the package the complete state of the levels it
    /// names and removes their other overrides. Defaults to <c>merge</c>; null means the default.
    /// </summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; } = DeviceSpecificSettingsValidationSchema.kModeMerge;

    /// <summary>True only validates and previews the import; nothing is written. Defaults to false; null means the default.</summary>
    [JsonPropertyName("dryRun")]
    public bool? DryRun { get; set; } = false;

    /// <summary>Unsupported option keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Request of <c>POST /api/DeviceSpecificSettings/Snapshots</c>. The descriptions of all keys are kept in
/// <see cref="DeviceSpecificSettingsValidationSchema"/>.
/// </summary>
public sealed class AddDeviceSpecificSettingsSnapshotRequest : IRequestWithRootAdditionalData
{
    private EmptyRequestOptions options = new();

    /// <summary>Required, unique name of the snapshot.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Optional description. Defaults to null.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Optional package to store. Defaults to null, which stores the current settings of this installation.</summary>
    [JsonPropertyName("package")]
    public DeviceSpecificSettingsPackage? Package { get; set; }

    /// <summary>Optional options; none are defined yet. Defaults to an empty object.</summary>
    [JsonPropertyName("options")]
    public EmptyRequestOptions Options
    {
        get => options;
        set => options = value ?? new EmptyRequestOptions();
    }

    /// <summary>Unsupported root keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>An options object without defined options; every key in it is reported as unsupported.</summary>
public sealed class EmptyRequestOptions : IRequestWithAdditionalData
{
    /// <summary>Unsupported option keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Request of <c>POST /api/DeviceSpecificSettings/Snapshots/List</c>. The descriptions of all keys are kept in
/// <see cref="DeviceSpecificSettingsValidationSchema"/>.
/// </summary>
public sealed class ListDeviceSpecificSettingsSnapshotsRequest : IRequestWithRootAdditionalData
{
    private ListDeviceSpecificSettingsSnapshotsOptions options = new();

    /// <summary>Optional output options. Defaults to an empty object; an explicit null is treated like the default.</summary>
    [JsonPropertyName("options")]
    public ListDeviceSpecificSettingsSnapshotsOptions Options
    {
        get => options;
        set => options = value ?? new ListDeviceSpecificSettingsSnapshotsOptions();
    }

    /// <summary>Unsupported root keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>Output options of the snapshot list.</summary>
public sealed class ListDeviceSpecificSettingsSnapshotsOptions : IRequestWithAdditionalData
{
    /// <summary>Optional response filter; null applies no restriction.</summary>
    [JsonPropertyName("filter")]
    public DeviceSpecificSettingsSnapshotFilterRequest? Filter { get; set; }

    /// <summary>
    /// Maximum number of snapshots returned, between 1 and the configured <c>maxRestResponseObjectLimit</c>; null returns a
    /// page of that size. The <c>X-Has-More</c> response header tells whether further snapshots follow.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    /// <summary>Number of snapshots skipped, at least 0; null skips none.</summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; set; }

    /// <summary>Unsupported option keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Response filter of the snapshot list: every key matches a field of <see cref="DeviceSpecificSettingsSnapshotSummary"/>
/// and is nullable; a key that is omitted or null does not restrict the result.
/// </summary>
public sealed class DeviceSpecificSettingsSnapshotFilterRequest : IRequestWithAdditionalData
{
    /// <summary>Optional exact snapshot id.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>Optional case-insensitive part of the name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Optional case-insensitive part of the description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Optional exact FWO version.</summary>
    [JsonPropertyName("fwo_version")]
    public string? FwoVersion { get; set; }

    /// <summary>Optional exact creation time.</summary>
    [JsonPropertyName("created")]
    public DateTimeOffset? Created { get; set; }

    /// <summary>Unsupported filter keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
