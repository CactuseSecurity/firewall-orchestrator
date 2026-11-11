namespace FWO.Data.DeviceSpecificSettings;

/// <summary>
/// Identifies a device specific setting independently of its serialized value.
/// </summary>
public abstract class DeviceSpecificSettingKey
{
    private readonly HashSet<DeviceSpecificScopeType> allowedScopes;

    public string DatabaseKey { get; }

    public Type ValueType { get; }

    public IReadOnlySet<DeviceSpecificScopeType> AllowedScopes => allowedScopes;

    /// <summary>
    /// Reads the value from a settings DTO. Null for keys stored in the DTO property named after the key.
    /// </summary>
    public Func<GlobalDeviceSpecificSettings, object>? DtoGetter { get; }

    /// <summary>
    /// Writes the value to a settings DTO. Null for keys stored in the DTO property named after the key.
    /// </summary>
    public Action<GlobalDeviceSpecificSettings, object>? DtoSetter { get; }

    private protected DeviceSpecificSettingKey(
        string databaseKey,
        Type valueType,
        IEnumerable<DeviceSpecificScopeType> allowedScopes,
        Func<GlobalDeviceSpecificSettings, object>? dtoGetter = null,
        Action<GlobalDeviceSpecificSettings, object>? dtoSetter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentNullException.ThrowIfNull(allowedScopes);

        this.allowedScopes = allowedScopes.ToHashSet();
        if (this.allowedScopes.Count == 0 || this.allowedScopes.Contains(DeviceSpecificScopeType.Undefined))
        {
            throw new ArgumentException("A setting key must allow at least one defined scope.", nameof(allowedScopes));
        }

        if ((dtoGetter is null) != (dtoSetter is null))
        {
            throw new ArgumentException("A setting key needs both DTO accessors or none.", nameof(dtoGetter));
        }

        DatabaseKey = databaseKey;
        ValueType = valueType;
        DtoGetter = dtoGetter;
        DtoSetter = dtoSetter;
    }

    /// <summary>Whether an override of this setting may be stored at the given scope type.</summary>
    public bool IsAllowedAt(DeviceSpecificScopeType scopeType)
    {
        return allowedScopes.Contains(scopeType);
    }

    public override string ToString()
    {
        return DatabaseKey;
    }
}

/// <summary>
/// Identifies a device specific setting and the C# type accepted for its value.
/// </summary>
public sealed class DeviceSpecificSettingKey<TValue> : DeviceSpecificSettingKey
{
    internal DeviceSpecificSettingKey(
        string databaseKey,
        IEnumerable<DeviceSpecificScopeType> allowedScopes)
        : base(databaseKey, typeof(TValue), allowedScopes)
    {
    }

    /// <summary>Creates a key whose value is kept in the DTO by the given accessors instead of a property of the same name.</summary>
    internal DeviceSpecificSettingKey(
        string databaseKey,
        IEnumerable<DeviceSpecificScopeType> allowedScopes,
        Func<GlobalDeviceSpecificSettings, TValue> dtoGetter,
        Action<GlobalDeviceSpecificSettings, TValue> dtoSetter)
        : base(databaseKey, typeof(TValue), allowedScopes,
            settings => dtoGetter(settings)!, (settings, value) => dtoSetter(settings, (TValue)value))
    {
    }
}
