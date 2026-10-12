using FWO.Config.Api.Data;
using FWO.Data.Provisioning;

namespace FWO.Config.Api.Provisioning;

/// <summary>
/// Converts between database node metadata and the public provisioning scope model.
/// </summary>
public static class ProvisioningSettingsScopeFactory
{
    public const string GlobalNodeType = "global";
    public const string DeviceTypeNodeType = "device_type";
    public const string ManagementNodeType = "management";
    public const string GatewayNodeType = "gateway";

    /// <summary>Creates the scope of a node read from the database.</summary>
    public static ProvisioningSettingsScope Create(ProvisioningConfigNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new ProvisioningSettingsScope
        {
            ScopeType = ParseNodeType(node.NodeType),
            ObjectKey = node.ObjectKey,
            DisplayName = node.DisplayName,
            NodeId = node.Id,
            ParentNodeId = node.ParentId,
            SortOrder = node.SortOrder
        };
    }

    /// <summary>Creates an independent copy of a scope.</summary>
    public static ProvisioningSettingsScope Copy(ProvisioningSettingsScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return new ProvisioningSettingsScope
        {
            ScopeType = scope.ScopeType,
            ObjectKey = scope.ObjectKey,
            DisplayName = scope.DisplayName,
            NodeId = scope.NodeId,
            ParentNodeId = scope.ParentNodeId,
            SortOrder = scope.SortOrder
        };
    }

    /// <summary>Returns the database node type of a scope type.</summary>
    public static string ToNodeType(ProvisioningScopeType scopeType)
    {
        return scopeType switch
        {
            ProvisioningScopeType.Global => GlobalNodeType,
            ProvisioningScopeType.DeviceType => DeviceTypeNodeType,
            ProvisioningScopeType.Management => ManagementNodeType,
            ProvisioningScopeType.Gateway => GatewayNodeType,
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, "A defined provisioning scope type is required.")
        };
    }

    /// <summary>Returns the scope type of a database node type; throws for unknown node types.</summary>
    public static ProvisioningScopeType ParseNodeType(string nodeType)
    {
        return nodeType switch
        {
            GlobalNodeType => ProvisioningScopeType.Global,
            DeviceTypeNodeType => ProvisioningScopeType.DeviceType,
            ManagementNodeType => ProvisioningScopeType.Management,
            GatewayNodeType => ProvisioningScopeType.Gateway,
            _ => throw new InvalidOperationException($"Unknown provisioning node type '{nodeType}'.")
        };
    }
}
