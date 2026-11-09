namespace FWO.Test.Helpers
{
    /// <summary>
    /// Captures the process-wide console output without allowing concurrent captures to
    /// replace each other's writers, and without exposing the buffer to an unsynchronized read.
    /// </summary>
    internal static class ConsoleOutput
    {
        private static readonly SemaphoreSlim kCaptureLock = new(1, 1);

        /// <summary>
        /// Upper bound for waiting on the capture lock. Exceeding it means another capture is
        /// nested inside this one or never returned, which would otherwise hang the whole run.
        /// </summary>
        private static readonly TimeSpan kCaptureTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Captures console output written while the asynchronous action runs.
        /// </summary>
        /// <param name="action">Action whose console output is captured.</param>
        /// <returns>Everything written to the console while the action ran.</returns>
        public static Task<string> CaptureAsync(Func<Task> action)
        {
            return CaptureAsync(_ => action(), kCaptureTimeout);
        }

        /// <summary>
        /// Captures console output and hands the running action a handle it can take
        /// intermediate snapshots from, for assertions on what was written up to a point.
        /// </summary>
        /// <param name="action">Action whose console output is captured.</param>
        /// <returns>Everything written to the console while the action ran.</returns>
        public static Task<string> CaptureAsync(Func<ConsoleCapture, Task> action)
        {
            return CaptureAsync(action, kCaptureTimeout);
        }

        /// <summary>
        /// Captures console output, failing after the given timeout instead of waiting forever
        /// for the capture lock.
        /// </summary>
        /// <param name="action">Action whose console output is captured.</param>
        /// <param name="timeout">How long to wait for an ongoing capture to finish.</param>
        /// <returns>Everything written to the console while the action ran.</returns>
        public static async Task<string> CaptureAsync(Func<ConsoleCapture, Task> action, TimeSpan timeout)
        {
            if (!await kCaptureLock.WaitAsync(timeout))
            {
                throw new InvalidOperationException(
                    "Could not acquire the console capture lock within " + timeout +
                    ". A nested ConsoleOutput.CaptureAsync call or a capture that never returned is holding it.");
            }

            ConsoleCapture capture = new();
            TextWriter originalOutput = Console.Out;
            try
            {
                Console.SetOut(capture.Writer);
                await action(capture);
            }
            catch
            {
                // the diverted log is usually what explains the failure, so hand it back to the
                // real console instead of dropping it together with the capture
                originalOutput.Write(capture.Snapshot());
                throw;
            }
            finally
            {
                Console.SetOut(originalOutput);
                kCaptureLock.Release();
            }

            return capture.Snapshot();
        }
    }
}
