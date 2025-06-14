using FWO.Basics;
using FWO.Data;
namespace FWO.Services.RuleTreeBuilder
{
    public class RuleTreeItem : TreeItem<Rule>
    {
        public bool IsSectionHeader { get; set; } = false;
        public bool IsRule { get; set; } = false;
        public bool IsInlineLayerRoot { get; set; } = false;
        public bool IsOrderedLayerHeader { get; set; } = false;

        public RuleTreeItem AddItem(RuleTreeItem? parent = null, RuleTreeItem? item = null, Rule? data = default!, List<int>? position = null, string header = "", bool isRoot = false, bool addToFlatList = false, bool addToChildren = false, bool setLastAddedItem = false)
        {
            return base.AddItem(parent, item ?? new(), data, position, header, isRoot, addToFlatList, addToChildren, setLastAddedItem) as RuleTreeItem;
        }
        
    }    
}