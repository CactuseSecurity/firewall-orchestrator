namespace FWO.Data.Provisioning;

public sealed class ProvisioningSettingsScope
{
    public ProvisioningScopeType ScopeType { get; set; }

    public string ObjectKey { get; set; } = "";

    public string? DisplayName { get; set; } = "";

    public long NodeId { get; set; }

    public long? ParentNodeId { get; set; }

    public int? SortOrder { get; set; }
}
