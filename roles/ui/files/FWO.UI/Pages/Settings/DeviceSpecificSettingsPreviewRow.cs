using FWO.Data.DeviceSpecificSettings;

namespace FWO.Ui.Pages.Settings
{
    /// <summary>One changed override in the import preview, flattened for the table.</summary>
    /// <param name="Level">Name of the level.</param>
    /// <param name="Key">Setting key.</param>
    /// <param name="Kind">Kind of change.</param>
    /// <param name="OldValue">Current value as JSON text; empty for an added override.</param>
    /// <param name="NewValue">Imported value as JSON text; empty for a removed override.</param>
    public sealed record DeviceSpecificSettingsPreviewRow(string Level, string Key, DeviceSpecificSettingChangeKind Kind, string OldValue, string NewValue)
    {
        /// <summary>Kind of change as text, for sorting and filtering the table.</summary>
        public string Change => Kind.ToString();

        /// <summary>The rows of all changes of a plan, in plan order.</summary>
        public static List<DeviceSpecificSettingsPreviewRow> FromPlan(DeviceSpecificSettingsImportPlan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);
            return [.. plan.Nodes.SelectMany(node => node.Changes.Select(change =>
                new DeviceSpecificSettingsPreviewRow(node.Name, change.Key, change.Kind, change.OldValue ?? "", change.NewValue ?? "")))];
        }
    }
}
