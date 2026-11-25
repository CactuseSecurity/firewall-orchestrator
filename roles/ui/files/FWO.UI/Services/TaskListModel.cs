namespace FWO.Ui.Services
{
    /// <summary>The tables of the task list views (approval and planning); each shows only the information relevant for its tasks.</summary>
    public enum TaskListTable
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

    /// <summary>Column definition of a task list view.</summary>
    /// <param name="Key">Unique column key.</param>
    /// <param name="TextKey">Localization key of the column title.</param>
    public sealed record TaskListColumn(string Key, string TextKey);

    /// <summary>Badge shown next to a cell value, e.g. the change of a group member.</summary>
    /// <param name="Text">Localized badge text.</param>
    /// <param name="CssClass">Bootstrap class coloring the badge.</param>
    public sealed record TaskListBadge(string Text, string CssClass);

    /// <summary>One cell of a task list view.</summary>
    /// <param name="Values">Values displayed in the cell, one per line.</param>
    /// <param name="Edit">Opens the editor of the cell, or null when the cell is read-only.</param>
    public sealed record TaskListCell(IReadOnlyList<string> Values, Func<Task>? Edit)
    {
        private static readonly List<string> kNoValues = [];

        /// <summary>An empty, read-only cell.</summary>
        public static readonly TaskListCell Empty = new(kNoValues, null);

        /// <summary>Optional badges, one per value at the same index; null when the cell has no badges.</summary>
        public IReadOnlyList<TaskListBadge>? Badges { get; init; }

        /// <summary>Whether clicking the cell opens an editor.</summary>
        public bool CanEdit => Edit != null;

        /// <summary>Values (with their badges) joined to one text, used for searching, filtering and sorting.</summary>
        public string Text => string.Join(", ", Values.Select((value, index) => Badge(index) is TaskListBadge badge
            ? $"{value} ({badge.Text})"
            : value));

        /// <summary>Returns the badge of the value at the given index, or null if there is none.</summary>
        /// <param name="index">Index of the value.</param>
        public TaskListBadge? Badge(int index) => Badges != null && index < Badges.Count ? Badges[index] : null;
    }
}
