using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Labelling;
using FWO.Logging;

namespace FWO.Services.Labelling
{
    /// <summary>
    /// Writes the labels of imported app servers into labelling.owner_network_label.
    /// </summary>
    /// <remarks>
    /// The assignments are diffed against the active ones: missing labels are added, labels no longer imported are
    /// closed by a removal event. All changes of an import run share one label change event, which belongs to the
    /// import control of the run. Unknown keys and values are created as imported. Labels are rejected (and logged)
    /// if they exceed the limits, use a key that stays a group (AppRole, AppZone) or the App key, or give a key that
    /// allows a single value only several values on one app server. The App label is not imported: every app server
    /// gets the App label of its owner (the app id from the CMDB), see <see cref="SyncOwnerAppLabels"/>.
    /// </remarks>
    public class AppServerLabelImport
    {
        /// <summary>
        /// Maximum number of labels per app server, further labels are skipped.
        /// </summary>
        public const int kMaxLabelsPerAppServer = 50;

        /// <summary>
        /// Maximum length of a label key.
        /// </summary>
        public const int kMaxLabelKeyLength = 100;

        /// <summary>
        /// Maximum length of a label value.
        /// </summary>
        public const int kMaxLabelValueLength = 250;

        private const string LogMessageTitle = "Import App Data Labels";
        private const string LevelAppServer = "App Server";
        private const int kSeverityWarning = 1;
        private const string kLabelValueConstraint = "labelling_label_value_key_id_value_unique";
        private static readonly List<string> kLabelValueUpdateColumns = ["value"];
        private static readonly List<long> kNoLabelIds = [];
        private static readonly HashSet<string> kRejectedKeys = new(StringComparer.OrdinalIgnoreCase) { "AppRole", "AppZone" };

        private readonly ApiConnection apiConnection;
        private readonly Func<Task<long?>> getImportControlId;
        private readonly bool originIsUi;
        private readonly Func<int, string, string, Task> addLogEntry;
        private readonly Dictionary<(string key, string value), long> labelIds = [];
        private Dictionary<string, LabelKey>? keysByName;
        private Dictionary<long, HashSet<long>> activeLabelIdsByAppServer = [];
        private Dictionary<long, HashSet<long>> activeAppLabelIdsByAppServer = [];
        private readonly AppLabelHandler appLabels;
        private long? changeEventId;

        /// <summary>
        /// False if no App key is configured, then no App labels are written.
        /// </summary>
        public bool AppLabelsEnabled => appLabels.Enabled;

        /// <summary>
        /// True if labels of an app server were added or closed since the active labels were loaded last.
        /// </summary>
        public bool LabelsChanged { get; private set; }

        /// <summary>
        /// Constructor for the app server label import of one import run.
        /// </summary>
        /// <param name="apiConnection">Api connection.</param>
        /// <param name="getImportControlId">Returns the import control the label changes belong to, null for changes from the UI.</param>
        /// <param name="originIsUi">True if the labels are imported in the UI (csv upload).</param>
        /// <param name="addLogEntry">Writes a data import log entry (severity, level, description).</param>
        /// <param name="appLabelKey">Configured name of the App key, empty to switch App labels off.</param>
        public AppServerLabelImport(ApiConnection apiConnection, Func<Task<long?>> getImportControlId, bool originIsUi, Func<int, string, string, Task> addLogEntry,
            string? appLabelKey = null)
        {
            this.apiConnection = apiConnection;
            this.getImportControlId = getImportControlId;
            this.originIsUi = originIsUi;
            this.addLogEntry = addLogEntry;
            appLabels = new(apiConnection, appLabelKey);
        }

