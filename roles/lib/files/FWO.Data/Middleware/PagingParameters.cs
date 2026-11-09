namespace FWO.Data.Middleware
{
    /// <summary>
    /// Paging keys of the middleware list requests. Every list is ordered deterministically, and the
    /// <c>X-Has-More</c> response header tells whether further items follow the returned page.
    /// </summary>
    public class PagingParameters
    {
        /// <summary>
        /// Maximum number of items returned, between 1 and the configured <c>maxRestResponseObjectLimit</c>
        /// (default 10000). Null returns a page of that size.
        /// </summary>
        public int? Limit { get; set; }

        /// <summary>
        /// Number of items skipped before the first returned item, at least 0. Null skips no item.
        /// </summary>
        public int? Offset { get; set; }
    }
}
