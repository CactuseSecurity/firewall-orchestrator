using System.Text.Json.Serialization;
using FWO.Data.DeviceSpecificSettings;

namespace FWO.Middleware.Server.Responses;

/// <summary>Response of <c>POST /api/DeviceSpecificSettings/Import</c>.</summary>
public sealed class ImportDeviceSpecificSettingsResponse
{
    /// <summary>True if the changes were written; false for a dry run.</summary>
    [JsonPropertyName("applied")]
    public bool Applied { get; set; }

    /// <summary>Mode the import ran in.</summary>
    [JsonPropertyName("mode"), JsonConverter(typeof(JsonStringEnumConverter))]
    public DeviceSpecificSettingsImportMode Mode { get; set; }

    /// <summary>Changes per level named in the package, in package order; at most one entry per package level.</summary>
    [JsonPropertyName("nodes")]
    public List<DeviceSpecificSettingsNodeChanges> Nodes { get; set; } = [];
}

/// <summary>Response of <c>GET /api/DeviceSpecificSettings/Snapshots/{id}</c>: a stored snapshot with its package.</summary>
public sealed class DeviceSpecificSettingsSnapshotResponse : DeviceSpecificSettingsSnapshotSummary
{
    /// <summary>The stored package.</summary>
    [JsonPropertyName("package")]
    public DeviceSpecificSettingsPackage Package { get; set; } = new();
}
