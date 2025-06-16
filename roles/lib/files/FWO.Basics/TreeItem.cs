using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Basics
{
    /// <summary>
    /// A generic class to build tree structures.
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    public class TreeItem<TItem> : ITreeItem<TItem>
    {
        /// <summary>
        /// Direct Children of this item.
        /// </summary>
        public List<ITreeItem<TItem>> Children { get; set; } = new();
        /// <summary>
        /// A flat list of every tree item that is somewhere in the tree.
        /// </summary>
        public List<ITreeItem<TItem>> ElementsFlat { get; set; } = new();

        /// <summary>
        /// N-dimensional numeric indicator for the position of this item in the tree.
        /// </summary>
        public List<int>? Position { get; set; } = new();
        /// <summary>
        /// Direct parent of this tree item.
        /// </summary>
        public ITreeItem<TItem>? Parent { get; set; }
        /// <summary>
        /// The last item that was added to the tree.
        /// </summary>
        public ITreeItem<TItem>? LastAddedItem { get; set; }
        /// <summary>
        /// The object that is organized with its kins in the tree structure
        /// </summary>
        public TItem? Data { get; set; }
        /// <summary>
        /// An identifier for the object.
        /// </summary>
        public string? Identifier { get; set; }

        /// <summary>
        /// Flag that indicates wether this is the root item of the tree.
        /// </summary>
        public bool IsRoot { get; set; } = false;
        /// <summary>
        /// A header for items that are organizational groups (like sections, or ordered layers for rules).
        /// </summary>
        public string Header { get; set; } = "";

        /// <summary>
        /// Returns the position as a dotted number of type string.
        /// </summary>
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

        /// <summary>
        /// Sets the position on the basis of a (dotted number) string. 
        /// </summary>
        public void SetPosition(string orderNumberString)
        {
            Position = orderNumberString
                .Split('.')
                .Select(int.Parse)
                .ToList();
        }

        /// <summary>
        /// Adds an item to the tree.
        /// </summary>
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

        /// <summary>
        /// Creates a json string for the structure, taking this item as the root.
        /// </summary>
        public string ToJson()
        {
            var rootNode = BuildSerializableNode(this);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            return JsonSerializer.Serialize(rootNode, options);
        }

        /// <summary>
        /// Creates items that can be serialized to a json.
        /// </summary>
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

        /// <summary>
        /// Definition which data of the actual items are processed and how they are processed during serialization.
        /// </summary>
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
