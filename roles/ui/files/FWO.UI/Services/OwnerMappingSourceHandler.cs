using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Services;
using FWO.Data.Enums;
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
        /// Currently selected mapping source. Only changeable through <see cref="SelectSource"/>.
        /// </summary>
        public OwnerMappingSourceStm? SelectedSource { get; private set; }

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

        /// <summary>
        /// Log levels offered for selection.
        /// </summary>
        public List<RuleOwnerMappingLogLevel?> LogLevels { get; } =
            [.. Enum.GetValues<RuleOwnerMappingLogLevel>().Cast<RuleOwnerMappingLogLevel?>()];

        /// <summary>
        /// How much detail about single unmappable rules is written to the log. Changing it never requires a
        /// rule owner rebuild, because it does not influence the mappings themselves.
        /// </summary>
        public RuleOwnerMappingLogLevel LogLevel { get; set; } = RuleOwnerMappingLogLevel.Warning;

        /// <summary>
        /// Settings changed by the last <see cref="ApplyTo"/> that require a rule owner rebuild. Passed on with
        /// the rebuild so its result can be told apart from a run where the incremental mapping failed. Kept
        /// until <see cref="ConfirmRuleOwnerRebuild"/> reports the rebuild as done, so a retry after a failed
        /// save still names what was changed.
        /// </summary>
        public List<RuleOwnerMappingChange> AppliedChanges { get; private set; } = [];

        private bool ruleOwnerRebuildPending;
        private RuleOwnerMappingLogLevel storedLogLevel = RuleOwnerMappingLogLevel.Warning;
        private bool appliedSettingsRequireRuleOwnerRebuild;
        private int storedSource;
        private string storedModelledMarker = "";
        private string rawOwnerKeys = "";
        private bool ownerKeysReadable = true;

        /// <summary>
        /// Reads the mapping source settings from the given configuration and shows them in the editor.
        /// </summary>
        /// <param name="configData">Configuration to read from.</param>
        public void Init(ConfigData configData)
        {
            TakeOverStoredSettings(configData);
            DiscardEdits();
        }

        /// <summary>
        /// Takes over the mapping source settings as they are stored in the database. Has to be called again
        /// after every successful write, because the editor compares against these settings to decide whether a
        /// full rule owner mapping rebuild is required, see <see cref="ApplyTo"/>. Taking the settings over is
        /// also what makes the rebuild they require outstanding, because a write which did not reach the database
        /// requires no rebuild.
        /// </summary>
        /// <param name="configData">Configuration holding the stored settings.</param>
        public void TakeOverStoredSettings(ConfigData configData)
        {
            storedSource = configData.OwnerSoruceMappingID;
            rawOwnerKeys = configData.CustomFieldOwnerKey ?? "";
            storedModelledMarker = configData.ModModelledMarker ?? "";
            storedLogLevel = configData.RuleOwnerMappingLogLevel;
            ruleOwnerRebuildPending |= appliedSettingsRequireRuleOwnerRebuild;
            appliedSettingsRequireRuleOwnerRebuild = false;
        }

        /// <summary>
        /// Drops every edit which was not written to the database and shows the stored settings again.
        /// </summary>
        public void DiscardEdits()
        {
            SelectSource(Enum.IsDefined(typeof(OwnerMappingSourceStm), storedSource)
                ? (OwnerMappingSourceStm)storedSource
                : OwnerMappingSourceStm.Disabled);
            OwnerKeys = DeserializeOwnerKeys(rawOwnerKeys, out ownerKeysReadable);
            OwnerKeysToAdd = [];
            OwnerKeysToDelete = [];
            ModelledMarker = storedModelledMarker;
            LogLevel = storedLogLevel;
        }

        /// <summary>
        /// Selects a mapping source. The owner keys queued for addition and removal are kept, so a source
        /// round trip does not lose them; only the key typed into the editor of the section being left is dropped.
        /// The settings of a source which is not selected are left as they are stored, see <see cref="Validate"/>
        /// and <see cref="ApplyTo"/>.
        /// </summary>
        /// <param name="source">Mapping source to select.</param>
        public void SelectSource(OwnerMappingSourceStm? source)
        {
            SelectedSource = source;
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
        /// only when they can, so a rejected save leaves the editor showing the stored settings. Key edits of the
        /// custom field section are only committed while that section is displayed.
        /// </summary>
        /// <returns>The text key of the error to display, or <see langword="null"/> when the settings are valid.</returns>
        public string? Validate()
        {
            if (SelectedSource == null)
            {
                return kNoSourceSelectedError;
            }
            if (SelectedSource != OwnerMappingSourceStm.CustomField)
            {
                return null;
            }

            List<string> editedOwnerKeys = BuildOwnerKeysAfterPendingEdits();
            if (editedOwnerKeys.Count == 0)
            {
                return kNoOwnerKeyError;
            }
            ApplyPendingOwnerKeys(editedOwnerKeys);
            return null;
        }

        /// <summary>
        /// Writes the edited mapping source settings into the given configuration, restoring the settings of the
        /// sources which are not selected. Everything is compared against the stored settings taken over by
        /// <see cref="TakeOverStoredSettings"/>, so a save attempt following a failed one neither loses the rebuild
        /// the change requires nor keeps what the failed attempt wrote into the configuration.
        /// </summary>
        /// <param name="configData">Configuration to write to.</param>
        /// <returns>True if a full rule owner mapping rebuild is required. Once the settings requiring it are
        /// stored, see <see cref="TakeOverStoredSettings"/>, this stays true until
        /// <see cref="ConfirmRuleOwnerRebuild"/> reports the rebuild as done.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="Validate"/> did not succeed before.</exception>
        public bool ApplyTo(ConfigData configData)
        {
            if (SelectedSource == null)
            {
                throw new InvalidOperationException($"{nameof(Validate)} has to succeed before {nameof(ApplyTo)} is called.");
            }

            configData.OwnerSoruceMappingID = (int)SelectedSource.Value;
            // every section writes its own setting only and the settings of the other sections are restored from
            // the stored ones, so neither an abandoned edit nor a value a failed save attempt left in the given
            // configuration can reach the database, which is written as a whole
            configData.CustomFieldOwnerKey = SelectedSource == OwnerMappingSourceStm.CustomField
                ? SerializeOwnerKeys()
                : rawOwnerKeys;
            configData.ModModelledMarker = SelectedSource == OwnerMappingSourceStm.NameField
                ? ModelledMarker
                : storedModelledMarker;

            // the log level applies to every source and never changes the mappings, so it is not part of the
            // comparison deciding whether a rule owner rebuild is required
            configData.RuleOwnerMappingLogLevel = LogLevel;

            // the stored settings are compared, not the ones of the given configuration: a retry after a failed
            // write would otherwise compare the configuration the previous attempt already changed against itself
            // the requirement is only remembered once the settings are stored, so a write which failed leaves no
            // rebuild outstanding for settings the database never received
            List<RuleOwnerMappingChange> changes = CollectChanges(storedSource, rawOwnerKeys, storedModelledMarker, configData);
            appliedSettingsRequireRuleOwnerRebuild = changes.Count > 0;
            if (changes.Count > 0)
            {
                AppliedChanges = changes;
            }
            return ruleOwnerRebuildPending || appliedSettingsRequireRuleOwnerRebuild;
        }

        /// <summary>
        /// Reports the rule owner mappings as rebuilt. Until this is called, every save keeps requesting the
        /// rebuild of the settings already stored, so a rebuild which did not succeed is not forgotten by the
        /// following save.
        /// </summary>
        public void ConfirmRuleOwnerRebuild()
        {
            ruleOwnerRebuildPending = false;
            AppliedChanges = [];
        }

        /// <summary>
        /// Collects the saved settings that require a full rule owner mapping rebuild. An empty result means
        /// nothing mapping-relevant was changed, so it also answers whether a rebuild is needed at all.
        /// </summary>
        /// <param name="oldSource">Mapping source before the change.</param>
        /// <param name="oldOwnerKeys">Serialized owner keys before the change.</param>
        /// <param name="oldModelledMarker">Name field marker before the change.</param>
        /// <param name="configData">Configuration holding the saved settings.</param>
        /// <returns>The mapping-relevant changes, newest state first.</returns>
        private static List<RuleOwnerMappingChange> CollectChanges(int oldSource, string oldOwnerKeys, string oldModelledMarker, ConfigData configData)
        {
            List<RuleOwnerMappingChange> changes = [];

            if (oldSource != configData.OwnerSoruceMappingID)
            {
                changes.Add(new RuleOwnerMappingChange
                {
                    Setting = RuleOwnerMappingChangeSetting.kSource,
                    From = DescribeSource(oldSource),
                    To = DescribeSource(configData.OwnerSoruceMappingID)
                });
            }

            if (configData.OwnerSoruceMappingID == (int)OwnerMappingSourceStm.CustomField && oldOwnerKeys != configData.CustomFieldOwnerKey)
            {
                changes.Add(new RuleOwnerMappingChange
                {
                    Setting = RuleOwnerMappingChangeSetting.kCustomFieldKeys,
                    From = oldOwnerKeys,
                    To = configData.CustomFieldOwnerKey ?? ""
                });
            }

            if (configData.OwnerSoruceMappingID == (int)OwnerMappingSourceStm.NameField && oldModelledMarker != configData.ModModelledMarker)
            {
                changes.Add(new RuleOwnerMappingChange
                {
                    Setting = RuleOwnerMappingChangeSetting.kMarker,
                    From = oldModelledMarker,
                    To = configData.ModModelledMarker ?? ""
                });
            }

            return changes;
        }

        /// <summary>
        /// Names a mapping source by its enum name, which the display resolves to a localized text.
        /// </summary>
        /// <param name="source">Stored mapping source id.</param>
        /// <returns>The enum name, or the raw id when it is not a known source.</returns>
        private static string DescribeSource(int source)
        {
            return Enum.IsDefined(typeof(OwnerMappingSourceStm), source)
                ? ((OwnerMappingSourceStm)source).ToString()
                : source.ToString();
        }

        /// <summary>
        /// Serializes the owner keys shown in the editor, keeping an unreadable stored value which was not replaced.
        /// </summary>
        /// <returns>The owner key value to store.</returns>
        private string SerializeOwnerKeys()
        {
            return ownerKeysReadable || OwnerKeys.Count > 0 ? JsonSerializer.Serialize(OwnerKeys) : rawOwnerKeys;
        }

        /// <summary>
        /// Builds the owner key list resulting from the queued additions and removals without changing the editor state.
        /// </summary>
        /// <returns>The owner keys as they are once the queued edits are applied.</returns>
        private List<string> BuildOwnerKeysAfterPendingEdits()
        {
            // the editor marks every row carrying a deleted key, so a key stored twice has to disappear completely
            List<string> editedOwnerKeys = [.. OwnerKeys.Where(key => !OwnerKeysToDelete.Contains(key))];
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
