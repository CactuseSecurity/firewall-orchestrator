using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Labelling;

namespace FWO.Services.Labelling
{
    /// <summary>
    /// Settings of the App labels.
    /// </summary>
    /// <param name="Key">Name of the App key, empty if App labels are switched off.</param>
    /// <param name="ValueFormat">Format of the App label values, see <see cref="LabelValueFormat"/>.</param>
    public sealed record AppLabelSettings(string? Key, string? ValueFormat)
    {
        /// <summary>The App key, trimmed.</summary>
        public string TrimmedKey => Key?.Trim() ?? "";

        /// <summary>The value format in effect.</summary>
        public string EffectiveValueFormat => new LabelValueFormat(ValueFormat).Format;
    }

    /// <summary>
    /// Moves the App labels of the app servers to a changed App key or value format: the App labels of a former key are
    /// closed and the app servers of all active owners get the App label of the new key and format, which closes their
    /// App labels of a former format. Without new key (App labels switched off) the former labels are only closed.
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
        /// Moves the App labels of the app servers from the former to the new App key or value format; nothing happens if
        /// neither changed.
        /// </summary>
        /// <param name="previous">App label settings before the change.</param>
        /// <param name="current">App label settings after the change.</param>
        /// <param name="addLogEntry">Reports labels the sync rejects (severity, level, description), like a data import log entry.</param>
        /// <param name="cancellationToken">Stops before the next owner.</param>
        /// <returns>True if the key or the value format changed.</returns>
        public async Task<bool> Apply(AppLabelSettings previous, AppLabelSettings current, Func<int, string, string, Task> addLogEntry,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(previous);
            ArgumentNullException.ThrowIfNull(current);
            string formerKey = previous.TrimmedKey;
            string key = current.TrimmedKey;
            bool keyChanged = !string.Equals(formerKey, key, StringComparison.Ordinal);
            bool formatChanged = !string.Equals(previous.EffectiveValueFormat, current.EffectiveValueFormat, StringComparison.Ordinal);
            if (!keyChanged && !formatChanged)
            {
                return false;
            }
            if (keyChanged && formerKey.Length > 0)
            {
                long removedEventId = await new AppLabelHandler(apiConnection, formerKey).CreateChangeEvent(null, true);
                await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.closeOwnerNetworkLabelsOfKey, new { keyName = formerKey, removedEventId });
            }
            if (key.Length > 0)
            {
                AppServerLabelImport labelImport = new(apiConnection, () => Task.FromResult<long?>(null), true, addLogEntry, key, current.ValueFormat);
                await labelImport.SyncOtherOwners(new HashSet<int>(), cancellationToken);
            }
            return true;
        }
    }
}
