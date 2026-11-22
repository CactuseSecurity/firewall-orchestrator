using System.Text.Json;
using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using Newtonsoft.Json.Linq;

namespace FWO.Services.DeviceSpecificSettings;

/// <summary>An override of a validated package entry: the typed value and its JSON form as stored in the database.</summary>
/// <param name="Value">Typed value, ready for a change set.</param>
/// <param name="Json">Value as stored, for comparing it with the stored override.</param>
public sealed record ValidatedDeviceSpecificValue(object Value, JToken Json);

/// <summary>A package entry resolved to a level of this installation, with its validated overrides.</summary>
/// <param name="Path">Path of the level along the current device hierarchy.</param>
/// <param name="Name">Name of the level for previews and messages.</param>
/// <param name="Values">Validated overrides by key.</param>
public sealed record ValidatedDeviceSpecificNode(
    DeviceSpecificScopePath Path,
    string Name,
    Dictionary<DeviceSpecificSettingKey, ValidatedDeviceSpecificValue> Values);

/// <summary>
/// Validates a <see cref="DeviceSpecificSettingsPackage"/> completely against an installation before anything is written:
/// format, bounds, known levels and keys, keys only at the levels they may be set at, values by the serializer and the
/// value rules of the settings page, levels and referenced objects resolved by name. Collects every error instead of
/// stopping at the first one.
/// </summary>
public static class DeviceSpecificSettingsPackageValidator
{
    private const string kDeviceTypeName = "device_type_name";
    private const string kDeviceTypeVersion = "device_type_version";
    private const string kManagementName = "management_name";
    private const string kGatewayName = "gateway_name";

    /// <summary>
    /// Checks the parts of a package that do not depend on an installation: format, version and the number of levels.
    /// </summary>
    public static List<DeviceSpecificSettingsImportIssue> ValidateStructure(DeviceSpecificSettingsPackage? package)
    {
        List<DeviceSpecificSettingsImportIssue> errors = [];
        if (package is null)
        {
            errors.Add(new("", "The package is missing."));
            return errors;
        }

        if (package.Format != DeviceSpecificSettingsPackage.kFormat)
        {
            errors.Add(new("format", $"The format must be '{DeviceSpecificSettingsPackage.kFormat}', not '{package.Format}'."));
        }

        if (package.Version != DeviceSpecificSettingsPackage.kCurrentVersion)
        {
            errors.Add(new("version", $"Format version {package.Version} is not supported; this release reads version {DeviceSpecificSettingsPackage.kCurrentVersion}."));
        }

        if (package.Nodes is null)
        {
            errors.Add(new("nodes", "The list of levels is missing."));
        }
        else if (package.Nodes.Count > DeviceSpecificSettingsPackage.kMaxNodes)
        {
            errors.Add(new("nodes", $"A package must not contain more than {DeviceSpecificSettingsPackage.kMaxNodes} levels, but contains {package.Nodes.Count}."));
        }
        return errors;
    }

    /// <summary>
    /// Validates a package against an installation and returns its entries resolved to levels of the installation.
    /// The entries are only meaningful if no error was added.
    /// </summary>
    /// <param name="package">The package.</param>
    /// <param name="context">The installation.</param>
    /// <param name="errors">Receives every error found.</param>
    public static List<ValidatedDeviceSpecificNode> Validate(DeviceSpecificSettingsPackage? package,
        DeviceSpecificSettingsTransferContext context, List<DeviceSpecificSettingsImportIssue> errors)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(errors);

        List<DeviceSpecificSettingsImportIssue> structureErrors = ValidateStructure(package);
        errors.AddRange(structureErrors);
        if (structureErrors.Count > 0)
        {
            return [];
        }

