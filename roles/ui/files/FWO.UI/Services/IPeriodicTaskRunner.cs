namespace FWO.Ui.Services
{
    /// <summary>
    /// Represents a reusable periodic task runner.
    /// Callers running on a synchronization context (e.g. the Blazor render dispatcher) should shut the
    /// runner down with <see cref="IAsyncDisposable.DisposeAsync"/> so that a callback still in flight can
    /// finish. The synchronous <see cref="IDisposable.Dispose"/> only waits for a limited time.
    /// </summary>
    public interface IPeriodicTaskRunner : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Starts the periodic execution loop.
        /// </summary>
        void Start();
    }
}
