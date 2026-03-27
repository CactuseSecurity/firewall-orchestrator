using FWO.Basics.Interfaces;

namespace FWO.Ui.Shared
{
    /// <summary>
    /// Carries the current tree node together with view actions for template rendering.
    /// </summary>
    public class TreeViewNodeContext<TNode>(
        TNode node,
        int level,
        bool hasChildren,
        Func<Task> toggleNodeAsync,
        Func<Task> selectNodeAsync)
        where TNode : ITreeViewItem
    {
        public TNode Node { get; } = node;
        public int Level { get; } = level;
        public bool HasChildren { get; } = hasChildren;
        public Func<Task> ToggleNodeAsync { get; } = toggleNodeAsync;
        public Func<Task> SelectNodeAsync { get; } = selectNodeAsync;
    }
}
