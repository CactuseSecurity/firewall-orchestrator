using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Labelling;
using FWO.Data.Modelling;

namespace FWO.Services.Modelling
{
    /// <summary>
    /// Labels as source and destination of a connection: the labels of an AND-group are ANDed, the groups of a side and
    /// the objects of the side are ORed.
    /// </summary>
    public partial class ModellingConnectionHandler
    {
        private static readonly List<string> kGroupLabelKeys = ["AppRole", "AppZone"];
        private List<ConnectionLabel> storedSrcLabels = [];
        private List<ConnectionLabel> storedDstLabels = [];

        /// <summary>
        /// The AND-groups of the source labels as edited.
        /// </summary>
        public List<List<ConnectionLabel>> SrcLabelGroups { get; private set; } = [];

        /// <summary>
        /// The AND-groups of the destination labels as edited.
        /// </summary>
        public List<List<ConnectionLabel>> DstLabelGroups { get; private set; } = [];

        /// <summary>
        /// The labels the user may assign: all labels except those of AppRole and AppZone, App labels only of own apps.
        /// </summary>
        public List<LabelValue> SelectableLabels { get; private set; } = [];

        /// <summary>
        /// True if labels may be edited: label-based modelling is switched on and the connection is not read-only.
        /// Otherwise assigned labels are only shown.
        /// </summary>
        public bool LabelEditingEnabled => userConfig.LabelBasedModelling && !ReadOnly;

        /// <summary>
        /// Takes over the labels of the connection as stored and as edited.
        /// </summary>
        public void LoadStoredLabels()
        {
            storedSrcLabels = [.. ActConn.SourceLabels];
            storedDstLabels = [.. ActConn.DestinationLabels];
            RefreshLabelGroups();
        }

        /// <summary>
        /// Shows the labels of the connection (e.g. of a used interface) as edited, without changing the stored state.
        /// </summary>
        public void RefreshLabelGroups()
        {
            SrcLabelGroups = ConnectionLabelFormatter.GroupLabels(ActConn.SourceLabels);
            DstLabelGroups = ConnectionLabelFormatter.GroupLabels(ActConn.DestinationLabels);
        }

        /// <summary>
        /// Returns the edited AND-groups of a side.
        /// </summary>
        public List<List<ConnectionLabel>> LabelGroups(bool isSource)
        {
            return isSource ? SrcLabelGroups : DstLabelGroups;
        }

