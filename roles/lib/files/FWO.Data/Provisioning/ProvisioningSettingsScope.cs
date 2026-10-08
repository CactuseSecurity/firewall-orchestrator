namespace FWO.Data.Provisioning;

/// <summary>
/// Locates one level of the provisioning hierarchy: its scope type and object key, and its persisted node if there is one
/// (NodeId 0 while nothing has been stored for the level yet).
/// </summary>
public sealed class ProvisioningSettingsScope
{
    public ProvisioningScopeType ScopeType { get; set; }

    public string ObjectKey { get; set; } = "";

    public string? DisplayName { get; set; } = "";

    public long NodeId { get; set; }

    public long? ParentNodeId { get; set; }

    public int? SortOrder { get; set; }
}
