using System.Diagnostics;
using FWO.Data.RulePositioning;
using RulebaseGraphReadResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Services.RulePositioning.RulebaseGraphReadValue,
    FWO.Services.RulePositioning.RulebaseGraphReadFailureCategory>;

namespace FWO.Services.RulePositioning;

/// <summary>
/// Reuses fixed-import rulebase graphs within one logical workflow operation.
/// </summary>
internal static class RulePositioningGraphCacheScope
{
    /// <summary>Operation-local graph cache state.</summary>
    private static readonly AsyncLocal<CacheState?> kCurrent = new();

    /// <summary>
    /// Begins or joins an operation scope. Nested workflow actions share the same cache.
    /// </summary>
    internal static IDisposable Begin()
    {
        CacheState state = kCurrent.Value ?? new CacheState();
        kCurrent.Value = state;
        state.Depth++;
        return new ScopeLease(state);
    }

    /// <summary>
    /// Reads one graph once per management and gateway in the active operation.
    /// </summary>
    internal static async Task<RulebaseGraphReadResult> ReadAsync(
        int managementId,
        int gatewayId,
        Func<Task<RulebaseGraphReadResult>> read,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long started = Stopwatch.GetTimestamp();
        CacheState? state = kCurrent.Value;
        if (state == null)
        {
            RulebaseGraphReadResult uncachedResult = await read();
            WriteCacheEvent(managementId, gatewayId, false, false, started);
            return uncachedResult;
        }

        (int ManagementId, int GatewayId) key = (managementId, gatewayId);
        Task<RulebaseGraphReadResult> pendingRead;
        bool cacheHit;
        lock (state.Reads)
        {
            cacheHit = state.Reads.TryGetValue(key, out pendingRead!);
            if (!cacheHit)
            {
                pendingRead = read();
                state.Reads[key] = pendingRead;
            }
        }

        try
        {
            RulebaseGraphReadResult result = await pendingRead.WaitAsync(cancellationToken);
            WriteCacheEvent(managementId, gatewayId, true, cacheHit, started);
            return result;
        }
        catch (Exception exception)
        {
            lock (state.Reads)
            {
                if (state.Reads.TryGetValue(key, out Task<RulebaseGraphReadResult>? cached) &&
                    ReferenceEquals(cached, pendingRead))
                {
                    state.Reads.Remove(key);
                }
            }
            RulePositioningDiagnosticLog.WriteException(
                "graph.cache",
                exception,
                Facts(managementId, gatewayId, true, cacheHit, started));
            throw;
        }
    }

    /// <summary>Writes one bounded cache lookup summary.</summary>
    private static void WriteCacheEvent(
        int managementId,
        int gatewayId,
        bool cacheActive,
        bool cacheHit,
        long started)
    {
        RulePositioningDiagnosticLog.WriteInfo(
            "graph.cache",
            "summary",
            Facts(managementId, gatewayId, cacheActive, cacheHit, started));
    }

    /// <summary>Builds the cache event's numeric facts.</summary>
    private static Dictionary<string, object?> Facts(
        int managementId,
        int gatewayId,
        bool cacheActive,
        bool cacheHit,
        long started) => new()
        {
            ["management_id"] = managementId,
            ["gateway_id"] = gatewayId,
            ["cache_active"] = cacheActive,
            ["cache_hit"] = cacheHit,
            ["duration_ms"] = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds
        };

    /// <summary>Mutable operation-local cache shared by nested leases.</summary>
    private sealed class CacheState
    {
        /// <summary>Gets or sets the nested cache-scope depth.</summary>
        internal int Depth { get; set; }
        /// <summary>Gets graph reads keyed by management and gateway.</summary>
        internal Dictionary<(int ManagementId, int GatewayId), Task<RulebaseGraphReadResult>> Reads { get; } = [];
    }

    /// <summary>Releases one nested cache scope.</summary>
    private sealed class ScopeLease(CacheState state) : IDisposable
    {
        private bool disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            state.Depth--;
            if (state.Depth == 0 && ReferenceEquals(kCurrent.Value, state))
            {
                kCurrent.Value = null;
            }
        }
    }
}
