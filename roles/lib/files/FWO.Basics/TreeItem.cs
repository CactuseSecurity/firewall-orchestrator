using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Basics
{
    public class TreeItem<TItem> : ITreeItem<TItem>
    {
        public List<ITreeItem<TItem>> Children { get; set; } = new();
        public List<ITreeItem<TItem>> ElementsFlat { get; set; } = new();

        public List<int>? Position { get; set; } = new();
        public ITreeItem<TItem>? Parent { get; set; }
        public ITreeItem<TItem>? LastAddedItem { get; set; }
        public TItem? Data { get; set; }
        public string? Identifier { get; set; }

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

        public virtual ITreeItem<TItem> AddItem(ITreeItem<TItem>? parent = null, ITreeItem<TItem>? item = null, TItem? data = default!, List<int>? position = null, string header = "", bool isRoot = false, bool addToFlatList = false, bool addToChildren = false, bool setLastAddedItem = false)
        {
            ITreeItem<TItem> newItem = item ?? new TreeItem<TItem>();

            newItem.Parent = parent ?? this;
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

        public string ToJson()
        {
            var rootNode = BuildSerializableNode(this);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            return JsonSerializer.Serialize(rootNode, options);
        }

        private SerializableTreeNode BuildSerializableNode(ITreeItem<TItem> node)
        {
            var serializable = new SerializableTreeNode();

            if (node.Identifier != null)
            {
                serializable.Identifier = node.Identifier;
            }

            if (node.IsRoot == true)
            {
                serializable.IsRoot = true;
            }

            if (node.Header != "")
            {
                serializable.Header = node.Header;
            }

            if (node.Data != null)
            {
                serializable.Position = node.GetPositionString();
            }

            if (node.Children.Any())
            {
                serializable.Children = new();

                foreach (var child in node.Children)
                {
                    serializable.Children.Add(BuildSerializableNode(child));
                }                
            }


            return serializable;
        }

        private class SerializableTreeNode
        {
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Identifier { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public bool? IsRoot { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Header { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Position { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public List<SerializableTreeNode>? Children { get; set; }
        }

    }
}