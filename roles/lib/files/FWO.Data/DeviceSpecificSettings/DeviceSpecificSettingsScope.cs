namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// Locates one level of the device specific settings hierarchy: its scope type and object key, and its persisted node if there is one
/// (NodeId 0 while nothing has been stored for the level yet).
/// </summary>
public sealed class DeviceSpecificSettingsScope
{
    public DeviceSpecificScopeType ScopeType { get; set; }

    public string ObjectKey { get; set; } = "";

    public string? DisplayName { get; set; } = "";

    public long NodeId { get; set; }

    public long? ParentNodeId { get; set; }

    public int? SortOrder { get; set; }
}
