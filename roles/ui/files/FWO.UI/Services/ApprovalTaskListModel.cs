using FWO.Data.Workflow;

namespace FWO.Ui.Services
{
    /// <summary>The tables of the approval task list view; each shows only the information relevant for its tasks.</summary>
    public enum ApprovalTaskListTable
    {
        /// <summary>Access tasks.</summary>
        Access,
        /// <summary>Network object and network group tasks.</summary>
        Address,
        /// <summary>Service object and service group tasks.</summary>
        Service,
        /// <summary>All other tasks, e.g. generic, interface and time-object tasks.</summary>
        Other
    }

    /// <summary>Column definition of the approval task list view.</summary>
    /// <param name="Key">Unique column key.</param>
    /// <param name="TextKey">Localization key of the column title.</param>
    public sealed record ApprovalTaskListColumn(string Key, string TextKey);

    /// <summary>Badge shown next to a cell value, e.g. the change of a group member.</summary>
    /// <param name="Text">Localized badge text.</param>
    /// <param name="CssClass">Bootstrap class coloring the badge.</param>
    public sealed record ApprovalTaskListBadge(string Text, string CssClass);

    /// <summary>One cell of the approval task list view.</summary>
    /// <param name="Values">Values displayed in the cell, one per line.</param>
    /// <param name="Edit">Opens the editor of the cell, or null when the cell is read-only.</param>
    public sealed record ApprovalTaskListCell(IReadOnlyList<string> Values, Func<Task>? Edit)
    {
        private static readonly List<string> kNoValues = [];

        /// <summary>An empty, read-only cell.</summary>
        public static readonly ApprovalTaskListCell Empty = new(kNoValues, null);

        /// <summary>Optional badges, one per value at the same index; null when the cell has no badges.</summary>
        public IReadOnlyList<ApprovalTaskListBadge>? Badges { get; init; }

        /// <summary>Whether clicking the cell opens an editor.</summary>
        public bool CanEdit => Edit != null;

        /// <summary>Values (with their badges) joined to one text, used for searching, filtering and sorting.</summary>
        public string Text => string.Join(", ", Values.Select((value, index) => Badge(index) is ApprovalTaskListBadge badge
            ? $"{value} ({badge.Text})"
            : value));

        /// <summary>Returns the badge of the value at the given index, or null if there is none.</summary>
        /// <param name="index">Index of the value.</param>
        public ApprovalTaskListBadge? Badge(int index) => Badges != null && index < Badges.Count ? Badges[index] : null;
    }

    /// <summary>One request task or bundle displayed as a row of the approval task list view.</summary>
    public sealed class ApprovalTaskListRow
    {
        private readonly IReadOnlyDictionary<string, ApprovalTaskListCell> cells;

        /// <summary>Initializes a row for the given task.</summary>
        /// <param name="task">The displayed request task.</param>
        /// <param name="cells">The cells of the row by column key.</param>
        public ApprovalTaskListRow(WfReqTask task, IReadOnlyDictionary<string, ApprovalTaskListCell> cells)
        {
            Task = task;
            this.cells = cells;
        }

        /// <summary>The displayed request task; for a bundle its first task, which receives the edits for the whole bundle.</summary>
        public WfReqTask Task { get; }

        /// <summary>Compliance status of the row's tasks; null if they were not checked.</summary>
        public ApprovalTaskCompliance? Compliance { get; init; }

        /// <summary>Returns the cell of the given column, or an empty cell for unknown columns.</summary>
        /// <param name="columnKey">The column key.</param>
        public ApprovalTaskListCell Cell(string columnKey)
        {
            return cells.TryGetValue(columnKey, out ApprovalTaskListCell? cell) ? cell : ApprovalTaskListCell.Empty;
        }
    }
}
