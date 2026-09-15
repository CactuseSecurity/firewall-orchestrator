namespace FWO.Ui.Services
{
    /// <summary>
    /// Turns the row count the browser measured for the log data table into its page size.
    /// Kept out of the component so the decision can be tested without a browser.
    /// </summary>
    public static class LogDataTableLayout
    {
        /// <summary>
        /// Page size used until the browser has been measured.
        /// </summary>
        public const int kDefaultPageSize = 15;

        /// <summary>
        /// Answer of the browser measurement when there was nothing to measure at all. A window
        /// measured as too short for a single row reports zero instead, which is a real result
        /// and not a failure - the two must stay distinguishable (see logDataTable.js).
        /// </summary>
        public const int kCouldNotMeasure = -1;

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
        /// <param name="measuredRows">Rows that fit into the window, <see cref="kCouldNotMeasure"/> if there was nothing to measure.</param>
        /// <param name="currentPageSize">Page size the table has, kept if there was nothing to measure.</param>
        /// <returns>Page size within the supported bounds.</returns>
        public static int ResolvePageSize(int measuredRows, int currentPageSize)
        {
            if (measuredRows < 0)
            {
                // nothing to go by: the size the table already has is a better answer than the
                // default, which would make the table jump for no reason the user can see
                return currentPageSize;
            }
            // zero is a measurement, not a failure: the window is too short for a single row and
            // the floor below decides how many rows are shown anyway
            return Math.Clamp(measuredRows, kMinPageSize, kMaxPageSize);
        }
    }
}
