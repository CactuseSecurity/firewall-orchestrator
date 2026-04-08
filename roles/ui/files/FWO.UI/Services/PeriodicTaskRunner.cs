using FWO.Logging;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Runs an asynchronous callback repeatedly with a fixed interval until disposed.
    /// </summary>
    public sealed class PeriodicTaskRunner : IDisposable
    {
        private readonly Func<Task> callback;
        private readonly TimeSpan interval;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private int started;

        /// <summary>
        /// Initializes a new instance of the <see cref="PeriodicTaskRunner"/> class.
        /// </summary>
        /// <param name="callback">Callback to execute on each interval.</param>
        /// <param name="interval">Interval between callback executions.</param>
        public PeriodicTaskRunner(Func<Task> callback, TimeSpan interval)
        {
            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));

            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
            }

            this.interval = interval;
        }

        /// <summary>
        /// Starts the periodic background execution.
        /// </summary>
        public void Start()
        {
            if (Interlocked.Exchange(ref started, 1) == 1)
            {
                return;
            }

            _ = RunAsync(cancellationTokenSource.Token);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                using PeriodicTimer timer = new(interval);

                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    await callback();
                }
            }
            catch (TaskCanceledException)
            {
                Log.WriteDebug(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)} stopped.");
            }
            catch (OperationCanceledException)
            {
                Log.WriteDebug(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)} stopped.");
            }
            catch (Exception ex)
            {
                Log.WriteError(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)} ran into an exception: {ex}", ex);
            }
        }
    }
}