        /// <summary>
        /// Loads the labels the user may assign.
        /// </summary>
        public async Task LoadSelectableLabels()
        {
            try
            {
                List<LabelValue> labels = await apiConnection.SendQueryAsync<List<LabelValue>>(LabellingQueries.getModellingLabels,
                    new { excludedKeys = kGroupLabelKeys });
                SelectableLabels = [.. labels.Where(IsSelectable)];
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        /// <summary>
        /// True if the user may assign the label: not of a key that stays a group, and an App label only of an own app.
        /// </summary>
        public bool IsSelectable(LabelValue label)
        {
            if (kGroupLabelKeys.Contains(label.Key.Name, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
            bool isAppLabel = string.Equals(label.Key.Name, userConfig.AppLabelKey.Trim(), StringComparison.OrdinalIgnoreCase);
            return !isAppLabel || userConfig.User.Roles.Contains(Roles.Admin) || OwnAppIds().Contains(label.Value);
        }

        private HashSet<string> OwnAppIds()
        {
            return [.. AllApps.Where(app => userConfig.User.Ownerships.Contains(app.Id) && !string.IsNullOrWhiteSpace(app.ExtAppId))
                .Select(app => app.ExtAppId!)];
        }

        /// <summary>
        /// Adds a label to an AND-group of a side, or as a new AND-group if no group is given.
        /// </summary>
        /// <param name="isSource">True for the source, false for the destination.</param>
        /// <param name="groupIndex">Index of the AND-group, -1 for a new group.</param>
        /// <param name="label">The label to add.</param>
        public void AddLabel(bool isSource, int groupIndex, LabelValue label)
        {
            List<List<ConnectionLabel>> groups = LabelGroups(isSource);
            if (groupIndex < 0 || groupIndex >= groups.Count)
            {
                List<ConnectionLabel> newGroup = [new() { LabelId = label.Id, AndGroup = NextAndGroup(isSource), Label = label }];
                groups.Add(newGroup);
            }
            else if (!groups[groupIndex].Exists(assigned => assigned.LabelId == label.Id))
            {
                groups[groupIndex].Add(new() { LabelId = label.Id, AndGroup = groups[groupIndex][0].AndGroup, Label = label });
            }
        }

        /// <summary>
        /// Removes a label from an AND-group of a side; an emptied group is dropped.
        /// </summary>
        public void RemoveLabel(bool isSource, int groupIndex, ConnectionLabel label)
        {
            List<List<ConnectionLabel>> groups = LabelGroups(isSource);
            if (groupIndex < 0 || groupIndex >= groups.Count)
            {
                return;
            }
            groups[groupIndex].RemoveAll(assigned => assigned.LabelId == label.LabelId);
            if (groups[groupIndex].Count == 0)
            {
                groups.RemoveAt(groupIndex);
            }
        }

        /// <summary>
        /// Formats AND-groups, e.g. "(Stage: Prod ∩ OS: Linux) ∪ (Stage: Test)", not encoded.
        /// </summary>
        public static string DisplayLabelGroups(List<List<ConnectionLabel>> groups)
        {
            return ConnectionLabelFormatter.FormatGroups(groups);
        }

        /// <summary>
        /// Writes the changed labels of both sides: new assignments are added, removed ones closed, recorded with one label
        /// change event of the connection. A side taken over from a used interface carries no labels of its own.
        /// </summary>
        /// <param name="connectionId">Id of the saved connection.</param>
        public async Task SaveLabels(int connectionId)
        {
            long? changeEventId = null;
            async Task<long> GetChangeEventId()
            {
                changeEventId ??= (await apiConnection.SendQueryAsync<ReturnId>(LabellingQueries.addConnectionLabelChangeEvent,
                    new { connectionId, actor = userConfig.User.Dn })).Id ?? throw new InvalidOperationException("No label change event id returned.");
                return changeEventId.Value;
            }
            storedSrcLabels = await SaveSide(connectionId, storedSrcLabels, ActConn.SrcFromInterface ? [] : SrcLabelGroups, ModellingTypes.ConnectionField.Source, GetChangeEventId);
            storedDstLabels = await SaveSide(connectionId, storedDstLabels, ActConn.DstFromInterface ? [] : DstLabelGroups, ModellingTypes.ConnectionField.Destination, GetChangeEventId);
            if (!ActConn.SrcFromInterface)
            {
                ActConn.SourceLabels = [.. storedSrcLabels];
            }
            if (!ActConn.DstFromInterface)
            {
                ActConn.DestinationLabels = [.. storedDstLabels];
            }
        }

        private async Task<List<ConnectionLabel>> SaveSide(int connectionId, List<ConnectionLabel> stored, List<List<ConnectionLabel>> groups,
            ModellingTypes.ConnectionField field, Func<Task<long>> getChangeEventId)
        {
            List<ConnectionLabel> wanted = [.. groups.SelectMany(group => group)];
            List<ConnectionLabel> toClose = [.. stored.Where(label => !wanted.Exists(other => SameAssignment(label, other)))];
            List<ConnectionLabel> toAdd = [.. wanted.Where(label => !stored.Exists(other => SameAssignment(label, other)))];
            bool isSource = field == ModellingTypes.ConnectionField.Source;
            if (toClose.Count > 0)
            {
                long removedEventId = await getChangeEventId();
                var assignments = toClose.Select(label => new { label_id = new { _eq = label.LabelId }, and_group = new { _eq = label.AndGroup } }).ToList();
                await apiConnection.SendQueryAsync<ReturnId>(isSource ? LabellingQueries.removeConnectionSourceLabels : LabellingQueries.removeConnectionDestinationLabels,
                    new { connectionId, assignments, removedEventId });
                await LogLabelChanges(toClose, field, ModellingTypes.ChangeType.Unassign, "Removed", "from");
            }
            if (toAdd.Count > 0)
            {
                long createdEventId = await getChangeEventId();
                var labels = toAdd.Select(label => new { connection_id = connectionId, label_id = label.LabelId, and_group = label.AndGroup, created_event_id = createdEventId }).ToList();
                await apiConnection.SendQueryAsync<ReturnId>(isSource ? LabellingQueries.addConnectionSourceLabels : LabellingQueries.addConnectionDestinationLabels,
                    new { labels });
                await LogLabelChanges(toAdd, field, ModellingTypes.ChangeType.Assign, "Added", "to");
            }
            return wanted;
        }

        private async Task LogLabelChanges(List<ConnectionLabel> labels, ModellingTypes.ConnectionField field, ModellingTypes.ChangeType changeType, string verb, string preposition)
        {
            foreach (ConnectionLabel label in labels)
            {
                await LogChange(changeType, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"{verb} Label {label.Display()} (group {label.AndGroup}) {preposition} {GetTypeAndName()}: {field}", Application.Id);
            }
        }

        private static bool SameAssignment(ConnectionLabel label, ConnectionLabel other)
        {
            return label.LabelId == other.LabelId && label.AndGroup == other.AndGroup;
        }

        private int NextAndGroup(bool isSource)
        {
            List<ConnectionLabel> stored = isSource ? storedSrcLabels : storedDstLabels;
            IEnumerable<int> used = stored.Select(label => label.AndGroup).Concat(LabelGroups(isSource).SelectMany(group => group).Select(label => label.AndGroup));
            return used.Any() ? used.Max() + 1 : 0;
        }
    }
}
