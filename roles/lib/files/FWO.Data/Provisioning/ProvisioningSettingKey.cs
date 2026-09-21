namespace FWO.Data.Provisioning;

/// <summary>
/// Identifies a provisioning setting independently of its serialized value.
/// </summary>
public abstract class ProvisioningSettingKey
{
    private readonly HashSet<ProvisioningScopeType> allowedScopes;

    public string DatabaseKey { get; }

    public Type ValueType { get; }

    public IReadOnlySet<ProvisioningScopeType> AllowedScopes => allowedScopes;

    internal ProvisioningSettingKey(
        string databaseKey,
        Type valueType,
        IEnumerable<ProvisioningScopeType> allowedScopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(allowedScopes);

        this.allowedScopes = allowedScopes.ToHashSet();
        if (this.allowedScopes.Count == 0 || this.allowedScopes.Contains(ProvisioningScopeType.Undefined))
        {
            throw new ArgumentException("A setting key must allow at least one defined scope.", nameof(allowedScopes));
        }

        DatabaseKey = databaseKey;
        ValueType = valueType;
    }

    public bool IsAllowedAt(ProvisioningScopeType scopeType)
    {
        return allowedScopes.Contains(scopeType);
    }

    public override string ToString()
    {
        return DatabaseKey;
    }
}

/// <summary>
/// Identifies a provisioning setting and the C# type accepted for its value.
/// </summary>
public sealed class ProvisioningSettingKey<TValue> : ProvisioningSettingKey
{
    internal ProvisioningSettingKey(
        string databaseKey,
        IEnumerable<ProvisioningScopeType> allowedScopes)
        : base(databaseKey, typeof(TValue), allowedScopes)
    {
    }
}
