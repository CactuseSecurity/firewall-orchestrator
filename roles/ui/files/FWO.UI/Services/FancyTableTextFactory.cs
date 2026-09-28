using FWO.Config.Api;
using SoPro.FancyTable.Components;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Creates centralized localized text sets for FancyTable-based components.
    /// </summary>
    public static class FancyTableTextFactory
    {
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
                ShowingVisibleRowsFormat = userConfig.GetText("showing_visible_rows_format")
            };
        }
    }
}
