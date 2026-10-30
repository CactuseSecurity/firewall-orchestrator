namespace FWO.Ui.Services
{
    /// <summary>
    /// Tracks the widths of the left sidebars currently shown, so that content rendered outside the page layouts,
    /// e.g. the warnings of the main layout, can start after them like the page content does.
    /// </summary>
    public class LeftSidebarState
    {
        private readonly Dictionary<object, int> sidebarWidths = [];

        /// <summary>
        /// Raised when a left sidebar was added, removed or changed its width.
        /// </summary>
        public event Action? OnChange;

        /// <summary>
        /// True while at least one left sidebar is shown.
        /// </summary>
        public bool HasSidebar => sidebarWidths.Count > 0;

        /// <summary>
        /// Width of the widest left sidebar shown, 0 if none is shown.
        /// </summary>
        public int Width => sidebarWidths.Count > 0 ? sidebarWidths.Values.Max() : 0;

        /// <summary>
        /// Records the current width of a left sidebar.
        /// </summary>
        /// <param name="sidebar">The sidebar reporting its width.</param>
        /// <param name="width">Its width in pixels.</param>
        public void SetWidth(object sidebar, int width)
        {
            if (sidebarWidths.TryGetValue(sidebar, out int knownWidth) && knownWidth == width)
            {
                return;
            }
            sidebarWidths[sidebar] = width;
            OnChange?.Invoke();
        }

        /// <summary>
        /// Forgets a left sidebar that is no longer shown.
        /// </summary>
        /// <param name="sidebar">The sidebar that was removed.</param>
        public void Remove(object sidebar)
        {
            if (sidebarWidths.Remove(sidebar))
            {
                OnChange?.Invoke();
            }
        }
    }
}
