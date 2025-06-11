namespace FWO.Basics
{
    public class TreeItem<TItem>
    {
        public List<TreeItem<TItem>> Children { get; set; } = new();
        public List<TreeItem<TItem>> ElementsFlat { get; set; } = new();

        public List<int>? Position { get; set; } = new();
        public TreeItem<TItem>? Parent { get; set; }
        public TreeItem<TItem>? LastAddedItem { get; set; }
        public TItem? Data { get; set; }

        public bool IsRoot { get; set; } = false;
        public string Header { get; set; } = "";

        public string GetPositionString()
        {
            if (Position != null)
            {
                return string.Join(".", Position);
            }
            else
            {
                return "";
            }
            
        }

        public void SetPosition(string orderNumberString)
        {
            Position = orderNumberString
                .Split('.')
                .Select(int.Parse)
                .ToList();
        }

        public TreeItem<TItem> AddItem(TreeItem<TItem>? item = null, TItem? data = default!, List<int>? position = null, string header = "", bool isRoot = false, bool addToFlatList = false, bool addToChildren = false, bool setLastAddedItem = false)
        {
            TreeItem<TItem> newItem = item ?? new();

            newItem.Parent = this;
            newItem.Data = data;
            newItem.Position = position;
            newItem.Header = header;
            newItem.IsRoot = isRoot;

            if (addToFlatList)
            {
                ElementsFlat.Add(newItem);
            }

            if (addToChildren)
            {
                Children.Add(newItem);
            }

            if (setLastAddedItem)
            {
                LastAddedItem = newItem;
            }

            return newItem;
        }

    }
}