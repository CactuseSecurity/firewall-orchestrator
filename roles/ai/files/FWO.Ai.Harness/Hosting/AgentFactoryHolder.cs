using FWO.Ai.Harness.Agent;

namespace FWO.Ai.Harness.Hosting;

/// <summary>
/// Holds the agent factory of the AI settings currently in effect, which <see cref="Settings.AiSettingsRefresher"/>
/// replaces when the settings change. A run leases the factory for its whole duration, so a factory that is replaced
/// is disposed only after its last run has ended; new runs get the new factory right away. Thread-safe.
/// </summary>
public sealed class AgentFactoryHolder : IDisposable
{
    /// <summary>Why runs fail while no AI provider is configured.</summary>
    public const string kNotConfiguredReason = "No AI provider is configured.";

    private readonly object sync = new();
    private HeldFactory? current;
    private string unavailableReason = kNotConfiguredReason;
    private bool disposed;

    /// <summary>
    /// Creates a holder without factory, so that runs fail with <see cref="kNotConfiguredReason"/> until one is set.
    /// </summary>
    public AgentFactoryHolder()
    {
    }

    /// <summary>
    /// Creates a holder with the given factory, e.g. one with a scripted chat client of a test.
    /// </summary>
    /// <param name="factory">Factory to hold; the holder takes ownership of it</param>
    public AgentFactoryHolder(FwoAgentFactory factory)
    {
        SetFactory(factory);
    }

    /// <summary>Whether runs currently get an agent factory.</summary>
    public bool HasFactory
    {
        get
        {
            lock (sync)
            {
                return current != null;
            }
        }
    }

    /// <summary>Why runs fail while there is no factory; never contains a secret.</summary>
    public string UnavailableReason
    {
        get
        {
            lock (sync)
            {
                return unavailableReason;
            }
        }
    }

    /// <summary>
    /// Leases the current factory for a run. Dispose the lease when the run has ended.
    /// </summary>
    /// <returns>The lease of the current factory.</returns>
    /// <exception cref="InvalidOperationException">There is no factory, see <see cref="UnavailableReason"/>.</exception>
    /// <exception cref="ObjectDisposedException">The holder has been disposed.</exception>
    public AgentFactoryLease Acquire()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            HeldFactory held = current ?? throw new InvalidOperationException(unavailableReason);
            held.Leases++;
            return new AgentFactoryLease(this, held);
        }
    }

    /// <summary>
    /// Replaces the current factory by the given one. The replaced factory is disposed once its last run has ended.
    /// </summary>
    /// <param name="factory">New factory; the holder takes ownership of it</param>
    public void SetFactory(FwoAgentFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Replace(new HeldFactory(factory), kNotConfiguredReason);
    }

    /// <summary>
    /// Removes the current factory, so that new runs fail with the given reason. The removed factory is disposed once
    /// its last run has ended.
    /// </summary>
    /// <param name="reason">Why runs fail; must not contain a secret</param>
    public void SetUnavailable(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Replace(null, reason);
    }

    /// <summary>
    /// Removes the current factory and disposes it once its last run has ended; runs started afterwards fail.
    /// </summary>
    public void Dispose()
    {
        HeldFactory? retired;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            retired = Retire(current);
            current = null;
        }
        retired?.Factory.Dispose();
    }

    /// <summary>
    /// Ends the lease of a run, and disposes its factory if it has been replaced and this was its last run.
    /// </summary>
    internal void Release(HeldFactory held)
    {
        bool disposeFactory;
        lock (sync)
        {
            held.Leases--;
            disposeFactory = held.Retired && held.Leases == 0;
        }
        if (disposeFactory)
        {
            held.Factory.Dispose();
        }
    }

    /// <summary>
    /// Sets the given factory and reason, and disposes the replaced factory if no run uses it.
    /// </summary>
    private void Replace(HeldFactory? replacement, string reason)
    {
        HeldFactory? retired;
        lock (sync)
        {
            if (disposed)
            {
                replacement?.Factory.Dispose();
                throw new ObjectDisposedException(nameof(AgentFactoryHolder));
            }
            retired = Retire(current);
            current = replacement;
            unavailableReason = reason;
        }
        retired?.Factory.Dispose();
    }

    /// <summary>
    /// Marks a factory as replaced; returns it if no run uses it, so that the caller disposes it outside the lock.
    /// </summary>
    private static HeldFactory? Retire(HeldFactory? held)
    {
        if (held == null)
        {
            return null;
        }
        held.Retired = true;
        return held.Leases == 0 ? held : null;
    }

    /// <summary>
    /// A factory with the number of runs using it, guarded by the lock of the holder.
    /// </summary>
    internal sealed class HeldFactory(FwoAgentFactory factory)
    {
        public FwoAgentFactory Factory { get; } = factory;

        public int Leases { get; set; }

        public bool Retired { get; set; }
    }
}

/// <summary>
/// Lease of the agent factory for one run, see <see cref="AgentFactoryHolder.Acquire"/>.
/// </summary>
public sealed class AgentFactoryLease : IDisposable
{
    private readonly AgentFactoryHolder holder;
    private readonly AgentFactoryHolder.HeldFactory held;
    private int released;

    /// <summary>
    /// Creates the lease of a held factory.
    /// </summary>
    internal AgentFactoryLease(AgentFactoryHolder holder, AgentFactoryHolder.HeldFactory held)
    {
        this.holder = holder;
        this.held = held;
    }

    /// <summary>The leased factory; usable until the lease is disposed.</summary>
    public FwoAgentFactory Factory => held.Factory;

    /// <summary>
    /// Ends the lease; disposing it again does nothing.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref released, 1) == 0)
        {
            holder.Release(held);
        }
    }
}
