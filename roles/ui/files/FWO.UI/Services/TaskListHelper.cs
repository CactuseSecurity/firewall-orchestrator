using FWO.Config.Api;
using FWO.Data.Workflow;
using Microsoft.AspNetCore.Components;
using SoPro.FancyTable.Components;

namespace FWO.Ui.Services
{
    /// <summary>Builds the values, cells, columns and badges shared by the task list views (approval and planning).</summary>
    public static class TaskListHelper
    {
        /// <summary>Column key of the actions column.</summary>
        public const string kActionsColumn = "actions";
        /// <summary>Minimum width of all columns except the id.</summary>
        public const double kColumnMinWidth = 80;

        /// <summary>Returns the localization key of the title of a table.</summary>
        /// <param name="table">The table.</param>
        public static string TitleKey(TaskListTable table) => table switch
        {
            TaskListTable.Access => "approval_list_access_tasks",
            TaskListTable.Address => "approval_list_address_tasks",
            TaskListTable.Service => "approval_list_service_tasks",
            _ => "approval_list_other_tasks"
        };

        /// <summary>
        /// Inserts the actions column directly after the id column, so the actions stay in view without scrolling horizontally.
        /// </summary>
        /// <param name="columns">The columns, containing the id column.</param>
        /// <param name="idKey">Key of the id column.</param>
        /// <param name="title">The localized column title.</param>
        /// <param name="cellTemplate">Renders the actions of a row.</param>
        public static void InsertActionsColumn<TItem>(List<FancyColumn<TItem>> columns, string idKey, string title,
            RenderFragment<TItem> cellTemplate)
        {
            int actionsPosition = columns.FindIndex(column => column.Key == idKey) + 1;
            columns.Insert(actionsPosition, new FancyColumn<TItem>
            {
                Key = kActionsColumn,
                Title = title,
                Sortable = false,
                Searchable = false,
                CellTemplate = cellTemplate
            });
        }

        /// <summary>Renders a cell; clicking an editable cell opens its editor.</summary>
        /// <param name="cell">The cell.</param>
        public static RenderFragment CellContent(TaskListCell cell) => builder =>
        {
            builder.OpenComponent<Pages.Request.TaskListCellContent>(0);
            builder.AddComponentParameter(1, nameof(Pages.Request.TaskListCellContent.Cell), cell);
            builder.CloseComponent();
        };
        /// <summary>Returns the value as the only value of a cell, or no value if it is empty.</summary>
        /// <param name="value">The value.</param>
        public static List<string> SingleValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? [] : [value];

        /// <summary>Returns the non-empty values.</summary>
        /// <param name="values">The values.</param>
        public static List<string> NonEmptyValues(IEnumerable<string> values) =>
            [.. values.Where(value => !string.IsNullOrWhiteSpace(value))];

        /// <summary>Returns the distinct non-empty values.</summary>
        /// <param name="values">The values.</param>
        public static List<string> DistinctValues(IEnumerable<string> values) => [.. NonEmptyValues(values).Distinct()];

        /// <summary>Formats a name with its technical detail as "name (detail)", or the non-empty one of both.</summary>
        /// <param name="name">The name.</param>
        /// <param name="detail">The detail, e.g. an ip address or a service definition.</param>
        public static string NameWithDetail(string name, string detail)
        {
            if (string.IsNullOrWhiteSpace(name) || name == detail)
            {
                return detail;
            }
            return string.IsNullOrWhiteSpace(detail) ? name : $"{name} ({detail})";
        }

        /// <summary>Returns a read-only cell showing one value.</summary>
        /// <param name="value">The value; an empty value shows an empty cell.</param>
        public static TaskListCell ReadOnly(string? value) => Cell(value, null);

        /// <summary>Returns a cell showing one value.</summary>
        /// <param name="value">The value; an empty value shows an empty cell.</param>
        /// <param name="edit">Opens the editor of the cell, or null when the cell is read-only.</param>
        public static TaskListCell Cell(string? value, Func<Task>? edit) => new(SingleValue(value), edit);

        /// <summary>Adds the column if the condition holds.</summary>
        /// <param name="columns">The columns.</param>
        /// <param name="condition">Whether the column is shown.</param>
        /// <param name="column">The column.</param>
        public static void AddIf(List<TaskListColumn> columns, bool condition, TaskListColumn column)
        {
            if (condition)
            {
                columns.Add(column);
            }
        }

        /// <summary>Returns the badge of a group member change, using the texts and colors of the card views.</summary>
        /// <param name="action">The request or implementation action of the member.</param>
        /// <param name="userConfig">The user configuration providing the texts.</param>
        public static TaskListBadge MemberChangeBadge(string? action, UserConfig userConfig) => action switch
        {
            nameof(RequestAction.delete) => new(userConfig.GetText("remove_member"), "text-bg-danger"),
            nameof(RequestAction.unchanged) => new(userConfig.GetText("unchanged"), "text-bg-secondary"),
            _ => new(userConfig.GetText("add_member"), "text-bg-success")
        };
    }
}
