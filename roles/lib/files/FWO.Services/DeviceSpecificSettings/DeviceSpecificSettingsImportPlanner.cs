using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data.DeviceSpecificSettings;
using Newtonsoft.Json.Linq;

namespace FWO.Services.DeviceSpecificSettings;

/// <summary>The writes of one level an import makes: overrides to store and overrides to remove.</summary>
/// <param name="Path">Path of the level along the current device hierarchy.</param>
/// <param name="Upserts">Typed values to store by key.</param>
/// <param name="Removals">Keys whose override is removed.</param>
public sealed record DeviceSpecificSettingsNodeWrite(
    DeviceSpecificScopePath Path,
    Dictionary<DeviceSpecificSettingKey, object> Upserts,
    List<DeviceSpecificSettingKey> Removals);

/// <summary>The preview of an import and the writes that apply it; the writes are empty while the plan has errors.</summary>
/// <param name="Plan">Preview: changes per level, or the validation errors.</param>
/// <param name="Writes">Writes per level with at least one change.</param>
public sealed record DeviceSpecificSettingsImportPlanResult(DeviceSpecificSettingsImportPlan Plan, List<DeviceSpecificSettingsNodeWrite> Writes);

/// <summary>
/// Compares a validated package with the stored settings and determines the added, changed and removed overrides per
/// level. In merge mode only the values of the package are set; in replace mode the package is the complete state of
/// the levels it names, so their other overrides are removed. Levels not named in the package are never touched.
/// </summary>
public static class DeviceSpecificSettingsImportPlanner
{
    /// <summary>Validates the package against the installation and plans its import.</summary>
    public static DeviceSpecificSettingsImportPlanResult Plan(DeviceSpecificSettingsPackage? package,
        DeviceSpecificSettingsImportMode mode, DeviceSpecificSettingsTransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        DeviceSpecificSettingsImportPlan plan = new() { Mode = mode };
        List<ValidatedDeviceSpecificNode> nodes = DeviceSpecificSettingsPackageValidator.Validate(package, context, plan.Errors);
        if (plan.HasErrors)
        {
            return new DeviceSpecificSettingsImportPlanResult(plan, []);
        }

        List<DeviceSpecificSettingsNodeWrite> writes = [];
        foreach (ValidatedDeviceSpecificNode node in nodes)
        {
            (DeviceSpecificSettingsNodeChanges changes, DeviceSpecificSettingsNodeWrite write) = PlanNode(node, mode, context);
            plan.Nodes.Add(changes);
            if (changes.Changes.Count > 0)
            {
                writes.Add(write);
            }
        }
        return new DeviceSpecificSettingsImportPlanResult(plan, writes);
    }

    private static (DeviceSpecificSettingsNodeChanges, DeviceSpecificSettingsNodeWrite) PlanNode(ValidatedDeviceSpecificNode node,
        DeviceSpecificSettingsImportMode mode, DeviceSpecificSettingsTransferContext context)
    {
        IReadOnlyDictionary<DeviceSpecificSettingKey, JToken> stored =
            context.FindStoredNode(node.Path.ScopeType, node.Path.ObjectKey)?.Values ?? new Dictionary<DeviceSpecificSettingKey, JToken>();
        DeviceSpecificSettingsNodeChanges changes = new() { Level = node.Path.ScopeType, Name = node.Name };
        DeviceSpecificSettingsNodeWrite write = new(node.Path, [], []);

        foreach ((DeviceSpecificSettingKey key, ValidatedDeviceSpecificValue value) in node.Values)
        {
            bool isStored = stored.TryGetValue(key, out JToken? storedValue);
            if (isStored && JToken.DeepEquals(storedValue, value.Json))
            {
                changes.UnchangedCount++;
                continue;
            }

            write.Upserts[key] = value.Value;
            changes.Changes.Add(new DeviceSpecificSettingChange
            {
                Key = key.DatabaseKey,
                Kind = isStored ? DeviceSpecificSettingChangeKind.Changed : DeviceSpecificSettingChangeKind.Added,
                OldValue = isStored ? DeviceSpecificSettingsPackageValidator.DisplayValue(key, storedValue!, context) : null,
                NewValue = DeviceSpecificSettingsPackageValidator.DisplayValue(key, value.Json, context)
            });
        }

        if (mode == DeviceSpecificSettingsImportMode.Replace)
        {
            foreach ((DeviceSpecificSettingKey key, JToken storedValue) in stored.Where(entry => !node.Values.ContainsKey(entry.Key)))
            {
                write.Removals.Add(key);
                changes.Changes.Add(new DeviceSpecificSettingChange
                {
                    Key = key.DatabaseKey,
                    Kind = DeviceSpecificSettingChangeKind.Removed,
                    OldValue = DeviceSpecificSettingsPackageValidator.DisplayValue(key, storedValue, context)
                });
            }
        }

        changes.Changes.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        return (changes, write);
    }
}
