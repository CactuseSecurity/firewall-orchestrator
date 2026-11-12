using System.Text.Json;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Remembers the number of tickets matching the where expression of the last count. Counting all matching tickets
    /// is much slower than loading one page, and the table loads a page on every page change while the count only
    /// changes with the task type and state dropdowns or the column filters. So the count is only queried again
    /// when the where expression changes or the cache is cleared.
    /// </summary>
    public sealed class WorkflowTicketCountCache
    {
        private string? countedWhere;
        private int ticketCount;

        /// <summary>
        /// Returns the cached count if it was counted for the same where expression.
        /// </summary>
        /// <param name="where">Where expression of the paged ticket query.</param>
        /// <param name="count">The cached count, or 0 if there is none for this where expression.</param>
        /// <returns>True if a count for this where expression is cached.</returns>
        public bool TryGet(Dictionary<string, object> where, out int count)
        {
            bool cached = countedWhere != null && countedWhere == Key(where);
            count = cached ? ticketCount : 0;
            return cached;
        }

        /// <summary>
        /// Caches the count of tickets matching the where expression, replacing the previous one.
        /// </summary>
        /// <param name="where">Where expression the tickets were counted for.</param>
        /// <param name="count">Number of matching tickets.</param>
        public void Store(Dictionary<string, object> where, int count)
        {
            countedWhere = Key(where);
            ticketCount = count;
        }

        /// <summary>
        /// Forgets the cached count, e.g. after tickets changed, so the next page counts again.
        /// </summary>
        public void Clear()
        {
            countedWhere = null;
            ticketCount = 0;
        }

        private static string Key(Dictionary<string, object> where)
        {
            return JsonSerializer.Serialize(where);
        }
    }
}
