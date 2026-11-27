using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Labelling;

namespace FWO.Services.Labelling
{
    /// <summary>
    /// Owner whose App label is wanted.
    /// </summary>
    /// <param name="Id">Id of the owner.</param>
    /// <param name="Name">Name of the owner.</param>
    /// <param name="AppId">External app id of the owner (from the CMDB), null or empty if it has none.</param>
    public sealed record AppLabelOwner(int Id, string? Name, string? AppId);

    /// <summary>
    /// Maintains the App labels: the configured App key, its values (the app id of an owner from the CMDB, or name and
    /// app id in the configured format, see <see cref="LabelValueFormat"/>) and the App label of the app servers of an
    /// owner. Every App label value records its owner, which decides which modellers may assign it.
    /// </summary>
    /// <remarks>
    /// The App key is an imported key (its values come from the CMDB), global and allows several values. As label keys
    /// are upserted elsewhere without changing the flags of an existing key, the flags are set here on every use - also
    /// for a key created before, e.g. by the Guardicore importer.
    /// </remarks>
    /// <param name="apiConnection">Api connection with write access to the labelling tables.</param>
    /// <param name="keyName">The configured name of the App key, empty to switch App labels off.</param>
    /// <param name="valueFormat">The configured format of the App label values, empty for the bare app id.</param>
    public class AppLabelHandler(ApiConnection apiConnection, string? keyName, string? valueFormat = null)
    {
        private readonly Dictionary<int, long> labelIdsByOwnerId = [];
        private int? keyId;

        /// <summary>
        /// The configured name of the App key, trimmed.
        /// </summary>
        public string KeyName { get; } = keyName?.Trim() ?? "";

        /// <summary>
        /// The configured format of the App label values.
        /// </summary>
        public LabelValueFormat ValueFormat { get; } = new(valueFormat);

        /// <summary>
        /// False if no App key is configured, then no App labels are written.
        /// </summary>
        public bool Enabled => KeyName.Length > 0;

        /// <summary>
        /// True if the given label key is the App key, which only FWO assigns.
        /// </summary>
        /// <param name="labelKey">Name of a label key.</param>
        public bool IsAppKey(string labelKey)
        {
            return Enabled && string.Equals(labelKey.Trim(), KeyName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the label id of the App label of every given owner, creating missing App labels and recording the owner
        /// of the values. Owners without app id are skipped.
        /// </summary>
        /// <param name="owners">Owners with name and external app id.</param>
        /// <returns>The label ids by owner id.</returns>
        public async Task<Dictionary<int, long>> GetAppLabelIds(IEnumerable<AppLabelOwner> owners)
        {
            List<AppLabelOwner> unknownOwners = [.. owners
                .Where(owner => !string.IsNullOrWhiteSpace(owner.AppId) && !labelIdsByOwnerId.ContainsKey(owner.Id))
                .DistinctBy(owner => owner.Id)];
            if (unknownOwners.Count > 0)
            {
                int appKeyId = await GetKeyId();
                var values = unknownOwners.Select(owner => new
                {
                    key_id = appKeyId,
                    value = ValueFormat.Value(owner.Name, owner.AppId!),
                    is_imported = true,
                    owner_id = owner.Id
                }).ToList();
                LabelValueUpsertResult result = await apiConnection.SendQueryAsync<LabelValueUpsertResult>(LabellingQueries.upsertAppLabelValues, new { values });
                foreach (LabelValue label in result.Returning.Where(label => label.OwnerId != null))
                {
                    labelIdsByOwnerId[label.OwnerId!.Value] = label.Id;
                }
            }
            return labelIdsByOwnerId;
        }

        /// <summary>
        /// Brings the App labels of all active app servers of an owner in line with the App label of the owner: missing
        /// App labels are added, other App labels (e.g. of a former app id, name or value format) are closed. Without an
        /// app id all App labels of the app servers are closed.
        /// </summary>
        /// <param name="ownerId">Id of the owner.</param>
        /// <param name="getChangeEventId">Returns the label change event the changes are recorded with.</param>
        /// <returns>The number of added and closed assignments.</returns>
        public async Task<int> SyncAppServers(int ownerId, Func<Task<long>> getChangeEventId)
        {
            if (!Enabled)
            {
                return 0;
            }
            List<OwnerAppServerAppLabels> owners = await apiConnection.SendQueryAsync<List<OwnerAppServerAppLabels>>(
                LabellingQueries.getOwnerAppServerAppLabels, new { ownerId, keyName = KeyName });
            OwnerAppServerAppLabels? owner = owners.FirstOrDefault();
            if (owner == null || owner.AppServers.Count == 0)
            {
                return 0;
            }
            long? appLabelId = await GetOwnerAppLabelId(new AppLabelOwner(ownerId, owner.Name, owner.AppIdExternal));
            int changes = 0;
            foreach (AppServerAppLabels appServer in owner.AppServers)
            {
                changes += await SyncAppServer(appServer, appLabelId, getChangeEventId);
            }
            return changes;
        }

        private async Task<long?> GetOwnerAppLabelId(AppLabelOwner owner)
        {
            if (string.IsNullOrWhiteSpace(owner.AppId))
            {
                return null;
            }
            List<AppLabelOwner> owners = [owner];
            Dictionary<int, long> appLabelIds = await GetAppLabelIds(owners);
            return appLabelIds.TryGetValue(owner.Id, out long labelId) ? labelId : null;
        }

        private async Task<int> SyncAppServer(AppServerAppLabels appServer, long? appLabelId, Func<Task<long>> getChangeEventId)
        {
            List<long> labelIdsToClose = [.. appServer.AppLabels.Select(label => label.LabelId).Where(labelId => labelId != appLabelId).Distinct()];
            bool addAppLabel = appLabelId != null && !appServer.AppLabels.Exists(label => label.LabelId == appLabelId);
            if (labelIdsToClose.Count == 0 && !addAppLabel)
            {
                return 0;
            }
            long eventId = await getChangeEventId();
            if (labelIdsToClose.Count > 0)
            {
                await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.removeOwnerNetworkLabels,
                    new { ownerNetworkId = appServer.Id, labelIds = labelIdsToClose, removedEventId = eventId });
            }
            if (addAppLabel)
            {
                List<object> labels = [new { owner_network_id = appServer.Id, label_id = appLabelId!.Value, created_event_id = eventId }];
                await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.addOwnerNetworkLabels, new { labels });
            }
            return labelIdsToClose.Count + (addAppLabel ? 1 : 0);
        }

        /// <summary>
        /// Creates a label change event.
        /// </summary>
        /// <param name="importControlId">Import control the change belongs to, null for changes from the UI.</param>
        /// <param name="originIsUi">True if the change stems from the UI.</param>
        /// <returns>The id of the event.</returns>
        public async Task<long> CreateChangeEvent(long? importControlId, bool originIsUi)
        {
            ReturnId changeEvent = await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.addLabelChangeEvent, new { importControlId, originIsUi });
            return changeEvent.Id ?? throw new InvalidOperationException("No label change event id returned.");
        }

        private async Task<int> GetKeyId()
        {
            if (keyId == null)
            {
                LabelKey key = await apiConnection.SendQueryAsync<LabelKey>(LabellingQueries.upsertAppLabelKey, new { keyName = KeyName });
                keyId = key.Id;
            }
            return keyId.Value;
        }
    }
}