        List<ValidatedDeviceSpecificNode> nodes = [];
        Dictionary<(DeviceSpecificScopeType, string), int> firstIndexByLevel = [];
        for (int index = 0; index < package!.Nodes.Count; index++)
        {
            string path = $"nodes[{index}]";
            DeviceSpecificSettingsPackageNode? node = package.Nodes[index];
            if (node is null)
            {
                errors.Add(new(path, "The level entry is empty."));
                continue;
            }

            ResolvedLevel? level = ResolveLevel(node, path, context, errors);
            if (level is not null && !firstIndexByLevel.TryAdd((level.Path.ScopeType, level.Path.ObjectKey), index))
            {
                errors.Add(new(path, $"Level '{level.Name}' is already named in nodes[{firstIndexByLevel[(level.Path.ScopeType, level.Path.ObjectKey)]}]."));
            }

            Dictionary<DeviceSpecificSettingKey, ValidatedDeviceSpecificValue> values = ValidateValues(node, path, level?.Path.ScopeType, context, errors);
            if (level is not null)
            {
                nodes.Add(new ValidatedDeviceSpecificNode(level.Path, level.Name, values));
            }
        }
        return nodes;
    }

    private sealed record ResolvedLevel(DeviceSpecificScopePath Path, string Name);

    /// <summary>Resolves the natural key of an entry to a level of the installation; unknown objects are reported, never created.</summary>
    private static ResolvedLevel? ResolveLevel(DeviceSpecificSettingsPackageNode node, string path,
        DeviceSpecificSettingsTransferContext context, List<DeviceSpecificSettingsImportIssue> errors)
    {
        DeviceSpecificScopeType? scopeType = ParseLevel(node.Level);
        if (scopeType is null)
        {
            errors.Add(new($"{path}.level", $"Level '{node.Level}' is unknown; use '{DeviceSpecificSettingsScopeFactory.GlobalNodeType}', "
                + $"'{DeviceSpecificSettingsScopeFactory.DeviceTypeNodeType}', '{DeviceSpecificSettingsScopeFactory.ManagementNodeType}' "
                + $"or '{DeviceSpecificSettingsScopeFactory.GatewayNodeType}'."));
            return null;
        }

        if (!ValidateNameFields(node, path, scopeType.Value, errors))
        {
            return null;
        }

        return scopeType.Value switch
        {
            DeviceSpecificScopeType.Global => new ResolvedLevel(DeviceSpecificScopePath.Global(), DeviceSpecificSettingsTransferContext.kGlobalName),
            DeviceSpecificScopeType.DeviceType => ResolveDeviceType(node, path, context, errors),
            DeviceSpecificScopeType.Management => ResolveManagement(node, path, context, errors),
            _ => ResolveGateway(node, path, context, errors)
        };
    }

    private static DeviceSpecificScopeType? ParseLevel(string? level) => level switch
    {
        DeviceSpecificSettingsScopeFactory.GlobalNodeType => DeviceSpecificScopeType.Global,
        DeviceSpecificSettingsScopeFactory.DeviceTypeNodeType => DeviceSpecificScopeType.DeviceType,
        DeviceSpecificSettingsScopeFactory.ManagementNodeType => DeviceSpecificScopeType.Management,
        DeviceSpecificSettingsScopeFactory.GatewayNodeType => DeviceSpecificScopeType.Gateway,
        _ => null
    };

    /// <summary>Checks that exactly the name fields of the level are given.</summary>
    private static bool ValidateNameFields(DeviceSpecificSettingsPackageNode node, string path, DeviceSpecificScopeType scopeType,
        List<DeviceSpecificSettingsImportIssue> errors)
    {
        int errorCount = errors.Count;
        bool isDeviceType = scopeType == DeviceSpecificScopeType.DeviceType;
        bool hasManagement = scopeType is DeviceSpecificScopeType.Management or DeviceSpecificScopeType.Gateway;
        CheckNameField(node.DeviceTypeName, isDeviceType, path, kDeviceTypeName, node.Level, errors);
        CheckNameField(node.DeviceTypeVersion, isDeviceType, path, kDeviceTypeVersion, node.Level, errors);
        CheckNameField(node.ManagementName, hasManagement, path, kManagementName, node.Level, errors);
        CheckNameField(node.GatewayName, scopeType == DeviceSpecificScopeType.Gateway, path, kGatewayName, node.Level, errors);
        return errors.Count == errorCount;
    }

    private static void CheckNameField(string? value, bool required, string path, string field, string level,
        List<DeviceSpecificSettingsImportIssue> errors)
    {
        if (required && string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new($"{path}.{field}", $"'{field}' is required for level '{level}'."));
        }
        else if (!required && value is not null)
        {
            errors.Add(new($"{path}.{field}", $"'{field}' is not allowed for level '{level}'."));
        }
    }

    private static ResolvedLevel? ResolveDeviceType(DeviceSpecificSettingsPackageNode node, string path,
        DeviceSpecificSettingsTransferContext context, List<DeviceSpecificSettingsImportIssue> errors)
    {
        DeviceType? deviceType = context.DeviceTypes.FirstOrDefault(candidate =>
            candidate.Name == node.DeviceTypeName && candidate.Version == node.DeviceTypeVersion);
        if (deviceType is null)
        {
            errors.Add(new(path, $"Device type '{node.DeviceTypeName} {node.DeviceTypeVersion}' does not exist."));
            return null;
        }
        return new ResolvedLevel(DeviceSpecificScopePath.ForDeviceType(deviceType), deviceType.NameVersion());
    }

    private static ResolvedLevel? ResolveManagement(DeviceSpecificSettingsPackageNode node, string path,
        DeviceSpecificSettingsTransferContext context, List<DeviceSpecificSettingsImportIssue> errors)
    {
        Management? management = FindManagement(node.ManagementName!, path, context, errors);
        return management is null ? null : new ResolvedLevel(DeviceSpecificScopePath.ForManagement(management), management.Name);
    }

    private static ResolvedLevel? ResolveGateway(DeviceSpecificSettingsPackageNode node, string path,
        DeviceSpecificSettingsTransferContext context, List<DeviceSpecificSettingsImportIssue> errors)
    {
        Management? management = FindManagement(node.ManagementName!, path, context, errors);
        if (management is null)
        {
            return null;
        }

        List<Device> devices = [.. management.Devices.Where(device => device.Name == node.GatewayName)];
        if (devices.Count != 1)
        {
            errors.Add(new(path, devices.Count == 0
                ? $"Gateway '{node.GatewayName}' does not exist in management '{management.Name}'."
                : $"Gateway name '{node.GatewayName}' is ambiguous in management '{management.Name}'."));
            return null;
        }
        return new ResolvedLevel(DeviceSpecificScopePath.ForGateway(management, devices[0]),
            DeviceSpecificSettingsTransferContext.GatewayName(management.Name, devices[0].Name!));
    }

    private static Management? FindManagement(string name, string path, DeviceSpecificSettingsTransferContext context,
        List<DeviceSpecificSettingsImportIssue> errors)
    {
        List<Management> managements = [.. context.Managements.Where(management => management.Name == name)];
        if (managements.Count != 1)
        {
            errors.Add(new(path, managements.Count == 0
                ? $"Management '{name}' does not exist."
                : $"Management name '{name}' is ambiguous."));
            return null;
        }
        return managements[0];
    }

    /// <summary>Validates the overrides of an entry; the level checks are skipped while the level is unknown.</summary>
    private static Dictionary<DeviceSpecificSettingKey, ValidatedDeviceSpecificValue> ValidateValues(DeviceSpecificSettingsPackageNode node,
        string path, DeviceSpecificScopeType? scopeType, DeviceSpecificSettingsTransferContext context, List<DeviceSpecificSettingsImportIssue> errors)
    {
        Dictionary<DeviceSpecificSettingKey, ValidatedDeviceSpecificValue> values = [];
        if (node.Values is null)
        {
            errors.Add(new($"{path}.values", "The overrides are missing; use an empty object for a level without overrides."));
            return values;
        }

        foreach ((string databaseKey, JsonElement element) in node.Values.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            string valuePath = $"{path}.values.{databaseKey}";
            if (!DeviceSpecificSettingKeys.ByDatabaseKey.TryGetValue(databaseKey, out DeviceSpecificSettingKey? key))
            {
                errors.Add(new(valuePath, $"Setting '{databaseKey}' is unknown."));
                continue;
            }

            if (scopeType is not null && !key.IsAllowedAt(scopeType.Value))
            {
                errors.Add(new(valuePath, $"Setting '{databaseKey}' cannot be set at level '{node.Level}'."));
                continue;
            }

            ValidatedDeviceSpecificValue? value = ValidateValue(key, element, valuePath, context, errors);
            if (value is not null)
            {
                values[key] = value;
            }
        }
        return values;
    }

    /// <summary>Checks one value with the serializer and the value rules and returns it typed and as stored.</summary>
    private static ValidatedDeviceSpecificValue? ValidateValue(DeviceSpecificSettingKey key, JsonElement element, string path,
        DeviceSpecificSettingsTransferContext context, List<DeviceSpecificSettingsImportIssue> errors)
    {
        JToken? json = key == DeviceSpecificSettingKeys.ScCredential
            ? ResolveCredential(element, path, context, errors)
            : DeviceSpecificSettingsPackageJson.ToJToken(element);
        if (json is null)
        {
            return null;
        }

        try
        {
            object value = DeviceSpecificSettingValueRules.Normalize(key, DeviceSpecificSettingValueSerializer.Deserialize(key, json));
            return new ValidatedDeviceSpecificValue(value, DeviceSpecificSettingValueSerializer.Serialize(key, value));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            errors.Add(new(path, exception.Message));
            return null;
        }
    }

    /// <summary>Maps a credential name to the ID of the single password credential of that name.</summary>
    private static JValue? ResolveCredential(JsonElement element, string path, DeviceSpecificSettingsTransferContext context,
        List<DeviceSpecificSettingsImportIssue> errors)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            errors.Add(new(path, "The credential must be given by its name as a JSON string."));
            return null;
        }

        string name = element.GetString()!;
        List<Credential> credentials = context.FindPasswordCredentials(name);
        if (credentials.Count == 1)
        {
            return new JValue(credentials[0].Id);
        }

        string message;
        if (credentials.Count > 1)
        {
            message = $"Credential name '{name}' is ambiguous.";
        }
        else if (context.HasCredentialNamed(name))
        {
            message = $"Credential '{name}' is a key pair and cannot log in with a password.";
        }
        else
        {
            message = $"Credential '{name}' does not exist.";
        }
        errors.Add(new(path, message));
        return null;
    }

    /// <summary>A value as shown in previews: JSON text, with a referenced credential shown by its name.</summary>
    public static string DisplayValue(DeviceSpecificSettingKey key, JToken value, DeviceSpecificSettingsTransferContext context)
    {
        if (key == DeviceSpecificSettingKeys.ScCredential && value.Type == JTokenType.Integer
            && context.FindCredential(value.Value<int>()) is Credential credential)
        {
            return new JValue(credential.Name).ToString(Newtonsoft.Json.Formatting.None);
        }
        return value.ToString(Newtonsoft.Json.Formatting.None);
    }
}
