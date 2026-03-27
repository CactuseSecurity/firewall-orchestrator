namespace FWO.Basics.Interfaces
{
    public interface ITreeViewItem
    {
        List<ITreeViewItem> Children { get; set; }
        object Data { get; set; }
        Dictionary<string, string> DisplayData { get; set; }
        List<ITreeViewItem> ElementsFlat { get; set; }
        bool IsExpandable { get; set; }
        bool IsExpanded { get; set; }
        bool IsSelectable { get; set; }
        bool IsVisible { get; set; }
        string Label { get; set; }
    }
}
