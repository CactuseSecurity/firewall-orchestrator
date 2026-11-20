using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;

namespace FWO.Services.Labelling
{
    /// <summary>
    /// Moves the App labels of the app servers to a changed App key: the App labels of the former key are closed and the
    /// app servers of all active owners get the App label of the new key. Without new key (App labels switched off) the
    /// former labels are only closed.
    /// </summary>
    /// <remarks>
    /// The labels of the former key cannot be told apart from other labels afterwards, as the App key is an ordinary
    /// imported key, so they have to be closed when the key changes. The App labels of the rules follow with the next run
    /// of the rule owner mapping (see <see cref="RuleAppLabelSync"/>), whose changes the rule reports place in time.
    /// </remarks>
    /// <param name="apiConnection">Api connection with write access to the labelling tables.</param>
    public class AppLabelKeyChange(ApiConnection apiConnection)
    {
        /// <summary>
        /// Moves the App labels of the app servers from the former to the new App key; nothing happens if the key is unchanged.
        /// </summary>
        /// <param name="previousKey">App key before the change, empty if App labels were switched off.</param>
        /// <param name="newKey">App key after the change, empty to switch App labels off.</param>
        /// <param name="addLogEntry">Reports labels the sync rejects (severity, level, description), like a data import log entry.</param>
        /// <param name="cancellationToken">Stops before the next owner.</param>
        /// <returns>True if the key changed.</returns>
        public async Task<bool> Apply(string? previousKey, string? newKey, Func<int, string, string, Task> addLogEntry, CancellationToken cancellationToken = default)
        {
            string formerKey = previousKey?.Trim() ?? "";
            string key = newKey?.Trim() ?? "";
            if (string.Equals(formerKey, key, StringComparison.Ordinal))
            {
                return false;
            }
            if (formerKey.Length > 0)
            {
                long removedEventId = await new AppLabelHandler(apiConnection, formerKey).CreateChangeEvent(null, true);
                await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.closeOwnerNetworkLabelsOfKey, new { keyName = formerKey, removedEventId });
            }
            if (key.Length > 0)
            {
                AppServerLabelImport labelImport = new(apiConnection, () => Task.FromResult<long?>(null), true, addLogEntry, key);
                await labelImport.SyncOtherOwners(new HashSet<int>(), cancellationToken);
            }
            return true;
        }
    }
}
