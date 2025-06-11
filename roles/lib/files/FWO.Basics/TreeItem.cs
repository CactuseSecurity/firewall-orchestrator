namespace FWO.Basics
{
    public class TreeItem<TItem>
    {
        public List<TreeItem<TItem>> Children { get; set; }
        public List<TreeItem<TItem>> ElementsFlat { get; set; }
        public TreeItem<TItem>? Parent { get; set; }
        public TreeItem<TItem>? LastAddedItem { get; set; }

        public List<int> Position { get; set; }

        public TItem? Data { get; set; }
        public object? Origin { get; set; }

        public bool IsRoot { get; set; }

        public string Header { get; set; } = "";


        public TreeItem() : this(default!)
        {
            IsRoot = true;
        }

        public TreeItem(TItem data)
        {
            Children = new();
            Position = new();
            ElementsFlat = new();
            Data = data;
        }

        public string GetPositionString()
        {
            return string.Join(".", Position);
        }

        public void SetPosition(string orderNumberString)
        {
            Position = orderNumberString
                .Split('.')
                .Select(int.Parse)
                .ToList();
        }


    }
}