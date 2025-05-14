namespace FWO.Data.Report
{
    public class TreeNode<T>
    {
        public T Item { get; set; }
        public bool IsExpanded { get; set; } = false;
        public List<TreeNode<T>> Children { get; set; } = new List<TreeNode<T>>();
    }
}
