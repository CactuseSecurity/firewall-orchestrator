using FWO.Basics;
using FWO.Data;
namespace FWO.Services.RuleTreeBuilder
{
    public class RuleTreeItem : TreeItem<Rule>
    {
        /// <summary>
        /// Flag to mark items that act as section headers (/roots)
        /// </summary>
        public bool IsSectionHeader { get; set; } = false;
        /// <summary>
        /// Flag to mark items that a rule as data.
        /// </summary>
        public bool IsRule { get; set; } = false;
        /// <summary>
        /// Flag to mark items that act as inline layer headers (/roots)
        /// </summary>
        public bool IsInlineLayerRoot { get; set; } = false;
        /// <summary>
        /// Flag to mark items that act as ordered layer headers (/roots)
        /// </summary>
        public bool IsOrderedLayerHeader { get; set; } = false;

        /// <summary>
        /// A strongly typed version of the generic AddItem method.
        /// </summary>
        public RuleTreeItem AddItem(RuleTreeItem? parent = null, RuleTreeItem? item = null, Rule? data = default!, List<int>? position = null, string header = "", bool isRoot = false, bool addToFlatList = false, bool addToChildren = false, bool setLastAddedItem = false)
        {
            return base.AddItem(parent, item ?? new(), data, position, header, isRoot, addToFlatList, addToChildren, setLastAddedItem) as RuleTreeItem;
        }

    }
}
