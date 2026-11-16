using FWO.Config.Api;
using SoPro.FancyTable.Components;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Creates centralized localized text sets for FancyTable-based components.
    /// </summary>
    public static class FancyTableTextFactory
    {
        /// <summary>
        /// Creates the FancyTable texts in the language of the given user.
        /// </summary>
        /// <param name="userConfig">User config providing the localized texts.</param>
        /// <returns>The localized FancyTable texts.</returns>
        public static FancyTableTexts Create(UserConfig userConfig)
        {
            return new FancyTableTexts
            {
                SearchPlaceholder = userConfig.GetText("search"),
                RowsPerPageLabel = userConfig.GetText("rows_per_page"),
                AllItemsLabel = userConfig.GetText("all"),
                HiddenColumnsLabel = userConfig.GetText("hidden_columns"),
                PaginationAriaLabel = userConfig.GetText("pagination"),
                PreviousPageLabel = userConfig.GetText("previous"),
                NextPageLabel = userConfig.GetText("next"),
                NoRowsLabel = userConfig.GetText("no_rows"),
                NoMatchingRowsFormat = userConfig.GetText("no_matching_rows"),
                NoMatchingRowsWithTotalFormat = userConfig.GetText("no_matching_rows_with_total"),
                ShowingItemsFormat = userConfig.GetText("showing_items_format"),
                ShowingFilteredItemsFormat = userConfig.GetText("showing_filtered_items_format"),
                ExpandLabel = userConfig.GetText("expand"),
                CollapseLabel = userConfig.GetText("collapse"),
                NoMatchingRootItemsFormat = userConfig.GetText("no_matching_root_items_format"),
                ShowingVisibleRowsFormat = userConfig.GetText("showing_visible_rows_format"),
                ResizeColumnFormat = userConfig.GetText("resize_column_format"),
                ReorderColumnFormat = userConfig.GetText("reorder_column_format"),
                SortColumnFormat = userConfig.GetText("sort_column_format"),
                HideColumnFormat = userConfig.GetText("hide_column_format"),
                ClearSearchLabel = userConfig.GetText("clear_search"),
                FilteredColumnLabel = userConfig.GetText("filtered_column"),
                FilterColumnFormat = userConfig.GetText("filter_column_format"),
                FilterValuePlaceholder = userConfig.GetText("value"),
                ApplyFilterLabel = userConfig.GetText("apply_filter"),
                ClearFilterLabel = userConfig.GetText("clear_filter"),
                FilterOperatorLabel = userConfig.GetText("filter_operator"),
                FilterOperatorEqualsLabel = userConfig.GetText("filter_op_equals"),
                FilterOperatorNotEqualsLabel = userConfig.GetText("filter_op_not_equals"),
                FilterOperatorContainsLabel = userConfig.GetText("filter_op_contains"),
                FilterOperatorNotContainsLabel = userConfig.GetText("filter_op_not_contains"),
                FilterOperatorStartsWithLabel = userConfig.GetText("filter_op_starts_with"),
                FilterOperatorEndsWithLabel = userConfig.GetText("filter_op_ends_with"),
                FilterOperatorIsEmptyLabel = userConfig.GetText("filter_op_is_empty"),
                FilterOperatorIsNotEmptyLabel = userConfig.GetText("filter_op_is_not_empty")
            };
        }
    }
}
