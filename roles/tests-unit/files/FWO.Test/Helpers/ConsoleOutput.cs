namespace FWO.Test
{
    /// <summary>
    /// Captures the process-wide console output without allowing concurrent captures to
    /// replace each other's writers.
    /// </summary>
    internal static class ConsoleOutput
    {
        private static readonly SemaphoreSlim kCaptureLock = new(1, 1);

        /// <summary>
        /// Captures console output written while the asynchronous action runs.
        /// </summary>
        public static async Task<string> CaptureAsync(Func<Task> action)
        {
            await kCaptureLock.WaitAsync();
            using StringWriter output = new();
            TextWriter synchronizedOutput = TextWriter.Synchronized(output);
            TextWriter originalOutput = Console.Out;
            try
            {
                Console.SetOut(synchronizedOutput);
                await action();
                await synchronizedOutput.FlushAsync();
                return output.ToString();
            }
            finally
            {
                Console.SetOut(originalOutput);
                kCaptureLock.Release();
            }
        }
    }
}
