using System.Text;

namespace FWO.Test.Helpers
{
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
