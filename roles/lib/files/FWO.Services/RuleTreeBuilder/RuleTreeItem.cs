using FWO.Basics;
using FWO.Data;
namespace FWO.Services.RuleTreeBuilder
{
    public class RuleTreeItem : TreeItem<Rule>
    {
        #region Properties & fields

        /// <summary>
        /// Direct Children of this tree item.
        /// </summary>
        new public List<RuleTreeItem> Children { get; set; } = new();
        /// <summary>
        /// The object that is organized with its kins in the tree structure
        /// </summary>
        new public Rule Data { get; set; } = new();
        /// <summary>
        /// A flat list of every tree item that is somewhere in the tree.
        /// </summary>
        new public List<RuleTreeItem> ElementsFlat { get; set; } = new();
        /// <summary>
        /// Flag to mark items that act as roots of section headers
        /// </summary>
        public bool IsSectionHeader { get; set; } = false;
        /// <summary>
        /// Flag to mark items that are representatives of rules.
        /// </summary>
        public bool IsRule { get; set; } = false;
        /// <summary>
        /// Flag to mark items that act as roots of inline layer
        /// </summary>
        public bool IsInlineLayerRoot { get; set; } = false;
        /// <summary>
        /// Flag to mark items that act as roots of ordered layer headers
        /// </summary>
        public bool IsOrderedLayerHeader { get; set; } = false;
        /// <summary>
        /// Flag to mark items that act as roots of concatenations
        /// </summary>
        public bool IsConcatenationRoot { get; set; } = false;
        /// <summary>
        /// Flag to mark tree items that are expanded
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnIsExpandedChanged();
            }
        }
        /// <summary>
        /// Backing field for IsExpanded.
        /// </summary>
        private bool _isExpanded = true;
        /// <summary>
        /// Flag to mark tree items that are visible
        /// </summary>
        public bool IsVisible { get; set; } = true;
        /// <summary>
        /// The last item that was added to the tree.
        /// </summary>
        new public RuleTreeItem? LastAddedItem { get; set; }
        /// <summary>
        /// Direct parent of this tree item.
        /// </summary>
        new public RuleTreeItem? Parent { get; set; }
        /// <summary>
        /// Tree items without children that were reached during the last traversal down.
        /// </summary>
        public List<RuleTreeItem> SelectedRuleTreeLeafs { get; set; } = new();

        #endregion

        /// <summary>
        /// A strongly typed version of the generic AddItem method.
        /// </summary>
        public RuleTreeItem AddItem(RuleTreeItem? item = null, List<int>? position = null, string header = "", bool isRoot = false, bool addToFlatList = false, bool addToChildren = false, bool setLastAddedItem = false)
        {
            return base.AddItem(item ?? new RuleTreeItem(), position, header, isRoot, addToFlatList, addToChildren,
                setLastAddedItem) as RuleTreeItem ?? new RuleTreeItem();
        }

        private void OnIsExpandedChanged()
        {
            SelectedRuleTreeLeafs.Clear();

            foreach (RuleTreeItem item in TraverseDown(this))
            {
                if (item != this)
                {
                    if (!item.Children.Any())
                    {
                        SelectedRuleTreeLeafs.Add(item);
                    }

                    if (IsExpanded)
                    {
                        if (item != this && item.Parent != null && !item.Parent.IsExpanded)
                        {
                            item.IsVisible = false;
                        }
                        else
                        {
                            item.IsVisible = true;
                        }
                    }
                    else
                    {
                        item.IsVisible = false;
                    }
                }
            }
        }

        private IEnumerable<RuleTreeItem> TraverseDown(RuleTreeItem item, Action<RuleTreeItem>? action = null)
        {
            if (action != null)
            {
                action(item);
            }

            yield return item;

            foreach (RuleTreeItem child in item.Children)
            {
                foreach (RuleTreeItem descendant in TraverseDown(child))
                {
                    if (action != null)
                    {
                        action(descendant);
                    }

                    yield return descendant;
                }
            }
        }

        private IEnumerable<RuleTreeItem> TraverseUp(RuleTreeItem item, Action<RuleTreeItem>? action = null)
        {
            var current = item;

            while (current != null)
            {
                action?.Invoke(current);
                yield return current;

                current = current.Parent;
            }
        }
    }
}
