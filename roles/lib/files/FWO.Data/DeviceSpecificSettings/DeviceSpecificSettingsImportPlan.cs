using System.Text.Json.Serialization;

namespace FWO.Data.DeviceSpecificSettings;

/// <summary>How an imported package is applied to the levels it names.</summary>
public enum DeviceSpecificSettingsImportMode
{
    /// <summary>Only the values in the package are set; other overrides of the named levels are kept.</summary>
    Merge,

    /// <summary>The package is the complete state of the levels it names; overrides missing in it are removed.</summary>
    Replace
}

/// <summary>Kind of change an import makes to one override.</summary>
public enum DeviceSpecificSettingChangeKind
{
    /// <summary>The override is new on the level.</summary>
    Added,

    /// <summary>The override exists with another value.</summary>
    Changed,

    /// <summary>The override is removed, so the level inherits the value again.</summary>
    Removed
}

/// <summary>A problem found while validating an import package, with the path of the offending part.</summary>
/// <param name="Path">Path in the package, e.g. <c>nodes[2].values.scUrl</c>.</param>
/// <param name="Message">What is wrong.</param>
public sealed record DeviceSpecificSettingsImportIssue(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("message")] string Message);

/// <summary>One override an import adds, changes or removes.</summary>
public sealed class DeviceSpecificSettingChange
{
    /// <summary>Setting key.</summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    /// <summary>Kind of change.</summary>
    [JsonPropertyName("change"), JsonConverter(typeof(JsonStringEnumConverter))]
    public DeviceSpecificSettingChangeKind Kind { get; set; }

    /// <summary>Current value as JSON text, referenced objects by name; null for an added override.</summary>
    [JsonPropertyName("old_value")]
    public string? OldValue { get; set; }

    /// <summary>Imported value as JSON text, referenced objects by name; null for a removed override.</summary>
    [JsonPropertyName("new_value")]
    public string? NewValue { get; set; }
}

/// <summary>The changes an import makes to one level.</summary>
public sealed class DeviceSpecificSettingsNodeChanges
{
    /// <summary>Level type.</summary>
    [JsonPropertyName("level"), JsonConverter(typeof(JsonStringEnumConverter))]
    public DeviceSpecificScopeType Level { get; set; }

    /// <summary>Name of the level, e.g. <c>Global</c>, <c>Check Point R8x</c> or <c>mgmt &gt; gateway</c>.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Added, changed and removed overrides, ordered by key.</summary>
    [JsonPropertyName("changes")]
    public List<DeviceSpecificSettingChange> Changes { get; set; } = [];

    /// <summary>Number of overrides in the package that already have the imported value.</summary>
    [JsonPropertyName("unchanged")]
    public int UnchangedCount { get; set; }
}

/// <summary>
/// Result of checking an import package against this installation: either the validation errors, or the changes
/// per level that applying the package makes.
/// </summary>
public sealed class DeviceSpecificSettingsImportPlan
{
    /// <summary>Mode the plan was made for.</summary>
    [JsonPropertyName("mode"), JsonConverter(typeof(JsonStringEnumConverter))]
    public DeviceSpecificSettingsImportMode Mode { get; set; }

    /// <summary>Changes per level named in the package, in package order; empty while there are errors.</summary>
    [JsonPropertyName("nodes")]
    public List<DeviceSpecificSettingsNodeChanges> Nodes { get; set; } = [];

    /// <summary>Every validation error found; nothing is written while there is one.</summary>
    [JsonPropertyName("errors")]
    public List<DeviceSpecificSettingsImportIssue> Errors { get; set; } = [];

    /// <summary>True if the package is invalid for this installation.</summary>
    [JsonIgnore]
    public bool HasErrors => Errors.Count > 0;

    /// <summary>True if applying the package changes at least one override.</summary>
    [JsonIgnore]
    public bool HasChanges => Nodes.Any(node => node.Changes.Count > 0);
}
