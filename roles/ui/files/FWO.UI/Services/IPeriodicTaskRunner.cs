namespace FWO.Ui.Services
{
    /// <summary>
    /// Represents a reusable periodic task runner.
    /// </summary>
    public interface IPeriodicTaskRunner : IDisposable
    {
        /// <summary>
        /// Starts the periodic execution loop.
        /// </summary>
        void Start();
    }
}
