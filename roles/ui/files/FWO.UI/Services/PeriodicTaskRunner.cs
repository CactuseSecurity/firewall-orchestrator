using FWO.Logging;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Runs an asynchronous callback repeatedly with a fixed interval until disposed.
    /// </summary>
    public sealed class PeriodicTaskRunner : IPeriodicTaskRunner
    {
        private readonly Func<Task> callback;
        private readonly TimeSpan interval;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private int started;
        private readonly string TaskName;

        /// <summary>
        /// Initializes a new instance of the <see cref="PeriodicTaskRunner"/> class.
        /// </summary>
        /// <param name="callback">Callback to execute on each interval.</param>
        /// <param name="interval">Interval between callback executions.</param>
        /// <param name="taskName">Optional name used for logging.</param>
        public PeriodicTaskRunner(Func<Task> callback, TimeSpan interval, string taskName = "")
        {
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));

            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
            }

            this.interval = interval;
            TaskName = taskName;
        }

        public void Start()
        {
            Log.WriteDebug(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)}{(!string.IsNullOrWhiteSpace(TaskName) ? $" {TaskName}" : "")} started.");

            if (Interlocked.Exchange(ref started, 1) == 1)
            {
                return;
            }

            _ = RunAsync(cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                using PeriodicTimer timer = new(interval);

                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await callback();
                }
            }
            catch (TaskCanceledException)
            {
                Log.WriteDebug(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)}{(!string.IsNullOrWhiteSpace(TaskName) ? $" {TaskName}" : "")} stopped.");
            }
            catch (OperationCanceledException)
            {
                Log.WriteDebug(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)}{(!string.IsNullOrWhiteSpace(TaskName) ? $" {TaskName}" : "")} stopped.");
            }
            catch (Exception ex)
            {
                Log.WriteError(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)}{(!string.IsNullOrWhiteSpace(TaskName) ? $" {TaskName}" : "")} ran into an exception: {ex}", ex);
            }
        }
    }
}
