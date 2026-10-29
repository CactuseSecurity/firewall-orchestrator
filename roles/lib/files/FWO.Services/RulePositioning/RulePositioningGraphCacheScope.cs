namespace FWO.Services.RulePositioning;

/// <summary>
/// Reuses fixed-import rulebase graphs within one logical workflow operation.
/// </summary>
internal static class RulePositioningGraphCacheScope
{
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
        CacheState? state = kCurrent.Value;
        if (state == null)
        {
            return await read();
        }

        (int ManagementId, int GatewayId) key = (managementId, gatewayId);
        Task<RulebaseGraphReadResult> pendingRead;
        lock (state.Reads)
        {
            if (!state.Reads.TryGetValue(key, out pendingRead!))
            {
                pendingRead = read();
                state.Reads[key] = pendingRead;
            }
        }

        try
        {
            return await pendingRead.WaitAsync(cancellationToken);
        }
        catch
        {
            lock (state.Reads)
            {
                if (state.Reads.TryGetValue(key, out Task<RulebaseGraphReadResult>? cached) &&
                    ReferenceEquals(cached, pendingRead))
                {
                    state.Reads.Remove(key);
                }
            }
            throw;
        }
    }

    private sealed class CacheState
    {
        internal int Depth { get; set; }
        internal Dictionary<(int ManagementId, int GatewayId), Task<RulebaseGraphReadResult>> Reads { get; } = [];
    }

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
