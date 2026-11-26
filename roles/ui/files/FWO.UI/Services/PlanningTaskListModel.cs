using FWO.Data.Workflow;

namespace FWO.Ui.Services
{
    /// <summary>The tasks shown by the planning task list view.</summary>
    public enum PlanningTaskListMode
    {
        /// <summary>Request tasks, each followed by its implementation tasks.</summary>
        AllTasks,
        /// <summary>Only the implementation tasks.</summary>
        ImplementationTasks
    }

    /// <summary>
    /// The rule columns a table of the planning task list view needs for its implementation tasks. The access table
    /// always has them; the other tables only if they hold rule implementation tasks, e.g. of rule delete or modify tasks.
    /// </summary>
    public enum PlanningRuleColumns
    {
        /// <summary>No rule columns.</summary>
        None,
        /// <summary>The device, needed by implementation tasks of rule delete tasks.</summary>
        Device,
        /// <summary>The device and the rule content: rule position, source, destination, service, action and tracking.</summary>
        All
    }

    /// <summary>The columns a table of the planning task list view needs for its implementation tasks.</summary>
    /// <param name="RuleColumns">The rule columns, used by the tables other than the access table.</param>
    /// <param name="FreeText">Whether an implementation task of the table has a free text or may get one.</param>
    public sealed record PlanningImplColumns(PlanningRuleColumns RuleColumns, bool FreeText)
    {
        /// <summary>The columns of a table without implementation tasks.</summary>
        public static readonly PlanningImplColumns None = new(PlanningRuleColumns.None, false);
    }

    /// <summary>Edit callbacks offered for the cells of implementation-task rows of the planning task list view.</summary>
    public sealed class PlanningImplTaskListActions
    {
        /// <summary>Determines whether the planner may change an implementation task of a request task.</summary>
        public Func<WfImplTask, WfReqTask, bool> CanEdit { get; init; } = (_, _) => false;
        /// <summary>Opens the management editor.</summary>
        public Func<WfImplTask, WfReqTask, Task> EditManagement { get; init; } = (_, _) => Task.CompletedTask;
        /// <summary>Opens the device editor.</summary>
        public Func<WfImplTask, WfReqTask, Task> EditDevice { get; init; } = (_, _) => Task.CompletedTask;
        /// <summary>Opens the free-text editor.</summary>
        public Func<WfImplTask, WfReqTask, Task> EditFreeText { get; init; } = (_, _) => Task.CompletedTask;
        /// <summary>Opens the name editor of a group, object or time object.</summary>
        public Func<WfImplTask, WfReqTask, Task> EditName { get; init; } = (_, _) => Task.CompletedTask;
    }

    /// <summary>One request task or implementation task displayed as a row of the planning task list view.</summary>
    public sealed class PlanningTaskListRow
    {
        private readonly IReadOnlyDictionary<string, TaskListCell> cells;

        /// <summary>Initializes a row.</summary>
        /// <param name="requestTask">The displayed request task, or the request task the displayed implementation task belongs to.</param>
        /// <param name="implementationTask">The displayed implementation task; null for a request-task row.</param>
        /// <param name="cells">The cells of the row by column key.</param>
        public PlanningTaskListRow(WfReqTask requestTask, WfImplTask? implementationTask, IReadOnlyDictionary<string, TaskListCell> cells)
        {
            RequestTask = requestTask;
            ImplementationTask = implementationTask;
            this.cells = cells;
        }

        /// <summary>
        /// The displayed request task (for a bundle displayed as one its first task), or the request task the displayed
        /// implementation task belongs to.
        /// </summary>
        public WfReqTask RequestTask { get; }

        /// <summary>
        /// The request tasks of the row: the task itself or the tasks of a bundle displayed as one. For an implementation
        /// task, the request tasks it implements together.
        /// </summary>
        public IReadOnlyList<WfReqTask> TaskGroup { get; init; } = [];

        /// <summary>The displayed implementation task; null for a request-task row.</summary>
        public WfImplTask? ImplementationTask { get; }

        /// <summary>Whether the row displays an implementation task.</summary>
        public bool IsImplementationTask => ImplementationTask != null;

        /// <summary>Id of the displayed task.</summary>
        public long Id => ImplementationTask?.Id ?? RequestTask.Id;

        /// <summary>Returns the cell of the given column, or an empty cell for unknown columns.</summary>
        /// <param name="columnKey">The column key.</param>
        public TaskListCell Cell(string columnKey)
        {
            return cells.TryGetValue(columnKey, out TaskListCell? cell) ? cell : TaskListCell.Empty;
        }
    }
}
