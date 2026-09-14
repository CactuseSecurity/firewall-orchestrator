using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Logging;
using System.Text.Json;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Holds the editor state of the rule owner mapping source settings and keeps the behaviour of every
    /// settings page offering them identical.
    /// </summary>
    public class OwnerMappingSourceHandler
    {
        /// <summary>
        /// Text key reported when no mapping source is selected at all.
        /// </summary>
        public const string kNoSourceSelectedError = "E5504";

        /// <summary>
        /// Text key reported when the custom field mapping is selected without any owner key,
        /// and when the owner key typed into the editor is blank.
        /// </summary>
        public const string kNoOwnerKeyError = "E5505";

        /// <summary>
        /// Text key reported when the owner key typed into the editor is already configured.
        /// </summary>
        public const string kDuplicateOwnerKeyError = "E5507";

        /// <summary>
        /// Mapping sources offered for selection. Manual mappings are set on a single rule and cannot be chosen here.
        /// </summary>
        public List<OwnerMappingSourceStm?> OwnerMappingSources { get; } =
            [.. Enum.GetValues<OwnerMappingSourceStm>()
                .Where(source => source != OwnerMappingSourceStm.Manual)
                .Cast<OwnerMappingSourceStm?>()];

        /// <summary>
        /// Currently selected mapping source.
        /// </summary>
        public OwnerMappingSourceStm? SelectedSource { get; set; }

        /// <summary>
        /// Owner keys of the custom field mapping.
        /// </summary>
        public List<string> OwnerKeys { get; set; } = [];

        /// <summary>
        /// Owner keys added in the editor but not applied yet.
        /// </summary>
        public List<string> OwnerKeysToAdd { get; set; } = [];

        /// <summary>
        /// Owner keys deleted in the editor but not applied yet.
        /// </summary>
        public List<string> OwnerKeysToDelete { get; set; } = [];

        /// <summary>
        /// Owner key currently typed into the editor.
        /// </summary>
        public string ActiveOwnerKey { get; set; } = "";

        /// <summary>
        /// Marker of the name field mapping.
        /// </summary>
        public string ModelledMarker { get; set; } = "";

        private string rawOwnerKeys = "";
        private bool ownerKeysReadable = true;

        /// <summary>
        /// Reads the mapping source settings from the given configuration.
        /// </summary>
        /// <param name="configData">Configuration to read from.</param>
        public void Init(ConfigData configData)
        {
            SelectedSource = Enum.IsDefined(typeof(OwnerMappingSourceStm), configData.OwnerSoruceMappingID)
                ? (OwnerMappingSourceStm)configData.OwnerSoruceMappingID
                : OwnerMappingSourceStm.Disabled;
            rawOwnerKeys = configData.CustomFieldOwnerKey ?? "";
            OwnerKeys = DeserializeOwnerKeys(rawOwnerKeys, out ownerKeysReadable);
            OwnerKeysToAdd = [];
            OwnerKeysToDelete = [];
            ActiveOwnerKey = "";
            ModelledMarker = configData.ModModelledMarker ?? "";
        }

        /// <summary>
        /// Selects a mapping source and drops the owner key edits which were not applied yet, so edits made in an
        /// editor section which is no longer displayed cannot be saved without the user seeing them.
        /// </summary>
        /// <param name="source">Mapping source to select.</param>
        public void SelectSource(OwnerMappingSourceStm? source)
        {
            SelectedSource = source;
            OwnerKeysToAdd = [];
            OwnerKeysToDelete = [];
            ActiveOwnerKey = "";
        }

        /// <summary>
        /// Queues the owner key currently typed into the editor, or reverts its pending deletion when it is
        /// a configured key which is only marked for removal.
        /// </summary>
        /// <returns>The text key of the error to display, or <see langword="null"/> when the key was queued.</returns>
        public string? AddOwnerKey()
        {
            string key = ActiveOwnerKey.Trim();
            if (key.Length == 0)
            {
                return kNoOwnerKeyError;
            }
            // the key stays configured until the settings are saved, so re-adding it undoes the pending deletion
            if (OwnerKeysToDelete.Remove(key))
            {
                ActiveOwnerKey = "";
                return null;
            }
            if (OwnerKeys.Contains(key) || OwnerKeysToAdd.Contains(key))
            {
                return kDuplicateOwnerKeyError;
            }
            OwnerKeysToAdd.Add(key);
            ActiveOwnerKey = "";
            return null;
        }

        /// <summary>
        /// Checks whether the mapping source settings can be persisted and applies the pending owner key edits
        /// only when they can, so a rejected save leaves the editor showing the stored settings.
        /// </summary>
        /// <returns>The text key of the error to display, or <see langword="null"/> when the settings are valid.</returns>
        public string? Validate()
        {
            if (SelectedSource == null)
            {
                return kNoSourceSelectedError;
            }

            List<string> editedOwnerKeys = BuildOwnerKeysAfterPendingEdits();
            if (SelectedSource == OwnerMappingSourceStm.CustomField && editedOwnerKeys.Count == 0)
            {
                return kNoOwnerKeyError;
            }
            ApplyPendingOwnerKeys(editedOwnerKeys);
            return null;
        }

        /// <summary>
        /// Writes the edited mapping source settings into the given configuration.
        /// </summary>
        /// <param name="configData">Configuration to write to.</param>
        /// <returns>True if the change requires a full rule owner mapping rebuild.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="Validate"/> did not succeed before.</exception>
        public bool ApplyTo(ConfigData configData)
        {
            if (SelectedSource == null)
            {
                throw new InvalidOperationException($"{nameof(Validate)} has to succeed before {nameof(ApplyTo)} is called.");
            }

            int oldSource = configData.OwnerSoruceMappingID;
            string oldOwnerKeys = configData.CustomFieldOwnerKey ?? "";
            string oldModelledMarker = configData.ModModelledMarker ?? "";

            configData.OwnerSoruceMappingID = (int)SelectedSource.Value;
            // an unreadable stored value has to survive saving a setting which does not use it
            configData.CustomFieldOwnerKey = ownerKeysReadable || OwnerKeys.Count > 0
                ? JsonSerializer.Serialize(OwnerKeys)
                : rawOwnerKeys;
            configData.ModModelledMarker = ModelledMarker;

            return NeedsRuleOwnerReinitialize(oldSource, oldOwnerKeys, oldModelledMarker, configData);
        }

        /// <summary>
        /// Decides whether the saved settings require a full rule owner mapping rebuild.
        /// </summary>
        /// <param name="oldSource">Mapping source before the change.</param>
        /// <param name="oldOwnerKeys">Serialized owner keys before the change.</param>
        /// <param name="oldModelledMarker">Name field marker before the change.</param>
        /// <param name="configData">Configuration holding the saved settings.</param>
        /// <returns>True if a rebuild is required.</returns>
        private static bool NeedsRuleOwnerReinitialize(int oldSource, string oldOwnerKeys, string oldModelledMarker, ConfigData configData)
        {
            if (oldSource != configData.OwnerSoruceMappingID)
            {
                return true;
            }

            if (configData.OwnerSoruceMappingID == (int)OwnerMappingSourceStm.CustomField && oldOwnerKeys != configData.CustomFieldOwnerKey)
            {
                return true;
            }

            return configData.OwnerSoruceMappingID == (int)OwnerMappingSourceStm.NameField && oldModelledMarker != configData.ModModelledMarker;
        }

        /// <summary>
        /// Builds the owner key list resulting from the queued additions and removals without changing the editor state.
        /// </summary>
        /// <returns>The owner keys as they are once the queued edits are applied.</returns>
        private List<string> BuildOwnerKeysAfterPendingEdits()
        {
            List<string> editedOwnerKeys = [.. OwnerKeys];
            foreach (string key in OwnerKeysToDelete)
            {
                editedOwnerKeys.Remove(key);
            }
            editedOwnerKeys.AddRange(OwnerKeysToAdd);
            return editedOwnerKeys;
        }

        /// <summary>
        /// Commits the owner keys queued for addition and removal to the editor list.
        /// </summary>
        /// <param name="editedOwnerKeys">Owner keys resulting from the queued edits.</param>
        private void ApplyPendingOwnerKeys(List<string> editedOwnerKeys)
        {
            OwnerKeys = editedOwnerKeys;
            OwnerKeysToDelete = [];
            OwnerKeysToAdd = [];
        }

        /// <summary>
        /// Reads a JSON key list while retaining compatibility with legacy single-key values.
        /// </summary>
        /// <param name="keysJson">JSON list or legacy plain-text key.</param>
        /// <param name="readable">True if the stored value could be read, false if it had to be discarded.</param>
        /// <returns>The configured keys, or an empty list if the stored value cannot be parsed.</returns>
        private static List<string> DeserializeOwnerKeys(string keysJson, out bool readable)
        {
            readable = true;
            if (string.IsNullOrWhiteSpace(keysJson))
            {
                return [];
            }

            string trimmedKeys = keysJson.Trim();
            if (!trimmedKeys.StartsWith('[') || !trimmedKeys.EndsWith(']'))
            {
                return [trimmedKeys];
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(trimmedKeys) ?? [];
            }
            catch (JsonException exception)
            {
                // keep the settings page usable so the invalid value can be corrected here
                readable = false;
                Log.WriteWarning("Read Config", $"Config item \"CustomFieldOwnerKey\" contains unsupported value \"{keysJson}\". Using empty key list. {exception.Message}");
                return [];
            }
        }
    }
}
