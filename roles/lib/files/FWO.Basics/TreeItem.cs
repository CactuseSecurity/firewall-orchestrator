namespace FWO.Basics
{
    public class TreeItem<T>
    {
        public List<TreeItem<T>> Children { get; set; }

        public TreeItem<T>? Parent { get; set; }
        public TreeItem<T>? LastAddedItem { get; set; }

        public List<int> Position { get; set; }

        public T? Data { get; set; }

        public bool IsRoot { get; set; }

        public string Header { get; set; } = "";


        public TreeItem() : this(default!)
        {
            IsRoot = true;
        }

        public TreeItem(T data)
        {
            Children = new();
            Position = new();
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