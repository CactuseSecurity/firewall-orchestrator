using FWO.Data.Workflow;

namespace FWO.Ui.Services
{
    /// <summary>One request task or bundle displayed as a row of the approval task list view.</summary>
    public sealed class ApprovalTaskListRow
    {
        private readonly IReadOnlyDictionary<string, TaskListCell> cells;

        /// <summary>Initializes a row for the given task.</summary>
        /// <param name="task">The displayed request task.</param>
        /// <param name="cells">The cells of the row by column key.</param>
        public ApprovalTaskListRow(WfReqTask task, IReadOnlyDictionary<string, TaskListCell> cells)
        {
            Task = task;
            this.cells = cells;
        }

        /// <summary>The displayed request task; for a bundle its first task, which receives the edits for the whole bundle.</summary>
        public WfReqTask Task { get; }

        /// <summary>All tasks of the row: the task itself or the tasks of a bundle displayed as one.</summary>
        public IReadOnlyList<WfReqTask> TaskGroup { get; init; } = [];

        /// <summary>Compliance status of the row's tasks; null if they were not checked.</summary>
        public ApprovalTaskCompliance? Compliance { get; init; }

        /// <summary>Returns the cell of the given column, or an empty cell for unknown columns.</summary>
        /// <param name="columnKey">The column key.</param>
        public TaskListCell Cell(string columnKey)
        {
            return cells.TryGetValue(columnKey, out TaskListCell? cell) ? cell : TaskListCell.Empty;
        }
    }
}
