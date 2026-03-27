using FWO.Basics.Interfaces;

namespace FWO.Ui.Shared
{
    /// <summary>
    /// Defines a tree-table column including header metadata and value selectors.
    /// </summary>
    public class TreeViewColumn<TNode> where TNode : ITreeViewItem
    {
        public string Key { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string HeaderClass { get; init; } = string.Empty;
        public string CellClass { get; init; } = string.Empty;
        public bool ClampContent { get; init; }
        public bool IsHierarchyColumn { get; init; }
        public Func<TreeViewNodeContext<TNode>, string>? HtmlSelector { get; init; }
        public Func<TreeViewNodeContext<TNode>, string>? TextSelector { get; init; }
        public Func<TreeViewNodeContext<TNode>, string>? TooltipSelector { get; init; }
    }
}