        /// <summary>
        /// Loads the active labels of the given app servers and resets LabelsChanged.
        /// </summary>
        /// <param name="appServerIds">Ids of the existing app servers of an app.</param>
        public async Task LoadActiveLabels(IEnumerable<long> appServerIds)
        {
            LabelsChanged = false;
            activeLabelIdsByAppServer = [];
            activeAppLabelIdsByAppServer = [];
            List<long> ids = [.. appServerIds];
            if (ids.Count == 0)
            {
                return;
            }
            List<OwnerNetworkLabel> activeLabels = await apiConnection.SendQueryAsync<List<OwnerNetworkLabel>>(
                LabellingQueries.getOwnerNetworkLabels, new { ownerNetworkIds = ids });
            foreach (OwnerNetworkLabel label in activeLabels)
            {
                // App labels are not imported, so the imported labels must not close them
                HashSet<long> labelIdsOfKind = appLabels.IsAppKey(label.Label?.Key.Name ?? "")
                    ? LabelIdsOf(activeAppLabelIdsByAppServer, label.OwnerNetworkId)
                    : ActiveLabelIds(label.OwnerNetworkId);
                labelIdsOfKind.Add(label.LabelId);
            }
        }

        /// <summary>
        /// Brings the labels of an app server in line with the imported labels. Errors are logged, not thrown.
        /// </summary>
        /// <param name="appServerId">Id of the app server.</param>
        /// <param name="appServerName">Name of the app server for log messages.</param>
        /// <param name="importedLabels">Imported labels, null leaves the labels untouched.</param>
        public async Task SyncLabels(long appServerId, string appServerName, List<ModellingImportLabel>? importedLabels)
        {
            if (importedLabels == null)
            {
                return;
            }
            try
            {
                List<(string key, string value)> acceptedLabels = await ValidateLabels(appServerName, importedLabels);
                HashSet<long> wantedLabelIds = await ResolveLabelIds(acceptedLabels);
                HashSet<long> activeLabelIds = ActiveLabelIds(appServerId);
                List<long> labelIdsToAdd = [.. wantedLabelIds.Except(activeLabelIds)];
                List<long> labelIdsToClose = [.. activeLabelIds.Except(wantedLabelIds)];
                await ApplyChanges(appServerId, labelIdsToAdd, labelIdsToClose);
            }
            catch (Exception exception)
            {
                await LogError($"Labels of app server {appServerName} could not be imported.", exception);
            }
        }

        /// <summary>
        /// Closes all active labels of an app server, e.g. when it is marked as deleted. Errors are logged, not thrown.
        /// </summary>
        /// <param name="appServerId">Id of the app server.</param>
        /// <param name="appServerName">Name of the app server for log messages.</param>
        public async Task CloseLabels(long appServerId, string appServerName)
        {
            try
            {
                List<long> labelIdsToClose = [.. ActiveLabelIds(appServerId), .. LabelIdsOf(activeAppLabelIdsByAppServer, appServerId)];
                await ApplyChanges(appServerId, kNoLabelIds, labelIdsToClose);
                LabelIdsOf(activeAppLabelIdsByAppServer, appServerId).Clear();
            }
            catch (Exception exception)
            {
                await LogError($"Labels of app server {appServerName} could not be closed.", exception);
            }
        }

        /// <summary>
        /// Gives all active app servers of an owner the App label of the owner and closes other App labels.
        /// Errors are logged, not thrown.
        /// </summary>
        /// <param name="ownerId">Id of the owner.</param>
        /// <param name="ownerName">Name of the owner for log messages.</param>
        public async Task SyncOwnerAppLabels(int ownerId, string ownerName)
        {
            try
            {
                if (await appLabels.SyncAppServers(ownerId, GetChangeEventId) > 0)
                {
                    LabelsChanged = true;
                }
            }
            catch (Exception exception)
            {
                await LogError($"App labels of the app servers of {ownerName} could not be assigned.", exception);
            }
        }

