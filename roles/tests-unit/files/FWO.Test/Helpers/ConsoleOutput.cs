using System.Text;

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

    /// <summary>
    /// Buffer of one console capture. Reads and writes share a single lock, so a background
    /// thread still writing through a stale Console.Out reference cannot corrupt a snapshot.
    /// </summary>
    internal sealed class ConsoleCapture
    {
        private readonly LockedTextWriter writer = new();

        /// <summary>
        /// Writer that Console.SetOut is pointed at for the duration of the capture.
        /// </summary>
        internal TextWriter Writer => writer;

        /// <summary>
        /// Returns everything written to the console so far, without ending the capture.
        /// </summary>
        /// <returns>The console output written up to this call.</returns>
        public string Snapshot()
        {
            return writer.Snapshot();
        }

        /// <summary>
        /// Deliberately never disposed: a timer or background task started by an earlier test
        /// may still hold this writer through a stale Console.Out reference, and a late write
        /// has to stay harmless rather than throw ObjectDisposedException on that thread.
        /// </summary>
        private sealed class LockedTextWriter : TextWriter
        {
            private readonly StringBuilder builder = new();
            private readonly object gate = new();

            /// <summary>
            /// Encoding of the captured output.
            /// </summary>
            public override Encoding Encoding => Encoding.UTF8;

            /// <summary>
            /// Appends a single character. Every other TextWriter overload routes here or
            /// through the two overloads below.
            /// </summary>
            /// <param name="value">Character to append.</param>
            public override void Write(char value)
            {
                lock (gate)
                {
                    builder.Append(value);
                }
            }

            /// <summary>
            /// Appends a string.
            /// </summary>
            /// <param name="value">String to append.</param>
            public override void Write(string? value)
            {
                lock (gate)
                {
                    builder.Append(value);
                }
            }

            /// <summary>
            /// Appends a string followed by a line break.
            /// </summary>
            /// <param name="value">String to append.</param>
            public override void WriteLine(string? value)
            {
                lock (gate)
                {
                    builder.AppendLine(value);
                }
            }

            /// <summary>
            /// Returns the buffer content under the same lock the writes take.
            /// </summary>
            /// <returns>Everything appended so far.</returns>
            public string Snapshot()
            {
                lock (gate)
                {
                    return builder.ToString();
                }
            }
        }
    }
}
