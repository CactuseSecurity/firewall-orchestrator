namespace FWO.Ui.Services
{
    /// <summary>
    /// Turns the row count the browser measured for the log data table into its page size.
    /// Kept out of the component so the decision can be tested without a browser.
    /// </summary>
    public static class LogDataTableLayout
    {
        /// <summary>
        /// Page size used until the browser has been measured, and whenever a measurement fails.
        /// </summary>
        public const int kDefaultPageSize = 15;

        /// <summary>
        /// Smallest page the table is ever given. In a window too short even for this the page
        /// scrolls, which is still better than a pager that shows fewer rows than it costs space.
        /// </summary>
        public const int kMinPageSize = 5;

        /// <summary>
        /// Largest page the table is ever given. Every row of a page is rendered, so a very tall
        /// window must not turn the whole loaded result set into one DOM update.
        /// </summary>
        public const int kMaxPageSize = 100;

        /// <summary>
        /// Resolves the page size from the number of rows the browser reported as fitting.
        /// </summary>
        /// <param name="measuredRows">Rows that fit into the window, zero if nothing could be measured.</param>
        /// <param name="currentPageSize">Page size the table has, kept if there was nothing to measure.</param>
        /// <returns>Page size within the supported bounds.</returns>
        public static int ResolvePageSize(int measuredRows, int currentPageSize)
        {
            if (measuredRows <= 0)
            {
                // a window which could not be measured must not make the table jump back to the
                // default, the size it already has is the better answer
                return currentPageSize;
            }
            return Math.Clamp(measuredRows, kMinPageSize, kMaxPageSize);
        }
    }
}
