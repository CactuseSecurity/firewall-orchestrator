using FWO.Logging;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Runs an asynchronous callback repeatedly with a fixed interval until disposed.
    /// </summary>
    public sealed class PeriodicTaskRunner : IPeriodicTaskRunner
    {
        /// <summary>
        /// Upper bound for the blocking wait of the synchronous <see cref="Dispose"/>. A callback may need
        /// the very thread that calls Dispose (e.g. the Blazor render dispatcher), so waiting without a
        /// limit would deadlock. Callers on such a thread should use <see cref="DisposeAsync"/> instead.
        /// </summary>
        private static readonly TimeSpan kSynchronousShutdownTimeout = TimeSpan.FromSeconds(5);

        private readonly Func<Task> callback;
        private readonly TimeSpan interval;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly object lifecycleLock = new();
        private Task executionTask = Task.CompletedTask;
        private int started;
        private bool disposed;
        private bool cancellationSourceDisposed;
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

        /// <summary>
        /// Starts the periodic background execution.
        /// </summary>
        public void Start()
        {
            lock (lifecycleLock)
            {
                ObjectDisposedException.ThrowIf(disposed, this);

                if (Interlocked.Exchange(ref started, 1) == 1)
                {
                    return;
                }

                Log.WriteDebug(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)}{DescribeTask()} started.");
                // Task.Run detaches the loop from the synchronization context of the caller. Without it the
                // loop would need the calling thread to make progress, which deadlocks as soon as that very
                // thread waits for the loop to end - exactly what happens when a Blazor component disposes
                // the runner on the render dispatcher. The scheduling itself gets no token on purpose: the
                // loop observes the cancellation on its own, whereas a cancelled scheduling would leave a
                // cancelled task behind that the shutdown then awaits.
                executionTask = Task.Run(() => RunAsync(cancellationTokenSource.Token), CancellationToken.None);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Task taskToWaitFor = BeginShutdown();

            if (taskToWaitFor.Wait(kSynchronousShutdownTimeout, CancellationToken.None))
            {
                CompleteShutdown();
                return;
            }

            // the loop is still inside a callback that apparently needs this thread: give up waiting instead
            // of blocking forever. The loop is cancelled and ends as soon as the callback returns.
            Log.WriteWarning(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)}{DescribeTask()} did not stop " +
                $"within {kSynchronousShutdownTimeout.TotalSeconds} seconds and is left to finish in the background. " +
                $"Use {nameof(DisposeAsync)} when disposing from a thread the callback depends on.");
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await BeginShutdown().ConfigureAwait(false);
            CompleteShutdown();
        }

        /// <summary>
        /// Marks the runner as disposed and cancels the loop.
        /// </summary>
        /// <returns>The task of the loop that is shutting down.</returns>
        private Task BeginShutdown()
        {
            lock (lifecycleLock)
            {
                if (!disposed)
                {
                    disposed = true;
                    cancellationTokenSource.Cancel();
                }
                return executionTask;
            }
        }

        /// <summary>
        /// Releases the cancellation source once the loop has ended.
        /// </summary>
        private void CompleteShutdown()
        {
            lock (lifecycleLock)
            {
                if (cancellationSourceDisposed)
                {
                    return;
                }

                cancellationSourceDisposed = true;
                cancellationTokenSource.Dispose();
            }
        }

        private string DescribeTask()
        {
            return !string.IsNullOrWhiteSpace(TaskName) ? $" {TaskName}" : "";
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                using PeriodicTimer timer = new(interval);

                // never resume on a captured synchronization context: the loop must be able to finish even
                // while the thread that started it is blocked waiting for exactly that
                while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await callback().ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                Log.WriteDebug(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)}{DescribeTask()} stopped.");
            }
            catch (OperationCanceledException)
            {
                Log.WriteDebug(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)}{DescribeTask()} stopped.");
            }
            catch (Exception ex)
            {
                Log.WriteError(nameof(PeriodicTaskRunner), $"{nameof(PeriodicTaskRunner)}{DescribeTask()} ran into an exception: {ex}", ex);
            }
        }
    }
}
