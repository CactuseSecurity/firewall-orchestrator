using System.Collections.Concurrent;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Result of one expiry sweep over the workflow email bundle store.
    /// </summary>
    /// <param name="DiscardedBundles">Number of collectors evicted that still held captured emails</param>
    /// <param name="DiscardedItems">Number of captured emails that were discarded unsent</param>
    public record WorkflowEmailBundleSweepResult(int DiscardedBundles, int DiscardedItems)
    {
        /// <summary>
        /// True when the sweep discarded captured emails, which must be reported instead of passing silently.
        /// </summary>
        public bool LostEmails => DiscardedItems > 0;
    }

    /// <summary>
    /// Keeps the workflow email bundle collectors of in-flight bundles. A bundle spans several action
    /// executions, so its collector has to outlive a single request; this store is the only place that
    /// holds that state and derives bundle keys, so key, ticket lock and authorization cannot drift apart.
    /// Register as a singleton.
    /// </summary>
    public sealed class WorkflowEmailBundleStore
    {
        /// <summary>
        /// Maximum number of concurrently tracked bundles. Reached only through abandoned bundles, so
        /// refusing further bundles is safer than evicting live ones - a refused bundle sends immediately.
        /// </summary>
        public const int kMaxBundles = 500;

        /// <summary>
        /// Maximum number of captured emails per bundle.
        /// </summary>
        public const int kMaxPendingItemsPerBundle = 500;

        private static readonly TimeSpan kMaxAge = TimeSpan.FromMinutes(30);

        private readonly ConcurrentDictionary<string, WorkflowEmailBundleCollector> bundles = new();

        /// <summary>
        /// Returns the collector of a bundle, creating it when the caller starts a new one.
        /// </summary>
        /// <param name="resolvedTicketId">Ticket id the caller was authorized against, not the raw request value</param>
        /// <param name="bundleId">Client supplied bundle id, already format validated</param>
        /// <param name="callerDn">DN of the calling user, bound to the bundle on creation</param>
        /// <returns>The collector, or null when the bundle belongs to another caller or the store is full</returns>
        public WorkflowEmailBundleCollector? GetOrCreate(long resolvedTicketId, string bundleId, string callerDn)
        {
            string key = BuildKey(resolvedTicketId, bundleId);
            WorkflowEmailBundleCollector collector = bundles.GetOrAdd(key, _ => new WorkflowEmailBundleCollector(callerDn));
            if (!collector.BelongsTo(callerDn))
            {
                return null;
            }

            if (bundles.Count > kMaxBundles && collector.PendingItems.Count == 0)
            {
                bundles.TryRemove(key, out _);
                return null;
            }

            collector.Touch();
            return collector;
        }

        /// <summary>
        /// Returns the collector of an existing bundle without creating one.
        /// </summary>
        /// <param name="resolvedTicketId">Ticket id the caller was authorized against</param>
        /// <param name="bundleId">Client supplied bundle id, already format validated</param>
        /// <param name="callerDn">DN of the calling user</param>
        /// <returns>The collector, or null when no bundle exists for this caller and key</returns>
        public WorkflowEmailBundleCollector? Get(long resolvedTicketId, string bundleId, string callerDn)
        {
            return bundles.TryGetValue(BuildKey(resolvedTicketId, bundleId), out WorkflowEmailBundleCollector? collector)
                && collector.BelongsTo(callerDn) ? collector : null;
        }

        /// <summary>
        /// Drops a bundle once it has been flushed or abandoned.
        /// </summary>
        /// <param name="resolvedTicketId">Ticket id the caller was authorized against</param>
        /// <param name="bundleId">Client supplied bundle id</param>
        public void Remove(long resolvedTicketId, string bundleId)
        {
            bundles.TryRemove(BuildKey(resolvedTicketId, bundleId), out _);
        }

        /// <summary>
        /// Evicts bundles that have not been touched within the maximum age. Runs unconditionally on every
        /// workflow action execution so abandoned bundles cannot accumulate.
        /// </summary>
        /// <returns>What the sweep discarded, so lost emails can be reported by the caller</returns>
        public WorkflowEmailBundleSweepResult Sweep()
        {
            DateTime threshold = DateTime.UtcNow.Subtract(kMaxAge);
            int discardedBundles = 0;
            int discardedItems = 0;
            foreach (KeyValuePair<string, WorkflowEmailBundleCollector> bundle in bundles)
            {
                if (bundle.Value.LastTouchedAt >= threshold || !bundles.TryRemove(bundle.Key, out WorkflowEmailBundleCollector? removed))
                {
                    continue;
                }

                if (removed.PendingItems.Count > 0)
                {
                    ++discardedBundles;
                    discardedItems += removed.PendingItems.Count;
                }
            }

            return new WorkflowEmailBundleSweepResult(discardedBundles, discardedItems);
        }

        private static string BuildKey(long resolvedTicketId, string bundleId)
        {
            return $"{resolvedTicketId}:{bundleId}";
        }
    }
}
