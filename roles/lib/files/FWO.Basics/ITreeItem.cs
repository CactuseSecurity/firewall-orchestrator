namespace FWO.Basics
{
    public interface ITreeItem<TItem>
    {
        List<ITreeItem<TItem>> Children { get; set; }
        List<ITreeItem<TItem>> ElementsFlat { get; set; }
        List<int>? Position { get; set; }
        ITreeItem<TItem>? Parent { get; set; }
        ITreeItem<TItem>? LastAddedItem { get; set; }
        TItem? Data { get; set; }
        string? Identifier { get; set; }
        bool IsRoot { get; set; }
        string Header { get; set; }

        string GetPositionString();
        void SetPosition(string orderNumberString);
        ITreeItem<TItem> AddItem(ITreeItem<TItem>? parent = null, ITreeItem<TItem>? item = null, TItem? data = default!, List<int>? position = null, string header = "", bool isRoot = false, bool addToFlatList = false, bool addToChildren = false, bool setLastAddedItem = false);
        string ToJson();
    }
}