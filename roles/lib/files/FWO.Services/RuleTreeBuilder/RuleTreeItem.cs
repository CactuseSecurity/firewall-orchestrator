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
        /// Lookup of flat tree items by their report rule instance.
        /// </summary>
        public Dictionary<Rule, RuleTreeItem> ItemsByRule { get; } = new(ReferenceEqualityComparer.Instance);
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
        private bool _isExpanded = false;
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

        #region Methods - Public

        /// <summary>
        /// A strongly typed version of the generic AddItem method.
        /// </summary>
        public RuleTreeItem AddItem(RuleTreeItem? item = null, List<int>? position = null, string header = "", bool isRoot = false, bool addToFlatList = false, bool addToChildren = false, bool setLastAddedItem = false)
        {
            return base.AddItem(item ?? new RuleTreeItem(), position, header, isRoot, addToFlatList, addToChildren,
                setLastAddedItem) as RuleTreeItem ?? new RuleTreeItem();
        }

        #endregion

        #region Methods - Private

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
                        ExpandItem(item);
                    }
                    else
                    {
                        item.IsVisible = false;
                    }
                }
            }
        }

        private void ExpandItem(RuleTreeItem item)
        {
            if (item != this && !AllExpandableAncestorsAreExpanded(item))
            {
                item.IsVisible = false;
            }
            else
            {
                item.IsVisible = true;
            }
        }

        /// <summary>
        /// Checks whether every expandable ancestor on the path from the supplied item back to
        /// the tree root is currently expanded. Visibility in the report tree depends on the full
        /// ancestor chain rather than only on the direct parent because structural inline-layer
        /// roots remain logically expanded even while their owning visible ancestors are
        /// collapsed.
        /// </summary>
        private static bool AllExpandableAncestorsAreExpanded(RuleTreeItem item)
        {
            RuleTreeItem? currentAncestor = item.Parent;

            while (currentAncestor != null)
            {
                if (!currentAncestor.IsRoot && currentAncestor.Children.Count > 0 && !currentAncestor.IsExpanded)
                {
                    return false;
                }

                currentAncestor = currentAncestor.Parent;
            }

            return true;
        }

        /// <summary>
        /// Updates the expanded state for all expandable descendants of the provided root item.
        /// Inline-layer roots are intentionally excluded from external collapse-state changes
        /// because they are structural-only nodes without their own visible toggle affordance.
        /// Their descendants should become visible again as soon as the owning visible ancestor
        /// is re-expanded, so inline roots must stay logically expanded even when the user
        /// collapses all visible rows.
        /// </summary>
        public static void SetExpandedRecursively(RuleTreeItem item, bool isExpanded)
        {
            foreach (RuleTreeItem childItem in TraverseDown(item))
            {
                if (childItem == item || childItem.Children.Count == 0)
                {
                    continue;
                }

                if (childItem.IsInlineLayerRoot)
                {
                    childItem.IsExpanded = true;
                    continue;
                }

                if (childItem.Children.Count > 0)
                {
                    childItem.IsExpanded = isExpanded;
                }
            }

            RefreshVisibilityRecursively(item);
        }

        /// <summary>
        /// Recomputes visibility for every descendant below the supplied root after a bulk
        /// expand/collapse operation. This normalization step ensures that visibility reflects
        /// the final expanded states of all ancestors instead of the transient state that existed
        /// while the recursive toggle loop was still in progress.
        /// </summary>
        private static void RefreshVisibilityRecursively(RuleTreeItem rootItem)
        {
            foreach (RuleTreeItem childItem in TraverseDown(rootItem))
            {
                if (childItem == rootItem)
                {
                    childItem.IsVisible = true;
                    continue;
                }

                childItem.IsVisible = AllExpandableAncestorsAreExpanded(childItem);
            }
        }


        private static IEnumerable<RuleTreeItem> TraverseDown(RuleTreeItem item, Action<RuleTreeItem>? action = null)
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

        #endregion
    }
}
