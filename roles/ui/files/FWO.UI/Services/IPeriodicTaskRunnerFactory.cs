namespace FWO.Ui.Services
{
    /// <summary>
    /// Creates periodic task runners.
    /// </summary>
    public interface IPeriodicTaskRunnerFactory
    {
        /// <summary>
        /// Creates a new periodic runner for the supplied callback.
        /// </summary>
        /// <param name="callback">Callback to execute on each interval.</param>
        /// <param name="interval">Interval between executions.</param>
        /// <param name="taskName">Optional name used for logging.</param>
        /// <returns>A new periodic task runner.</returns>
        IPeriodicTaskRunner Create(Func<Task> callback, TimeSpan interval, string taskName = "");
    }

    /// <summary>
    /// Default factory for <see cref="PeriodicTaskRunner"/>.
    /// </summary>
    public sealed class PeriodicTaskRunnerFactory : IPeriodicTaskRunnerFactory
    {
        /// <inheritdoc />
        public IPeriodicTaskRunner Create(Func<Task> callback, TimeSpan interval, string taskName = "")
        {
            return new PeriodicTaskRunner(callback, interval, taskName);
        }
    }
}