        /// <summary>
        /// Gives the app servers of the active owners that were not part of the import files the App label of their
        /// owner, e.g. of owners created in the UI. This fills their App labels after the upgrade and repairs App labels
        /// that were missed since; imported owners were synced when they were saved.
        /// </summary>
        /// <param name="importedOwnerIds">Owners saved by the import run, which are synced already.</param>
        /// <param name="cancellationToken">Stops before the next owner.</param>
        public async Task SyncOtherOwners(IReadOnlySet<int> importedOwnerIds, CancellationToken cancellationToken = default)
        {
            if (!AppLabelsEnabled)
            {
                return;
            }
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersWithNetworks);
            foreach (FwoOwner owner in owners.Where(owner => owner.Active && owner.OwnerNetworks.Length > 0 && !importedOwnerIds.Contains(owner.Id)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SyncOwnerAppLabels(owner.Id, owner.Name);
            }
        }

        /// <summary>
        /// Returns the importable labels of an app server, trimmed and without duplicates, and logs the rejected ones.
        /// </summary>
        /// <param name="appServerName">Name of the app server for log messages.</param>
        /// <param name="importedLabels">Imported labels.</param>
        /// <returns>The accepted labels as (key, value).</returns>
        public async Task<List<(string key, string value)>> ValidateLabels(string appServerName, List<ModellingImportLabel> importedLabels)
        {
            Dictionary<string, LabelKey> keys = await GetKeysByName();
            List<string> rejections = [];
            if (importedLabels.Count > kMaxLabelsPerAppServer)
            {
                rejections.Add($"more than {kMaxLabelsPerAppServer} labels, the remaining ones are skipped");
            }
            List<(string key, string value)> labels = [];
            foreach (ModellingImportLabel label in importedLabels.Take(kMaxLabelsPerAppServer))
            {
                string key = label.Key.Trim();
                string value = label.Value.Trim();
                string? rejection = appLabels.IsAppKey(key)
                    ? $"label key {key} is not importable, every app server gets the App label of its owner"
                    : GetRejection(key, value);
                if (rejection != null)
                {
                    rejections.Add(rejection);
                    continue;
                }
                key = keys.TryGetValue(key, out LabelKey? knownKey) ? knownKey.Name : key;
                if (!labels.Contains((key, value)))
                {
                    labels.Add((key, value));
                }
            }
            labels = RemoveMultipleValuesOfSingleValueKeys(labels, keys, rejections);
            foreach (string rejection in rejections)
            {
                await addLogEntry(kSeverityWarning, LevelAppServer, $"Label of app server {appServerName} rejected: {rejection}.");
            }
            return labels;
        }

        private static string? GetRejection(string key, string value)
        {
            if (key.Length == 0 || value.Length == 0)
            {
                return "empty label key or value";
            }
            if (key.Length > kMaxLabelKeyLength || value.Length > kMaxLabelValueLength)
            {
                return $"label key longer than {kMaxLabelKeyLength} or value longer than {kMaxLabelValueLength} characters";
            }
            if (kRejectedKeys.Contains(key))
            {
                return $"label key {key} is not importable, it is maintained as group";
            }
            return null;
        }

        private static List<(string key, string value)> RemoveMultipleValuesOfSingleValueKeys(
            List<(string key, string value)> labels, Dictionary<string, LabelKey> keys, List<string> rejections)
        {
            HashSet<string> keysWithMultipleValues = [.. labels
                .GroupBy(label => label.key)
                .Where(group => group.Count() > 1 && !(keys.TryGetValue(group.Key, out LabelKey? key) && key.AllowMultipleValues))
                .Select(group => group.Key)];
            foreach (string key in keysWithMultipleValues)
            {
                rejections.Add($"label key {key} allows a single value only, but several values were given");
            }
            return [.. labels.Where(label => !keysWithMultipleValues.Contains(label.key))];
        }

        private async Task<Dictionary<string, LabelKey>> GetKeysByName()
        {
            if (keysByName == null)
            {
                List<LabelKey> keys = await apiConnection.SendQueryAsync<List<LabelKey>>(LabellingQueries.getLabelKeys);
                keysByName = new(StringComparer.OrdinalIgnoreCase);
                foreach (LabelKey key in keys)
                {
                    keysByName.TryAdd(key.Name, key);
                }
            }
            return keysByName;
        }

        /// <summary>
        /// Returns the label ids of the given labels, creating unknown keys and values as imported.
        /// </summary>
        private async Task<HashSet<long>> ResolveLabelIds(List<(string key, string value)> labels)
        {
            List<(string key, string value)> unknownLabels = [.. labels.Where(label => !labelIds.ContainsKey(label))];
            if (unknownLabels.Count > 0)
            {
                await UpsertLabels(unknownLabels);
                await ReadLabelIds(unknownLabels);
            }
            HashSet<long> ids = [];
            foreach ((string key, string value) label in labels)
            {
                if (!labelIds.TryGetValue(label, out long labelId))
                {
                    throw new KeyNotFoundException($"Label {label.key}: {label.value} could not be created.");
                }
                ids.Add(labelId);
            }
            return ids;
        }

        private async Task UpsertLabels(List<(string key, string value)> labels)
        {
            var labelKeys = labels.GroupBy(label => label.key).Select(group => new
            {
                key_name = group.Key,
                key_source_internal = false,
                label_values = new
                {
                    data = group.Select(label => new { value = label.value, is_imported = true }).ToList(),
                    on_conflict = new { constraint = kLabelValueConstraint, update_columns = kLabelValueUpdateColumns }
                }
            }).ToList();
            await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.upsertImportedLabels, new { labelKeys });
        }

        private async Task ReadLabelIds(List<(string key, string value)> labels)
        {
            List<string> keyNames = [.. labels.Select(label => label.key).Distinct()];
            List<string> values = [.. labels.Select(label => label.value).Distinct()];
            List<LabelValue> labelValues = await apiConnection.SendQueryAsync<List<LabelValue>>(LabellingQueries.getLabelIds, new { keyNames, values });
            Dictionary<string, LabelKey> keys = await GetKeysByName();
            foreach (LabelValue labelValue in labelValues)
            {
                labelIds[(labelValue.Key.Name, labelValue.Value)] = labelValue.Id;
                keys.TryAdd(labelValue.Key.Name, labelValue.Key);
            }
        }

        private async Task ApplyChanges(long appServerId, List<long> labelIdsToAdd, List<long> labelIdsToClose)
        {
            if (labelIdsToAdd.Count == 0 && labelIdsToClose.Count == 0)
            {
                return;
            }
            long eventId = await GetChangeEventId();
            HashSet<long> activeLabelIds = ActiveLabelIds(appServerId);
            if (labelIdsToClose.Count > 0)
            {
                await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.removeOwnerNetworkLabels,
                    new { ownerNetworkId = appServerId, labelIds = labelIdsToClose, removedEventId = eventId });
                activeLabelIds.ExceptWith(labelIdsToClose);
            }
            if (labelIdsToAdd.Count > 0)
            {
                var labels = labelIdsToAdd.Select(labelId => new { owner_network_id = appServerId, label_id = labelId, created_event_id = eventId }).ToList();
                await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.addOwnerNetworkLabels, new { labels });
                activeLabelIds.UnionWith(labelIdsToAdd);
            }
            LabelsChanged = true;
        }

        private async Task<long> GetChangeEventId()
        {
            changeEventId ??= await appLabels.CreateChangeEvent(await getImportControlId(), originIsUi);
            return changeEventId.Value;
        }

        private HashSet<long> ActiveLabelIds(long appServerId)
        {
            return LabelIdsOf(activeLabelIdsByAppServer, appServerId);
        }

        private static HashSet<long> LabelIdsOf(Dictionary<long, HashSet<long>> labelIdsByAppServer, long appServerId)
        {
            if (!labelIdsByAppServer.TryGetValue(appServerId, out HashSet<long>? labelIds))
            {
                labelIds = [];
                labelIdsByAppServer[appServerId] = labelIds;
            }
            return labelIds;
        }

        private async Task LogError(string errorText, Exception exception)
        {
            Log.WriteError(LogMessageTitle, errorText, exception);
            await addLogEntry(kSeverityWarning, LevelAppServer, errorText);
        }
    }
}
